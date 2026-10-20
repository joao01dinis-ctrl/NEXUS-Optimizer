using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;

namespace Nexus.Diagnostics;

public enum AntivirusHealth { Unknown = -1, Good = 0, NotMonitored = 1, Poor = 2, Snoozed = 3 }
public enum ProtectionAction { UpdateSignatures, QuickScan, FullScan }

public sealed record DefenderSnapshot(string? Mode, bool? ServiceEnabled, bool? AntivirusEnabled,
    bool? RealTimeEnabled, bool? SignaturesOutOfDate, bool? TamperProtected, string? SignatureVersion,
    DateTimeOffset? SignaturesUpdated, DateTimeOffset? QuickScanEnded, DateTimeOffset? FullScanEnded)
{
    public bool Active => string.Equals(Mode, "Normal", StringComparison.OrdinalIgnoreCase) && ServiceEnabled == true && AntivirusEnabled == true;
    public static DefenderSnapshot Parse(string json, DateTimeOffset now)
    {
        if (json.Length > 65536) throw new JsonException("Defender response too large.");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 8 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a Defender status object.");
        string? Text(string key) => root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        bool? Flag(string key) => root.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
        DateTimeOffset? Date(string key)
        {
            var value = Text(key); if (value is null) return null;
            DateTimeOffset parsed;
            if (value.StartsWith("/Date(", StringComparison.Ordinal) && value.EndsWith(")/", StringComparison.Ordinal))
            {
                var number = value[6..^2];
                if (number.Length == 0) return null;
                // Windows PowerShell may append a timezone offset to its JSON date.
                var offset = number.IndexOfAny(['+', '-'], 1);
                if (offset >= 0) number = number[..offset];
                if (!long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds)) return null;
                try { parsed = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds); } catch (ArgumentOutOfRangeException) { return null; }
            }
            else if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed)) return null;
            return parsed.Year >= 2000 && parsed <= now.AddMinutes(5) ? parsed : null;
        }
        return new(Text("AMRunningMode"), Flag("AMServiceEnabled"), Flag("AntivirusEnabled"), Flag("RealTimeProtectionEnabled"),
            Flag("DefenderSignaturesOutOfDate"), Flag("IsTamperProtected"), Text("AntivirusSignatureVersion"),
            Date("AntivirusSignatureLastUpdated"), Date("QuickScanEndTime"), Date("FullScanEndTime"));
    }
}

public sealed record ProtectionStatus(DateTimeOffset CheckedAt, AntivirusHealth WindowsHealth, DefenderSnapshot? Defender,
    bool Administrator, string? ReadError)
{
    public bool CanRequestAction => Administrator && Defender?.Active == true;
    public string Summary => Defender is { Active: true, RealTimeEnabled: false } ? "O Defender indica proteção em tempo real desligada. Revê a Segurança do Windows." :
        Defender is { Active: true, SignaturesOutOfDate: true } ? "O Defender indica definições desatualizadas." : WindowsHealth switch
        {
            AntivirusHealth.Good => "O Windows comunica um bom estado do antivírus. Isto não é uma análise a malware.",
            AntivirusHealth.Poor => "O Windows comunica que o antivírus precisa de atenção.",
            AntivirusHealth.Snoozed => "O Windows comunica proteção antivírus em pausa.",
            AntivirusHealth.NotMonitored => "O estado do antivírus não está a ser monitorizado pelo Windows.",
            _ => "Não foi possível confirmar o estado geral do antivírus. Consulta a Segurança do Windows."
        };
}

public interface IProtectionBackend
{
    AntivirusHealth ReadWindowsHealth();
    bool IsAdministrator { get; }
    Task<DefenderSnapshot> ReadDefenderAsync(CancellationToken cancellation);
    Task ExecuteAsync(ProtectionAction action, CancellationToken cancellation);
}

public sealed class WindowsProtection(IProtectionBackend backend)
{
    private readonly SemaphoreSlim operation = new(1, 1);
    public WindowsProtection() : this(new WindowsDefenderBackend()) { }
    public async Task<ProtectionStatus> ReadAsync(CancellationToken cancellation = default)
    {
        var health = backend.ReadWindowsHealth();
        try { return new(DateTimeOffset.Now, health, await backend.ReadDefenderAsync(cancellation), backend.IsAdministrator, null); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or JsonException or System.ComponentModel.Win32Exception)
        { return new(DateTimeOffset.Now, health, null, backend.IsAdministrator, "A consulta do Defender está indisponível. Pode existir outro antivírus, uma política ou um componente desativado."); }
    }
    public async Task<ProtectionStatus> RequestAsync(ProtectionAction action, bool confirmed, CancellationToken cancellation = default)
    {
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        if (!confirmed) throw new InvalidOperationException("É necessária confirmação antes de pedir uma operação ao Defender.");
        if (!await operation.WaitAsync(0, cancellation)) throw new InvalidOperationException("Já existe um pedido ao Defender em curso nesta NEXUS.");
        try
        {
            // Re-read at action time; a previous green card does not authorize a changed provider.
            var current = await ReadAsync(cancellation);
            if (!current.Administrator) throw new InvalidOperationException("Para pedir uma análise ou atualização nesta página, abre a NEXUS como administrador. Também podes usar a Segurança do Windows.");
            if (current.Defender?.Active != true) throw new InvalidOperationException("O Defender não foi confirmado como antivírus ativo. Usa a Segurança do Windows para consultar o fornecedor atual.");
            await backend.ExecuteAsync(action, cancellation);
            return await ReadAsync(cancellation);
        }
        finally { operation.Release(); }
    }
}

public sealed class WindowsDefenderBackend : IProtectionBackend
{
    private const string ReadScript = "Get-MpComputerStatus -ErrorAction Stop | Select-Object AMRunningMode,AMServiceEnabled,AntivirusEnabled,RealTimeProtectionEnabled,DefenderSignaturesOutOfDate,IsTamperProtected,AntivirusSignatureVersion,AntivirusSignatureLastUpdated,QuickScanEndTime,FullScanEndTime | ConvertTo-Json -Compress";
    public bool IsAdministrator
    {
        get { using var identity = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }
    }
    public AntivirusHealth ReadWindowsHealth()
    {
        try { return WscGetSecurityProviderHealth(4, out var value) == 0 && Enum.IsDefined(typeof(AntivirusHealth), value) ? (AntivirusHealth)value : AntivirusHealth.Unknown; }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return AntivirusHealth.Unknown; }
    }
    [DllImport("wscapi.dll", ExactSpelling = true)]
    private static extern int WscGetSecurityProviderHealth(uint providers, out int health);
    public async Task<DefenderSnapshot> ReadDefenderAsync(CancellationToken cancellation) =>
        DefenderSnapshot.Parse(await PowerShellAsync(ReadScript, TimeSpan.FromSeconds(20), cancellation), DateTimeOffset.UtcNow);
    public async Task ExecuteAsync(ProtectionAction action, CancellationToken cancellation)
    {
        var script = action switch
        {
            ProtectionAction.UpdateSignatures => "Update-MpSignature -ErrorAction Stop",
            ProtectionAction.QuickScan => "Start-MpScan -ScanType QuickScan -ErrorAction Stop",
            ProtectionAction.FullScan => "Start-MpScan -ScanType FullScan -ErrorAction Stop",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        await PowerShellAsync(script, TimeSpan.FromHours(4), cancellation);
    }
    private static async Task<string> PowerShellAsync(string script, TimeSpan timeout, CancellationToken cancellation)
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment.Remove("OPENAI_API_KEY");
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; try { " + script + " } catch { Write-Output 'NEXUS_DEFENDER_REQUEST_FAILED'; exit 1 }" }) info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("Não foi possível contactar o Defender.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout);
        var output = ReadBoundedAsync(process.StandardOutput, deadline.Token);
        var errors = ReadBoundedAsync(process.StandardError, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var text = await output; await errors;
            if (process.ExitCode != 0) throw new InvalidOperationException("O Defender recusou o pedido. Consulta a Segurança do Windows e confirma os direitos e políticas deste PC.");
            return text;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new InvalidOperationException("A espera pelo Defender terminou. Uma análise já iniciada pode continuar no Windows; consulta o seu progresso na Segurança do Windows."); }
        finally
        {
            // Only our command host is stopped. Never stop WinDefend, MsMpEng or another security process.
            if (!process.HasExited) process.Kill(entireProcessTree: false);
            try { await Task.WhenAll(output, errors); } catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException) { }
        }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellation)
    {
        var buffer = new char[2048]; var text = new System.Text.StringBuilder();
        while (await reader.ReadAsync(buffer, cancellation) is var read && read > 0)
        {
            if (text.Length + read > 65536) throw new InvalidOperationException("A resposta do Defender excedeu o limite de leitura.");
            text.Append(buffer, 0, read);
        }
        return text.ToString();
    }
}
