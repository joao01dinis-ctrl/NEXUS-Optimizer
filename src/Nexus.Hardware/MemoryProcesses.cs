using System.ComponentModel;
using System.Diagnostics;

namespace Nexus.Hardware;

public sealed record MemoryProcess(int Id, string Name, double WorkingSetMb, double PrivateMb)
{
    public string Display => $"{Name} • PID {Id} • RAM {WorkingSetMb:F0} MB • privada {PrivateMb:F0} MB";
}

public static class MemoryProcesses
{
    /// <summary>Returns accessible processes in the current Windows session, without changing their memory.</summary>
    public static IReadOnlyList<MemoryProcess> Read()
    {
        using var current = Process.GetCurrentProcess();
        var session = current.SessionId;
        var result = new List<MemoryProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != session) continue;
                    process.Refresh();
                    result.Add(new(process.Id, process.ProcessName, process.WorkingSet64 / 1048576d,
                        process.PrivateMemorySize64 / 1048576d));
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
                { /* Exited or protected processes are unavailable in this snapshot. */ }
            }
        }
        return result.OrderByDescending(x => x.WorkingSetMb).ThenBy(x => x.Name).ToArray();
    }
}
