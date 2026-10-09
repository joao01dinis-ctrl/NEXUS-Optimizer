using Microsoft.Win32;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text.Json;

namespace Nexus.Optimization;

public sealed record DnsAdapter(Guid Id, string Name) { public string Key => "dns:" + Id; public override string ToString() => Name; }
public sealed record DnsConfiguration(bool Automatic, string[] Servers);
public interface IDnsConfigurationStore
{
    DnsConfiguration Read(Guid id);
    void Write(Guid id, DnsConfiguration config);
}
public static class DnsSettings
{
    public static string State(bool automatic, params string[] addresses)
    {
        if (automatic && addresses.Length != 0 || !automatic && addresses.Length is < 1 or > 4)
            throw new ArgumentException("Configuração DNS inválida.");
        var ips = addresses.Select(a => IPAddress.TryParse(a, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any)
            ? ip.ToString() : throw new ArgumentException("Escolhe endereços DNS IPv4 válidos.")).ToArray();
        return JsonSerializer.Serialize(new DnsConfiguration(automatic, ips));
    }
    public static IReadOnlyList<DnsAdapter> Adapters() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(a => a.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211 && Guid.TryParse(a.Id, out _))
        .Where(a => !new[] { "virtual", "vpn", "tap", "tun", "hyper-v" }.Any(v => a.Description.Contains(v, StringComparison.OrdinalIgnoreCase)))
        .Select(a => new DnsAdapter(Guid.Parse(a.Id), a.Name)).OrderBy(a => a.Name).ToArray();
    public static ISystemSetting Resolve(string key, IDnsConfigurationStore? store = null)
    {
        if (!key.StartsWith("dns:", StringComparison.Ordinal) || !Guid.TryParse(key[4..], out var id)) throw new ArgumentException("Adaptador inválido.");
        return new Setting(id, store ?? new WindowsStore());
    }
    private sealed class Setting(Guid id, IDnsConfigurationStore store) : ISystemSetting
    {
        public string Key => "dns:" + id;
        public string Name => "DNS IPv4 do adaptador " + id;
        public string Read() { var c = store.Read(id); return State(c.Automatic, c.Servers); }
        public void Write(string value)
        {
            var c = JsonSerializer.Deserialize<DnsConfiguration>(value) ?? throw new ArgumentException("Estado inválido.");
            if (value != State(c.Automatic, c.Servers)) throw new ArgumentException("Estado DNS não canónico.");
            store.Write(id, c);
        }
    }
    public static void RequireAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new UnauthorizedAccessException("Esta ação requer iniciar o NEXUS como administrador. Não foi alterada a ligação.");
    }
    private static ManagementObject Open(Guid id)
    {
        if (!Adapters().Any(a => a.Id == id)) throw new InvalidOperationException("O adaptador físico já não está disponível.");
        using var search = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE SettingID='{" + id + "}' AND IPEnabled=True");
        using var items = search.Get();
        foreach (ManagementObject item in items) return item;
        throw new InvalidOperationException("Não foi encontrada uma configuração IPv4 ativa para o adaptador.");
    }
    private sealed class WindowsStore : IDnsConfigurationStore
    {
        public DnsConfiguration Read(Guid id)
        {
            using var adapter = Open(id);
            using var policy = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient");
            if (policy?.GetValue("NameServer") is not null) throw new InvalidOperationException("O DNS é gerido por uma política do computador.");
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{" + id + "}");
            if (k is null) throw new InvalidOperationException("Não é possível preservar a configuração anterior.");
            var raw = k.GetValue("NameServer") as string ?? "";
            var addresses = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            _ = State(addresses.Length == 0, addresses);
            return new(addresses.Length == 0, addresses);
        }
        public void Write(Guid id, DnsConfiguration c)
        {
            RequireAdministrator();
            _ = Read(id); // Refuse managed or incompatible configurations before calling WMI.
            using var adapter = Open(id);
            using var args = adapter.GetMethodParameters("SetDNSServerSearchOrder");
            args["DNSServerSearchOrder"] = c.Automatic ? null : c.Servers;
            using var result = adapter.InvokeMethod("SetDNSServerSearchOrder", args, new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(10) });
            var code = Convert.ToUInt32(result["ReturnValue"]);
            if (code != 0) throw new InvalidOperationException(code == 1 ? "O Windows pede reinício. O histórico mantém a alteração para verificação/reposição." : "O Windows recusou a configuração DNS (" + code + ").");
        }
    }
    public static void Renew(Guid id)
    {
        RequireAdministrator(); using var adapter = Open(id);
        if (adapter["DHCPEnabled"] is not true) throw new InvalidOperationException("O adaptador não usa DHCP para IPv4. Não foi renovado.");
        using var result = adapter.InvokeMethod("RenewDHCPLease", null, new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(15) });
        if (Convert.ToUInt32(result["ReturnValue"]) != 0) throw new InvalidOperationException("O Windows não confirmou a renovação DHCP.");
    }
    public static async Task FlushAsync()
    {
        RequireAdministrator();
        using var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), "/flushdns")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new IOException("Não foi possível iniciar a ferramenta do Windows.");
        var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(); await output; await error;
        if (p.ExitCode != 0) throw new IOException("O Windows recusou a limpeza da cache DNS.");
    }
}
