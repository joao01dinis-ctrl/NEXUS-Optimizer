using Nexus.Hardware;
using Nexus.Core;
using Nexus.Optimization;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Nexus.Tests;

public sealed class SmartBoostNativeTests
{
    [Fact]
    public async Task ReadOnlyAnalysisCollectsRealWindowsSamplesAndNeverWritesSettings()
    {
        var hardware = new WindowsHardwareService(); var info = hardware.Detect();
        var originals = SmartBoost.VisualKeys.ToDictionary(k => k, k => WindowsSettings.Resolve(k).Read());
        hardware.ReadSample(); var before = ProcessCpuProbe.Read(); var samples = new List<Sample>();
        for (var i = 0; i < 5; i++) { await Task.Delay(2000); samples.Add(hardware.ReadSample()); }
        var after = ProcessCpuProbe.Read(); var previous = before.ToDictionary(p => (p.Id, p.Started));
        var processes = after.Where(p => previous.ContainsKey((p.Id, p.Started)))
            .Select(p => ProcessCpuProbe.Compare(previous[(p.Id, p.Started)], p, Environment.ProcessorCount, Stopwatch.Frequency))
            .OfType<ProcessCpuUsage>().OrderByDescending(p => p.Percent).Take(10).Select(p => new BoostProcess(p.Name, p.Percent)).ToArray();
        var power = HardwareEnvironment.ReadPower();
        IReadOnlyList<string> drivers;
        try { drivers = HardwareEnvironment.GraphicsDrivers(); } catch { drivers = []; }
        var report = SmartBoost.Evaluate(new(BoostGoal.Balanced, samples, [], processes, power.OnAcPower, power.BatterySaver,
            info.Cpu, info.Gpu, System.Runtime.InteropServices.RuntimeInformation.OSDescription, drivers,
            originals.Select(s => new BoostSetting(s.Key, s.Key, "0", s.Value)).ToArray()));
        Assert.Equal(5, report.Input.Samples.Count); Assert.All(samples, s => Assert.True(s.TotalGb > 0));
        Assert.True(samples[^1].At - samples[0].At >= TimeSpan.FromSeconds(8));
        Assert.True(SmartBoost.IsFresh(report, DateTimeOffset.Now));
        Assert.All(originals, s => Assert.Equal(s.Value, WindowsSettings.Resolve(s.Key).Read()));
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "src/Nexus.UI/Nexus.UI.csproj"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var output = Path.Combine(repository!.FullName, "artifacts", "smart-boost-validation", "report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { Mode = "Read only; no setting write method called", Report = report }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
