using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nexus.Benchmark;

public sealed record BenchmarkResult(DateTimeOffset At, string Method, string Environment,
    double CpuMegabytesPerSecond, double CopyMegabytesPerSecond, double CpuSeconds, double CopySeconds);

public sealed record BenchmarkComparison(double CpuPercent, double CopyPercent);

public sealed class LocalBenchmark
{
    public const string Method = "sha256-1MiB-copy-8MiB-v1";
    public Task<BenchmarkResult> RunAsync(string cpuName, CancellationToken cancellationToken = default,
        TimeSpan? sampleTime = null)
    {
        var time = sampleTime ?? TimeSpan.FromSeconds(2);
        if (time < TimeSpan.FromMilliseconds(50) || time > TimeSpan.FromSeconds(5)) throw new ArgumentOutOfRangeException(nameof(sampleTime));
        ArgumentException.ThrowIfNullOrWhiteSpace(cpuName);
        return Task.Run(() => Run(cpuName, time, cancellationToken), cancellationToken);
    }
    private static BenchmarkResult Run(string cpuName, TimeSpan time, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var cpu = new byte[1024 * 1024]; var source = new byte[8 * 1024 * 1024]; var destination = new byte[source.Length];
        Random.Shared.NextBytes(cpu); Random.Shared.NextBytes(source);
        Span<byte> digest = stackalloc byte[32];
        for (var i = 0; i < 4; i++) { token.ThrowIfCancellationRequested(); SHA256.HashData(cpu, digest); }
        var (cpuRate, cpuSeconds) = Measure(time, cpu.Length, () => SHA256.HashData(cpu), token);
        for (var i = 0; i < 4; i++) { token.ThrowIfCancellationRequested(); Array.Copy(source, destination, source.Length); }
        var (copyRate, copySeconds) = Measure(time, source.Length, () => Array.Copy(source, destination, source.Length), token);
        GC.KeepAlive(destination);
        return new(DateTimeOffset.Now, Method,
            $"{cpuName} | {System.Environment.ProcessorCount} CPU lógicas | {RuntimeInformation.ProcessArchitecture} | {RuntimeInformation.FrameworkDescription} | {System.Environment.OSVersion}",
            cpuRate, copyRate, cpuSeconds, copySeconds);
    }
    private static (double Rate, double Seconds) Measure(TimeSpan time, int bytes, Action action, CancellationToken token)
    {
        long count = 0; var watch = Stopwatch.StartNew();
        do { token.ThrowIfCancellationRequested(); action(); count++; } while (watch.Elapsed < time);
        watch.Stop(); var seconds = watch.Elapsed.TotalSeconds;
        return (count * (double)bytes / 1048576d / seconds, seconds);
    }
    public static BenchmarkComparison? Compare(BenchmarkResult current, BenchmarkResult previous)
    {
        if (current.Method != previous.Method || current.Environment != previous.Environment ||
            !Valid(current) || !Valid(previous)) return null;
        return new((current.CpuMegabytesPerSecond / previous.CpuMegabytesPerSecond - 1) * 100,
            (current.CopyMegabytesPerSecond / previous.CopyMegabytesPerSecond - 1) * 100);
    }
    public static bool Valid(BenchmarkResult r) => r.Method == Method && !string.IsNullOrWhiteSpace(r.Environment) &&
        new[] { r.CpuMegabytesPerSecond, r.CopyMegabytesPerSecond, r.CpuSeconds, r.CopySeconds }.All(x => double.IsFinite(x) && x > 0);
}
