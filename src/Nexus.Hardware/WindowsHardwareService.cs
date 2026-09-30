using System.Management;
using System.Runtime.InteropServices;
using Nexus.Core;
namespace Nexus.Hardware;

public sealed class WindowsHardwareService : IHardwareService
{
    private ulong lastIdle, lastTotal;
    public HardwareInfo Detect()
    {
        var warnings = new List<string>();
        string Read(string table, string field)
        {
            try
            {
                using var search = new ManagementObjectSearcher($"SELECT {field} FROM {table}");
                using var rows = search.Get();
                return string.Join(" • ", rows.Cast<ManagementBaseObject>().Select(x => { using (x) return x[field]?.ToString()?.Trim() ?? "Indisponível"; }));
            }
            catch (Exception e) { warnings.Add($"{table}: {e.Message}"); return "Indisponível"; }
        }
        var cpu = Read("Win32_Processor", "Name");
        var gpu = Read("Win32_VideoController", "Name");
        var disks = new List<string>();
        foreach (var d in DriveInfo.GetDrives())
            try
            {
                if (d.IsReady && d.DriveType == DriveType.Fixed)
                    disks.Add($"{d.Name}  •  {d.TotalSize / 1073741824d:F0} GB  •  {d.AvailableFreeSpace / 1073741824d:F0} GB livres");
            }
            catch (IOException e) { warnings.Add(e.Message); }
        var mem = Memory();
        return new(cpu, gpu, mem.TotalPhys / 1073741824d, disks, warnings);
    }
    public Sample ReadSample()
    {
        double? cpu = null;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var total = kernel + user;
            if (lastTotal > 0 && total > lastTotal)
                cpu = Math.Clamp(100d * (1d - (double)(idle - lastIdle) / (total - lastTotal)), 0, 100);
            lastTotal = total;
            lastIdle = idle;
        }
        var m = Memory();
        return new(cpu, m.MemoryLoad, (m.TotalPhys - m.AvailPhys) / 1073741824d, m.TotalPhys / 1073741824d, DateTimeOffset.Now);
    }
    private static MemoryStatus Memory()
    {
        var m = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref m))
            throw new System.ComponentModel.Win32Exception();
        return m;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
}
