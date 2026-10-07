using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nexus.Storage;

public sealed record TemporaryFileCandidate(string Path, long Bytes, DateTime LastWriteUtc, DateTime LastAccessUtc)
{
    public string Display => $"{Path} · {Bytes / 1048576d:0.00} MB";
}
public sealed record CleanupIssue(string Path, string Message);

public sealed class TemporaryScan
{
    internal TemporaryScan(string root, IReadOnlyList<TemporaryFileCandidate> files, int examined,
        bool limited, IReadOnlyList<CleanupIssue> issues)
    { Root = root; Files = files; ExaminedFiles = examined; Limited = limited; Issues = issues; }
    public string Root { get; }
    public IReadOnlyList<TemporaryFileCandidate> Files { get; }
    public int ExaminedFiles { get; }
    public bool Limited { get; }
    public IReadOnlyList<CleanupIssue> Issues { get; }
    public long CandidateBytes => Files.Sum(f => f.Bytes);
}

// Recycled bytes are still on the volume until the user empties the Recycle Bin.
public sealed record TemporaryCleanupResult(int Recycled, long RecycledBytes, int Skipped,
    IReadOnlyList<CleanupIssue> Errors, bool Cancelled, bool Limited);

public interface IRecycleBin
{
    // Implementations must recycle, or throw. Permanent deletion is never permitted.
    void Recycle(string path, Func<bool> revalidate, CancellationToken cancellationToken);
}

/// <summary>Reviews old regular files beneath one explicit root; never removes directories.</summary>
public sealed class TemporaryCleaner
{
    public static readonly TimeSpan MinimumAge = TimeSpan.FromDays(7);
    private static readonly TimeSpan ScanTimeLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CleanupTimeLimit = TimeSpan.FromSeconds(60);
    private readonly IRecycleBin recycleBin;
    private readonly int maximumFiles;
    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public TemporaryCleaner(IRecycleBin? recycleBin = null, int maximumFiles = 2000)
    {
        if (maximumFiles is < 1 or > 2000) throw new ArgumentOutOfRangeException(nameof(maximumFiles));
        this.recycleBin = recycleBin ?? new WindowsRecycleBin();
        this.maximumFiles = maximumFiles;
    }

    public TemporaryScan Scan(string suppliedRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suppliedRoot);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(suppliedRoot));
        EnsureNoReparseAncestors(root);
        if ((File.GetAttributes(root) & FileAttributes.Directory) == 0)
            throw new ArgumentException("A pasta de temporários não existe.", nameof(suppliedRoot));
        var cutoff = DateTime.UtcNow - MinimumAge;
        var files = new List<TemporaryFileCandidate>();
        var issues = new List<CleanupIssue>();
        var directories = new Stack<(string Path, int Depth)>();
        directories.Push((root, 0));
        var timer = Stopwatch.StartNew();
        var examined = 0; var entries = 0; var limited = false;
        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (timer.Elapsed >= ScanTimeLimit || examined >= maximumFiles || entries >= 20000)
            { limited = true; break; }
            var current = directories.Pop();
            try
            {
                EnsureNoReparseAncestors(current.Path);
                foreach (var path in Directory.EnumerateFileSystemEntries(current.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (timer.Elapsed >= ScanTimeLimit || examined >= maximumFiles || ++entries > 20000)
                    { limited = true; break; }
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0) continue;
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            if (current.Depth < 24) directories.Push((path, current.Depth + 1));
                            else limited = true;
                            continue;
                        }
                        examined++;
                        EnsureNoReparseAncestors(path);
                        var file = Snapshot(path);
                        if (file.LastWriteUtc <= cutoff && file.LastAccessUtc <= cutoff) files.Add(file);
                    }
                    catch (Exception ex) when (IsFileError(ex)) { issues.Add(new(path, ex.Message)); }
                }
            }
            catch (Exception ex) when (IsFileError(ex)) { issues.Add(new(current.Path, ex.Message)); }
            if (examined >= maximumFiles || entries > 20000 || timer.Elapsed >= ScanTimeLimit)
            { limited = true; break; }
        }
        return new(root, files.AsReadOnly(), examined, limited, issues.AsReadOnly());
    }

    public Task<TemporaryCleanupResult> RecycleAsync(TemporaryScan scan,
        IEnumerable<TemporaryFileCandidate> selectedFiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(selectedFiles);
        // Capture the reviewed selection before starting work. A caller cannot add a path absent from the scan.
        var available = scan.Files.ToDictionary(f => f.Path, PathComparer);
        var selection = selectedFiles.DistinctBy(f => f.Path, PathComparer).Take(maximumFiles + 1).ToArray();
        if (selection.Length > maximumFiles || selection.Any(f => !available.TryGetValue(f.Path, out var original) || original != f))
            throw new ArgumentException("A seleção não pertence à análise apresentada.", nameof(selectedFiles));
        var completion = new TaskCompletionSource<TemporaryCleanupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            try { completion.SetResult(RecycleReviewed(scan.Root, selection, cancellationToken)); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "NEXUS temporary recycling" };
        if (OperatingSystem.IsWindows()) worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        return completion.Task;
    }

    private TemporaryCleanupResult RecycleReviewed(string root, TemporaryFileCandidate[] files, CancellationToken token)
    {
        var errors = new List<CleanupIssue>();
        var recycled = 0; long bytes = 0; var skipped = 0; var cancelled = false; var limited = false;
        var timer = Stopwatch.StartNew();
        for (var i = 0; i < files.Length; i++)
        {
            if (token.IsCancellationRequested) { cancelled = true; skipped += files.Length - i; break; }
            if (timer.Elapsed >= CleanupTimeLimit) { limited = true; skipped += files.Length - i; break; }
            var file = files[i];
            try
            {
                if (!UnchangedAndSafe(root, file)) { skipped++; continue; }
                recycleBin.Recycle(file.Path, () => UnchangedAndSafe(root, file), token);
                recycled++; bytes += file.Bytes;
            }
            catch (OperationCanceledException) { cancelled = true; skipped += files.Length - i; break; }
            catch (Exception ex) when (IsFileError(ex) || ex is COMException || ex is InvalidOperationException || ex is PlatformNotSupportedException)
            { errors.Add(new(file.Path, ex.Message)); }
        }
        return new(recycled, bytes, skipped, errors.AsReadOnly(), cancelled, limited);
    }

    private static bool UnchangedAndSafe(string root, TemporaryFileCandidate file)
    {
        try
        {
            var fullPath = Path.GetFullPath(file.Path);
            var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, PathComparison)) return false;
            EnsureNoReparseAncestors(fullPath);
            var attributes = File.GetAttributes(fullPath);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0) return false;
            var current = Snapshot(fullPath);
            var cutoff = DateTime.UtcNow - MinimumAge;
            return current == file && current.LastWriteUtc <= cutoff && current.LastAccessUtc <= cutoff;
        }
        catch (Exception ex) when (IsFileError(ex)) { return false; }
    }

    private static TemporaryFileCandidate Snapshot(string path)
    {
        var file = new FileInfo(path); file.Refresh();
        if (!file.Exists) throw new FileNotFoundException("O ficheiro já não existe.", path);
        return new(Path.GetFullPath(path), file.Length, file.LastWriteTimeUtc, file.LastAccessTimeUtc);
    }

    private static void EnsureNoReparseAncestors(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Pasta ou ficheiro redirecionado: não será seguido.");
        }
    }

    private static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}

/// <summary>Windows 8+ forced recycle via IFileOperation; no permanent-delete fallback.</summary>
public sealed class WindowsRecycleBin : IRecycleBin
{
    // Microsoft documentation: SetOperationFlags, PreDeleteItem and PostDeleteItem (shobjidl_core.h).
    // FOFX_RECYCLEONDELETE requests recycling rather than permanent deletion.
    // A progress sink vetoes a non-recycling transfer and verifies a Recycle Bin destination.
    public void Recycle(string path, Func<bool> revalidate, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2)) throw new PlatformNotSupportedException("A reciclagem exige Windows 8 ou posterior.");
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("A reciclagem deve executar numa thread STA.");
        cancellationToken.ThrowIfCancellationRequested();
        if (!revalidate()) throw new IOException("O ficheiro mudou desde a análise; não foi reciclado.");
        IFileOperation? operation = null; IShellItem? item = null;
        try
        {
            operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"), true)!)!;
            // No confirmation-as-yes flag: errors stop, and permanent-delete confirmations are never accepted.
            operation.SetOperationFlags(0x0040 | 0x00080000 | 0x00100000 | 0x0400 | 0x0004 | 0x2000 | 0x1000);
            var id = typeof(IShellItem).GUID;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(Path.GetFullPath(path), IntPtr.Zero, ref id, out item));
            var sink = new RecycleSink(revalidate, cancellationToken);
            operation.DeleteItem(item, sink);
            try { operation.PerformOperations(); }
            catch (COMException) when (sink.Recycled) { return; }
            operation.GetAnyOperationsAborted(out var aborted);
            if (sink.Recycled) return; // Cancellation after a completed move must not hide that result.
            cancellationToken.ThrowIfCancellationRequested();
            if (sink.Failure < 0) Marshal.ThrowExceptionForHR(sink.Failure);
            if (aborted || !sink.Recycled) throw new IOException("O Windows não confirmou a transferência para a Lixeira; operação interrompida.");
        }
        finally
        {
            if (item is not null) Marshal.FinalReleaseComObject(item);
            if (operation is not null) Marshal.FinalReleaseComObject(operation);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid interfaceId, out IShellItem item);

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid id, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint name, out IntPtr result);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(IFileOperationProgressSink sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(IntPtr dialog);
        void SetProperties(IntPtr properties);
        void SetOwnerWindow(IntPtr owner);
        void ApplyPropertiesToItem(IShellItem item);
        void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink sink);
        void RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IFileOperationProgressSink sink);
        void MoveItems(IntPtr items, IShellItem destination);
        void CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IFileOperationProgressSink sink);
        void CopyItems(IntPtr items, IShellItem destination);
        void DeleteItem(IShellItem item, IFileOperationProgressSink sink);
        void DeleteItems(IntPtr items);
        void NewItem(IShellItem destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, IFileOperationProgressSink sink);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }

    [Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface IFileOperationProgressSink
    {
        [PreserveSig] int StartOperations();
        [PreserveSig] int FinishOperations(int result);
        [PreserveSig] int PreRenameItem(uint flags, IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostRenameItem(uint flags, IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IShellItem? newItem);
        [PreserveSig] int PreMoveItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name);
        [PreserveSig] int PostMoveItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, int result, IShellItem? newItem);
        [PreserveSig] int PreCopyItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name);
        [PreserveSig] int PostCopyItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, int result, IShellItem? newItem);
        [PreserveSig] int PreDeleteItem(uint flags, IShellItem item);
        [PreserveSig] int PostDeleteItem(uint flags, IShellItem item, int result, IShellItem? recycledItem);
        [PreserveSig] int PreNewItem(uint flags, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostNewItem(uint flags, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, uint attributes, int result, IShellItem? newItem);
        [PreserveSig] int UpdateProgress(uint total, uint completed);
        [PreserveSig] int ResetTimer();
        [PreserveSig] int PauseTimer();
        [PreserveSig] int ResumeTimer();
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class RecycleSink(Func<bool> revalidate, CancellationToken token) : IFileOperationProgressSink
    {
        private const int Abort = unchecked((int)0x80004004);
        public bool Recycled { get; private set; }
        public int Failure { get; private set; }
        public int StartOperations() => token.IsCancellationRequested ? Abort : 0;
        public int FinishOperations(int result) { if (result < 0) Failure = result; return 0; }
        public int PreDeleteItem(uint flags, IShellItem item)
        {
            try
            {
                if (token.IsCancellationRequested || (flags & 0x80) == 0 || !revalidate()) return Failure = Abort;
                return 0;
            }
            catch { return Failure = Abort; }
        }
        public int PostDeleteItem(uint flags, IShellItem item, int result, IShellItem? recycledItem)
        {
            if (result < 0) return Failure = result;
            Recycled = recycledItem is not null;
            return Recycled ? 0 : Failure = Abort;
        }
        public int PreRenameItem(uint f, IShellItem i, string n) => Abort;
        public int PostRenameItem(uint f, IShellItem i, string n, int r, IShellItem? o) => 0;
        public int PreMoveItem(uint f, IShellItem i, IShellItem d, string? n) => Abort;
        public int PostMoveItem(uint f, IShellItem i, IShellItem d, string? n, int r, IShellItem? o) => 0;
        public int PreCopyItem(uint f, IShellItem i, IShellItem d, string? n) => Abort;
        public int PostCopyItem(uint f, IShellItem i, IShellItem d, string? n, int r, IShellItem? o) => 0;
        public int PreNewItem(uint f, IShellItem d, string n) => Abort;
        public int PostNewItem(uint f, IShellItem d, string n, string? t, uint a, int r, IShellItem? o) => 0;
        public int UpdateProgress(uint total, uint completed) => token.IsCancellationRequested ? Abort : 0;
        public int ResetTimer() => 0; public int PauseTimer() => 0; public int ResumeTimer() => 0;
    }
}
