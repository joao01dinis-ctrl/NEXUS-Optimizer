using System.Management;
using System.Runtime.InteropServices;

namespace Nexus.Hardware;

public sealed record PowerStatus(bool? OnAcPower, bool? BatterySaver);
public static class HardwareEnvironment
{
    public static PowerStatus ReadPower()
    {
        if (!GetSystemPowerStatus(out var status)) return new(null, null);
        return new(status.AcLineStatus switch { 0 => false, 1 => true, _ => null },
            status.SystemStatusFlag switch { 0 => false, 1 => true, _ => null });
    }
    public static IReadOnlyList<string> GraphicsDrivers()
    {
        using var search = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController")
        { Options = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5), ReturnImmediately = false } };
        using var rows = search.Get();
        var result = new List<string>();
        foreach (var row in rows.Cast<ManagementBaseObject>())
            using (row) result.Add($"{row["Name"] ?? "GPU desconhecida"} · driver {row["DriverVersion"] ?? "indisponível"}");
        return result;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out NativePowerStatus status);
}
