using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Nexus.Network;

public sealed class InstalledWarpClient : ICloudflareWarpClient
{
    public static readonly Uri SetupPage = new("https://developers.cloudflare.com/warp-client/get-started/windows/");
    public static readonly Uri TermsPage = new("https://www.cloudflare.com/application/terms/");
    public static readonly Uri PrivacyPage = new("https://www.cloudflare.com/application/privacypolicy/");
    private string? cachedHelp, cachedFingerprint;
    private sealed record Installation(bool Present, string? Cli, string? Gui, string? Version);

    private static Installation FindInstallation()
    {
        var present = false;
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var folder = Path.Combine(root, "Cloudflare", "Cloudflare WARP");
            var cli = Path.Combine(folder, "warp-cli.exe"); var gui = Path.Combine(folder, "Cloudflare WARP.exe"); var service = Path.Combine(folder, "warp-svc.exe");
            if (!File.Exists(cli) && !File.Exists(gui) && !File.Exists(service)) continue;
            present = true;
            // Never resolve PATH, a user-selected executable, junctions, or an untrusted service.
            if (File.Exists(service) && !CloudflareFileTrust.Verify(service, folder)) continue;
            var trustedCli = CloudflareFileTrust.Verify(cli, folder) ? cli : null;
            var trustedGui = CloudflareFileTrust.Verify(gui, folder) ? gui : null;
            if (trustedCli is not null || trustedGui is not null)
                return new(true, trustedCli, trustedGui, FileVersionInfo.GetVersionInfo(trustedCli ?? trustedGui!).FileVersion);
        }
        return new(present, null, null, null);
    }

    public async Task<WarpObservation> InspectAsync(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        Installation install;
        try { install = await Task.Run(FindInstallation, cancellation); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        { return new(true, false, false, false, VpnConnectionState.Error, WarpTrafficMode.Unknown, Error: "A instalação não pôde ser verificada."); }
        if (!install.Present) return new(false, false, false, false, VpnConnectionState.Unavailable, WarpTrafficMode.Unknown);
        if (install.Cli is null) return new(true, install.Gui is not null, install.Gui is not null, false, VpnConnectionState.Unknown, WarpTrafficMode.Unknown, Version: install.Version);
        try
        {
            var file = new FileInfo(install.Cli); var fingerprint = install.Cli + ":" + file.Length + ":" + file.LastWriteTimeUtc.Ticks;
            if (cachedHelp is null || cachedFingerprint != fingerprint)
            { cachedHelp = await RunAsync(install.Cli, "--help", cancellation); cachedFingerprint = fingerprint; }
            if (!WarpOutput.HasRequiredCommands(cachedHelp))
                return new(true, true, install.Gui is not null, false, VpnConnectionState.Unknown, WarpTrafficMode.Unknown, Version: install.Version);
            var status = await RunAsync(install.Cli, "status", cancellation);
            if (WarpOutput.NeedsSetup(status)) return new(true, true, install.Gui is not null, true, VpnConnectionState.Unknown, WarpTrafficMode.Unknown, SetupRequired: true, Version: install.Version);
            var settings = await RunAsync(install.Cli, "settings", cancellation);
            return new(true, true, install.Gui is not null, true, WarpOutput.State(status), WarpOutput.Mode(settings),
                WarpOutput.OrganizationManaged(settings), WarpOutput.NeedsSetup(settings), install.Version);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { return new(true, true, install.Gui is not null, false, VpnConnectionState.Error, WarpTrafficMode.Unknown, Version: install.Version,
            Error: "O cliente não respondeu à consulta. Abre o WARP para concluir a configuração ou verificar o serviço."); }
    }

    public async Task RequestAsync(bool connect, CancellationToken cancellation)
    {
        var install = await Task.Run(FindInstallation, cancellation);
        if (install.Cli is null) throw new InvalidOperationException("O cliente oficial WARP deixou de estar disponível ou de ser validado.");
        var result = await RunAsync(install.Cli, connect ? "connect" : "disconnect", cancellation);
        if (WarpOutput.NeedsSetup(result)) throw new InvalidOperationException("Conclui a configuração e a aceitação dos termos na aplicação oficial WARP.");
    }

    public async Task OpenOfficialClientAsync(CancellationToken cancellation = default)
    {
        var install = await Task.Run(FindInstallation, cancellation);
        if (install.Gui is null) throw new InvalidOperationException("A aplicação oficial WARP não foi encontrada com uma assinatura válida.");
        var info = new ProcessStartInfo(install.Gui) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(install.Gui)!, CreateNoWindow = true };
        info.Environment.Remove("OPENAI_API_KEY");
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Não foi possível abrir o WARP.");
    }

    private static async Task<string> RunAsync(string executable, string command, CancellationToken cancellation)
    {
        if (command is not ("--help" or "status" or "settings" or "connect" or "disconnect")) throw new ArgumentException("Comando WARP não permitido.");
        if (!await Task.Run(() => CloudflareFileTrust.Verify(executable, Path.GetDirectoryName(executable)!), cancellation))
            throw new InvalidOperationException("A assinatura do WARP não pôde ser validada.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment.Remove("OPENAI_API_KEY"); info.ArgumentList.Add(command);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("Não foi possível contactar o WARP.");
        // EOF prevents accepting first-run terms or answering an unexpected prompt.
        process.StandardInput.Close();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(8));
        var output = ReadBoundedAsync(process.StandardOutput, deadline.Token); var errors = ReadBoundedAsync(process.StandardError, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var text = await output; var errorText = await errors;
            if (WarpOutput.RequiresInteractiveSetup(command, text) || WarpOutput.RequiresInteractiveSetup(command, errorText)) return "Missing Registration; complete setup in the official application.";
            if (process.ExitCode != 0) throw new InvalidOperationException("O WARP recusou o comando. Consulta a aplicação oficial.");
            return text;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new InvalidOperationException("A consulta ao WARP excedeu o tempo previsto."); }
        finally
        {
            // Only our CLI process is stopped. Never stop warp-svc or reset network settings.
            if (!process.HasExited) process.Kill(entireProcessTree: false);
            try { await Task.WhenAll(output, errors); } catch (Exception error) when (error is IOException or OperationCanceledException or InvalidDataException) { }
        }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellation)
    {
        var text = new StringBuilder(); var buffer = new char[2048];
        while (await reader.ReadAsync(buffer, cancellation) is var count && count > 0)
        { if (text.Length + count > 65536) throw new InvalidDataException("A resposta do WARP excedeu o limite."); text.Append(buffer, 0, count); }
        return text.ToString();
    }
}

public static class CloudflareFileTrust
{
    public static bool Verify(string path, string installationDirectory)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(path)) return false;
        try
        {
            var full = Path.GetFullPath(path); var folder = Path.GetFullPath(installationDirectory);
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Path.GetFullPath(Path.Combine(x, "Cloudflare", "Cloudflare WARP")));
            if (!roots.Contains(folder, StringComparer.OrdinalIgnoreCase) || !string.Equals(Path.GetDirectoryName(full), folder, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(full) is not ("warp-cli.exe" or "warp-svc.exe" or "Cloudflare WARP.exe")) return false;
            for (var current = new DirectoryInfo(folder); current is not null; current = current.Parent)
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            return (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0 && new FileInfo(full).Length <= 512L * 1024 * 1024 &&
                ReadVerifiedPublisher(full) is "Cloudflare, Inc." or "Cloudflare, Inc" or "Cloudflare Inc." or "Cloudflare Inc";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException or System.ComponentModel.Win32Exception or EntryPointNotFoundException or DllNotFoundException) { return false; }
    }

    internal static string? ReadVerifiedPublisher(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        Marshal.StructureToPtr(file, pointer, false);
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1, File = pointer, StateAction = 1,
            ProviderFlags = 0x1000 }; // Cached chain retrieval only; no unexpected network probe during a local query.
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try
        {
            if (WinVerifyTrust(new nint(-1), ref action, ref data) != 0 || data.StateData == 0) return null;
            // Read the signer actually verified by WinTrust, rather than merely any embedded certificate.
            var provider = WTHelperProvDataFromStateData(data.StateData);
            var signer = provider == 0 ? 0 : WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            var certPointer = signer == 0 ? 0 : WTHelperGetProvCertFromChain(signer, 0);
            if (certPointer == 0) return null;
            var contextPointer = Marshal.PtrToStructure<ProviderCertificateHead>(certPointer).Certificate;
            if (contextPointer == 0) return null;
            var context = Marshal.PtrToStructure<CertificateContextHead>(contextPointer);
            if (context.Encoded == 0 || context.Bytes is 0 or > 65536) return null;
            var encoded = new byte[context.Bytes]; Marshal.Copy(context.Encoded, encoded, 0, encoded.Length);
            using var certificate = X509CertificateLoader.LoadCertificate(encoded);
            return certificate.GetNameInfo(X509NameType.SimpleName, false);
        }
        finally
        {
            data.StateAction = 2; WinVerifyTrust(new nint(-1), ref action, ref data);
            Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer);
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public nint Handle, KnownSubject; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustData
    {
        public uint Size; public nint Policy, Sip; public uint UiChoice, Revocation, UnionChoice; public nint File;
        public uint StateAction; public nint StateData; public nint Url; public uint ProviderFlags, UiContext; public nint SignatureSettings;
    }
    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WinVerifyTrust(nint window, ref Guid action, ref TrustData data);
    [StructLayout(LayoutKind.Sequential)] private struct ProviderCertificateHead { public uint Size; public nint Certificate; }
    [StructLayout(LayoutKind.Sequential)] private struct CertificateContextHead { public uint Encoding; public nint Encoded; public uint Bytes; }
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern nint WTHelperProvDataFromStateData(nint stateData);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern nint WTHelperGetProvSignerFromChain(nint provider, uint signer, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterSignerIndex);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern nint WTHelperGetProvCertFromChain(nint signer, uint certificateIndex);
}
