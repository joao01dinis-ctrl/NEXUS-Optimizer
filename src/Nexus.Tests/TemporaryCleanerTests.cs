using System.Diagnostics;
using Nexus.Storage;
using Xunit;

namespace Nexus.Tests;

public sealed class TemporaryCleanerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Nexus-cleanup-tests-" + Guid.NewGuid());
    private readonly List<string> junctions = [];
    private sealed class FakeBin : IRecycleBin
    {
        public readonly List<string> Calls = [];
        public Action<string>? Before;
        public Action? After;
        public bool Fail;
        public void Recycle(string path, Func<bool> revalidate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Before?.Invoke(path);
            if (!revalidate()) throw new IOException("changed before move");
            Calls.Add(path);
            if (Fail) throw new IOException("recycle unavailable");
            After?.Invoke();
        }
    }
    private string File(string relative, int writeDays = 10, int accessDays = 10)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "test-content");
        System.IO.File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-writeDays));
        System.IO.File.SetLastAccessTimeUtc(path, DateTime.UtcNow.AddDays(-accessDays));
        return path;
    }

    [Fact] public void ScanRequiresBothOldWriteAndAccessAndOnlyExaminesTheGivenRoot()
    {
        var old = File("inside/old.tmp");
        File("inside/recent-write.tmp", 1, 10); File("inside/recent-access.tmp", 10, 1);
        File("outside/not-selected.tmp"); Directory.CreateDirectory(Path.Combine(root, "inside/empty"));
        var scan = new TemporaryCleaner(new FakeBin()).Scan(Path.Combine(root, "inside"));
        Assert.Equal(old, Assert.Single(scan.Files).Path);
        Assert.Equal(3, scan.ExaminedFiles); Assert.Equal(12, scan.CandidateBytes);
        Assert.Empty(scan.Issues);
    }

    [Fact] public async Task ChangedCandidatesAreSkippedAndDirectoriesArePreserved()
    {
        var a = File("data/changed-size.tmp"); var b = File("data/accessed.tmp");
        var bin = new FakeBin(); var cleaner = new TemporaryCleaner(bin); var scan = cleaner.Scan(root);
        System.IO.File.AppendAllText(a, "new"); System.IO.File.SetLastAccessTimeUtc(b, DateTime.UtcNow);
        var result = await cleaner.RecycleAsync(scan, scan.Files);
        Assert.Equal(0, result.Recycled); Assert.Equal(2, result.Skipped); Assert.Empty(bin.Calls);
        Assert.True(Directory.Exists(Path.Combine(root, "data")));
        Assert.True(System.IO.File.Exists(a)); Assert.True(System.IO.File.Exists(b));
    }

    [Fact] public async Task BackendRevalidatesImmediatelyBeforeTheMove()
    {
        var file = File("old.tmp");
        var bin = new FakeBin { Before = path => System.IO.File.AppendAllText(path, "changed") };
        var cleaner = new TemporaryCleaner(bin); var scan = cleaner.Scan(root);
        var result = await cleaner.RecycleAsync(scan, scan.Files);
        Assert.Equal(0, result.Recycled); Assert.Single(result.Errors); Assert.Empty(bin.Calls);
        Assert.True(System.IO.File.Exists(file));
    }

    [Fact] public void RejectsAFileThatWasNotInTheReviewedSnapshot()
    {
        var known = File("inside/known.tmp"); var outside = File("outside.tmp");
        var cleaner = new TemporaryCleaner(new FakeBin()); var scan = cleaner.Scan(Path.GetDirectoryName(known)!);
        var fabricated = scan.Files[0] with { Path = outside };
        Assert.Throws<ArgumentException>(() => { _ = cleaner.RecycleAsync(scan, [fabricated]); });
        Assert.True(System.IO.File.Exists(outside));
    }

    [Fact] public async Task ASelectionIsDeduplicatedAndARecycleFailureNeverDeletes()
    {
        var file = File("old.tmp"); var bin = new FakeBin { Fail = true };
        var cleaner = new TemporaryCleaner(bin); var scan = cleaner.Scan(root);
        var result = await cleaner.RecycleAsync(scan, [scan.Files[0], scan.Files[0]]);
        Assert.Single(bin.Calls); Assert.Equal(0, result.Recycled); Assert.Single(result.Errors);
        Assert.True(System.IO.File.Exists(file));
    }

    [Fact] public async Task CancellationBeforeMovingPreservesEveryFile()
    {
        var file = File("old.tmp"); var bin = new FakeBin(); var cleaner = new TemporaryCleaner(bin);
        var scan = cleaner.Scan(root); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var result = await cleaner.RecycleAsync(scan, scan.Files, cancellation.Token);
        Assert.True(result.Cancelled); Assert.Equal(1, result.Skipped); Assert.Empty(bin.Calls);
        Assert.True(System.IO.File.Exists(file));
        Assert.Throws<OperationCanceledException>(() => cleaner.Scan(root, cancellation.Token));
    }

    [Fact] public async Task CancellationAfterACompletedMoveRetainsItsCountAndStopsTheRest()
    {
        File("one.tmp"); File("two.tmp"); File("three.tmp");
        using var cancellation = new CancellationTokenSource();
        var bin = new FakeBin { After = cancellation.Cancel }; var cleaner = new TemporaryCleaner(bin);
        var scan = cleaner.Scan(root); var result = await cleaner.RecycleAsync(scan, scan.Files, cancellation.Token);
        Assert.True(result.Cancelled); Assert.Equal(1, result.Recycled); Assert.Equal(2, result.Skipped);
        Assert.Equal(12, result.RecycledBytes); Assert.Single(bin.Calls);
    }

    [Fact] public void ScanHasAFileLimitIncludingFilesThatAreTooRecent()
    {
        File("one.tmp", 1, 1); File("two.tmp", 1, 1); File("three.tmp", 1, 1);
        var scan = new TemporaryCleaner(new FakeBin(), maximumFiles: 2).Scan(root);
        Assert.Equal(2, scan.ExaminedFiles); Assert.True(scan.Limited); Assert.Empty(scan.Files);
    }

    [Fact] public async Task JunctionsAreNeverFollowedIncludingRootAndReplacedAncestors()
    {
        if (!OperatingSystem.IsWindows()) return;
        File("source/old.tmp"); File("target/old.tmp");
        Directory.CreateDirectory(Path.Combine(root, "target/child"));
        var link = Junction("link", "target");
        var cleaner = new TemporaryCleaner(new FakeBin());
        Assert.Throws<IOException>(() => cleaner.Scan(link));
        Assert.Throws<IOException>(() => cleaner.Scan(Path.Combine(link, "child")));
        var scan = cleaner.Scan(Path.Combine(root, "source"));
        System.IO.File.Move(Path.Combine(root, "source/old.tmp"), Path.Combine(root, "saved.tmp"));
        Directory.Delete(Path.Combine(root, "source"));
        Junction("source", "target");
        var result = await cleaner.RecycleAsync(scan, scan.Files);
        Assert.Equal(0, result.Recycled); Assert.Equal(1, result.Skipped);
        Assert.True(System.IO.File.Exists(Path.Combine(root, "target/old.tmp")));
        var fullScan = cleaner.Scan(root);
        Assert.DoesNotContain(fullScan.Files, f => f.Path.StartsWith(link + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private string Junction(string linkName, string targetName)
    {
        var link = Path.GetFullPath(Path.Combine(root, linkName)); var target = Path.GetFullPath(Path.Combine(root, targetName));
        var prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        Assert.StartsWith(prefix, link); Assert.StartsWith(prefix, target);
        Directory.CreateDirectory(target);
        using var process = Process.Start(new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/d /c mklink /J \"{link}\" \"{target}\"", UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        })!;
        Assert.True(process.WaitForExit(10000));
        Assert.Equal(0, process.ExitCode);
        junctions.Add(link);
        return link;
    }

    public void Dispose()
    {
        var resolvedRoot = Path.GetFullPath(root);
        var tempPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(tempPrefix, resolvedRoot);
        Assert.StartsWith("Nexus-cleanup-tests-", Path.GetFileName(resolvedRoot));
        foreach (var junction in junctions) if (Directory.Exists(junction)) Directory.Delete(junction);
        if (Directory.Exists(resolvedRoot)) Directory.Delete(resolvedRoot, true);
    }
}

public sealed class RecycleIntegrationFactAttribute : FactAttribute
{
    public RecycleIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXUS_TEST_RECYCLE_INTEGRATION") != "1")
            Skip = "Opt-in Windows Shell test: only recycles a newly created file in this repository's artifacts folder.";
    }
}

public sealed class WindowsRecycleIntegrationTests
{
    [RecycleIntegrationFact]
    public async Task ShellConfirmsRecycleDestinationForNewDisposableWorkspaceFile()
    {
        Assert.True(OperatingSystem.IsWindows());
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !System.IO.File.Exists(Path.Combine(repository.FullName, "src/Nexus.Storage/Nexus.Storage.csproj")))
            repository = repository.Parent;
        Assert.NotNull(repository);
        var ownedRoot = Path.GetFullPath(Path.Combine(repository!.FullName, "artifacts", "recycle-validation", Guid.NewGuid().ToString("N")));
        Assert.StartsWith(repository.FullName + Path.DirectorySeparatorChar, ownedRoot);
        Directory.CreateDirectory(ownedRoot);
        var file = Path.Combine(ownedRoot, "nexus-disposable-recycle-test.txt");
        await System.IO.File.WriteAllTextAsync(file, "NEXUS test file; safe to restore or remove manually.");
        System.IO.File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
        System.IO.File.SetLastAccessTimeUtc(file, DateTime.UtcNow.AddDays(-8));
        var cleaner = new TemporaryCleaner(new WindowsRecycleBin());
        var scan = cleaner.Scan(ownedRoot);
        Assert.Single(scan.Files);
        var result = await cleaner.RecycleAsync(scan, scan.Files);
        Assert.Empty(result.Errors); Assert.Equal(1, result.Recycled); Assert.Equal(0, result.Skipped);
        Assert.False(result.Cancelled); Assert.False(System.IO.File.Exists(file));
        // Only an empty, newly created directory is removed. The recycled file stays in the Bin.
        Directory.Delete(ownedRoot);
    }
}
