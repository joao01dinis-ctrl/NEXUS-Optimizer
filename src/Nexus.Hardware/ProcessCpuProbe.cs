using System.ComponentModel;
using System.Diagnostics;

namespace Nexus.Hardware;

public sealed record ProcessCpuObservation(int Id, long Started, string Name, TimeSpan CpuTime, long Timestamp);
public sealed record ProcessCpuUsage(int Id, long Started, string Name, double Percent)
{
    public string Display => $"{Name} · PID {Id} · CPU {Percent:F2}% da capacidade total";
}
public static class ProcessCpuProbe
{
    public static IReadOnlyList<ProcessCpuObservation> Read(CancellationToken token = default)
    {
        using var self = Process.GetCurrentProcess();
        var session = self.SessionId; var observations = new List<ProcessCpuObservation>(); var inspected = 0;
        var processes = Process.GetProcesses();
        try
        {
            foreach (var process in processes)
            {
                token.ThrowIfCancellationRequested();
                if (++inspected > 512) break;
                try
                {
                    if (process.SessionId != session) continue;
                    var started = process.StartTime.ToUniversalTime().Ticks;
                    var before = Stopwatch.GetTimestamp(); var cpu = process.TotalProcessorTime; var after = Stopwatch.GetTimestamp();
                    observations.Add(new(process.Id, started, process.ProcessName, cpu, before + (after - before) / 2));
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
        finally { foreach (var process in processes) process.Dispose(); }
        return observations;
    }
    public static ProcessCpuUsage? Compare(ProcessCpuObservation before, ProcessCpuObservation after, int logicalProcessors, long frequency)
    {
        if (before.Id != after.Id || before.Started != after.Started || before.Name != after.Name ||
            logicalProcessors <= 0 || frequency <= 0 || before.CpuTime < TimeSpan.Zero || after.CpuTime < before.CpuTime || after.Timestamp <= before.Timestamp)
            return null;
        var seconds = (after.Timestamp - (double)before.Timestamp) / frequency;
        if (seconds < .2) return null;
        var percent = (after.CpuTime - before.CpuTime).TotalSeconds / seconds / logicalProcessors * 100;
        if (!double.IsFinite(percent) || percent is < 0 or > 100.1) return null;
        return new(after.Id, after.Started, after.Name, Math.Min(percent, 100));
    }
    public static async Task<IReadOnlyList<ProcessCpuUsage>> MeasureAsync(CancellationToken token = default)
    {
        var before = await Task.Run(() => Read(token), token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        var after = await Task.Run(() => Read(token), token);
        var old = before.ToDictionary(x => (x.Id, x.Started));
        return after.Where(x => old.ContainsKey((x.Id, x.Started)))
            .Select(x => Compare(old[(x.Id, x.Started)], x, Environment.ProcessorCount, Stopwatch.Frequency))
            .OfType<ProcessCpuUsage>().OrderByDescending(x => x.Percent).Take(60).ToArray();
    }
}
