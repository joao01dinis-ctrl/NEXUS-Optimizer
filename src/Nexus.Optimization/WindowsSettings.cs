using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Optimization;

public sealed record PowerPlan(Guid Id, string Name) { public override string ToString() => Name; }
public sealed record GameProcess(int Id, long Started, string Name)
{
    public string Key => $"priority:{Id}:{Started}";
    public override string ToString() => $"{Name} · {Id}";
}
public static class WindowsSettings
{
    public static ISystemSetting Resolve(string key) => key switch
    {
        "power" => new PowerSetting(), "animations" => new AnimationSetting(),
        UserAccessibility.StickyKeysKey => UserAccessibility.Resolve(),
        _ when UserPreferences.Contains(key) => UserPreferences.Resolve(key),
        _ when UserPolicies.Contains(key) => UserPolicies.Resolve(key),
        _ when key.StartsWith("dns:", StringComparison.Ordinal) => DnsSettings.Resolve(key),
        _ when key.StartsWith("priority:") => new PrioritySetting(key),
        _ => throw new ArgumentException("Definição desconhecida.")
    };
    public static IReadOnlyList<GameProcess> Games()
    {
        var list = new List<GameProcess>();
        using var self = Process.GetCurrentProcess();
        foreach (var p in Process.GetProcesses())
        {
            using (p) try
            {
                if (p.Id != self.Id && p.SessionId == self.SessionId && p.MainWindowHandle != 0 && p.PriorityClass == ProcessPriorityClass.Normal && PriorityEligible(p.ProcessName, p.MainModule?.FileName))
                    list.Add(new(p.Id, p.StartTime.ToUniversalTime().Ticks, p.ProcessName));
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException) { }
        }
        return list.OrderBy(x => x.Name).ToArray();
    }
    public static bool PriorityEligible(string name, string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable)) return false;
        var windows = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (executable.StartsWith(windows + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        string[] blocked = ["explorer", "dwm", "csrss", "winlogon", "sihost", "Nexus.UI", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "MsMpEng", "MsSense", "SecurityHealthService", "vgc", "vgtray", "RiotClientServices", "RiotClientUx", "steam", "steamwebhelper", "EpicGamesLauncher", "Battle.net", "EADesktop", "Origin"];
        if (blocked.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        string[] prefixes = ["EasyAntiCheat", "BEService", "BattlEye", "FACEIT", "EAanticheat"];
        return !prefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }
    public static IReadOnlyList<PowerPlan> Plans()
    {
        var plans = new List<PowerPlan>();
        for (uint i = 0; i < 100; i++)
        {
            uint size = 16; var data = new byte[16];
            var result = PowerEnumerate(0, 0, 0, 16, i, data, ref size);
            if (result == 259) break;
            Check(result); var id = new Guid(data); uint bytes = 0;
            PowerReadFriendlyName(0, ref id, 0, 0, null, ref bytes);
            var buffer = new byte[bytes];
            Check(PowerReadFriendlyName(0, ref id, 0, 0, buffer, ref bytes));
            plans.Add(new(id, Encoding.Unicode.GetString(buffer).TrimEnd('\0')));
        }
        return plans;
    }
    private static void Check(uint code) { if (code != 0) throw new Win32Exception((int)code); }
    private sealed class PowerSetting : ISystemSetting
    {
        public string Key => "power"; public string Name => "Plano de energia";
        public string Read()
        {
            Check(PowerGetActiveScheme(0, out var p));
            try { return Marshal.PtrToStructure<Guid>(p).ToString(); } finally { LocalFree(p); }
        }
        public void Write(string value)
        {
            var id = Guid.Parse(value);
            if (!Plans().Any(x => x.Id == id)) throw new InvalidOperationException("O plano já não existe neste PC.");
            Check(PowerSetActiveScheme(0, ref id));
        }
    }
    private sealed class AnimationSetting : ISystemSetting
    {
        public string Key => "animations"; public string Name => "Animações da interface Windows";
        public string Read() { int value = 0; if (!SystemParametersInfoGet(0x1042, 0, ref value, 0)) throw new Win32Exception(); return value == 0 ? "0" : "1"; }
        public void Write(string value)
        {
            if (value is not ("0" or "1")) throw new ArgumentException("Valor inválido.");
            if (!SystemParametersInfoSet(0x1043, 0, value == "1" ? 1 : 0, 3)) throw new Win32Exception();
        }
    }
    private sealed class PrioritySetting(string key) : ISystemSetting
    {
        public string Key => key; public string Name => "Prioridade temporária da aplicação";
        private Process? Open()
        {
            var parts = key.Split(':'); if (parts.Length != 3) throw new ArgumentException("Processo inválido.");
            Process p; try { p = Process.GetProcessById(int.Parse(parts[1])); } catch (ArgumentException) { return null; }
            try
            {
                _ = p.Handle;
                if (p.StartTime.ToUniversalTime().Ticks != long.Parse(parts[2])) { p.Dispose(); return null; }
                if (!PriorityEligible(p.ProcessName, p.MainModule?.FileName)) throw new InvalidOperationException("Componentes do Windows, segurança, anti-cheat e launchers não são elegíveis.");
                return p;
            }
            catch { p.Dispose(); throw; }
        }
        public string Read() { using var p = Open(); return p is null ? "ended" : p.PriorityClass.ToString(); }
        public void Write(string value)
        {
            if (value is not ("Normal" or "AboveNormal")) throw new ArgumentException("Prioridade não permitida.");
            using var p = Open() ?? throw new InvalidOperationException("A aplicação já terminou.");
            using var self = Process.GetCurrentProcess();
            if (p.SessionId != self.SessionId || p.Id == self.Id) throw new InvalidOperationException("Processo não elegível.");
            p.PriorityClass = Enum.Parse<ProcessPriorityClass>(value);
        }
    }
    [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(nint root, out nint guid);
    [DllImport("powrprof.dll")] private static extern uint PowerSetActiveScheme(nint root, ref Guid guid);
    [DllImport("powrprof.dll")] private static extern uint PowerEnumerate(nint root, nint scheme, nint subgroup, uint access, uint index, byte[] buffer, ref uint size);
    [DllImport("powrprof.dll")] private static extern uint PowerReadFriendlyName(nint root, ref Guid scheme, nint subgroup, nint setting, byte[]? buffer, ref uint size);
    [DllImport("kernel32.dll")] private static extern nint LocalFree(nint p);
    [DllImport("user32.dll", EntryPoint="SystemParametersInfoW", SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SystemParametersInfoGet(uint action, uint param, ref int value, uint flags);
    [DllImport("user32.dll", EntryPoint="SystemParametersInfoW", SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SystemParametersInfoSet(uint action, uint param, nint value, uint flags);
}
