using Nexus.Core;
using Nexus.Optimization;
using Xunit;

namespace Nexus.Tests;

public sealed class SmartBoostTests
{
    private static BoostInput Input(BoostGoal goal = BoostGoal.Balanced, double cpu = 20, double ram = 40)
    {
        var start = DateTimeOffset.Now.AddSeconds(-10);
        return new(goal, Enumerable.Range(0, 5).Select(i => new Sample(cpu, ram, ram / 100 * 32, 32, start.AddSeconds(i * 2))).ToArray(),
            [new("C:", 1000, 500)], [], true, false, "CPU", "GPU", "Windows", ["test driver"],
            SmartBoost.VisualKeys.Select(k => new BoostSetting(k, k, "0", "1")).ToArray());
    }
    [Fact] public void BalancedIdleDoesNotPreselectWindowsChanges()
    {
        var report = SmartBoost.Evaluate(Input());
        Assert.False(report.SustainedCpuPressure); Assert.False(report.SustainedMemoryPressure);
        Assert.All(report.Rules.Where(r => r.SettingKey is not null), r => Assert.Equal(BoostDecision.Ignore, r.Decision));
        Assert.Equal("1.0.0", report.RuleVersion); Assert.Equal(12, report.Rules.Count);
    }
    [Theory][InlineData(90, 40)][InlineData(20, 90)]
    public void SustainedPressureRecommendsOnlyReadableDifferentVisualPreferences(double cpu, double ram)
    {
        var input = Input(cpu: cpu, ram: ram);
        var report = SmartBoost.Evaluate(input with { Settings = input.Settings.Select(s => s.Key == "menu-animation" ? s with { Current = "0" } : s.Key == "tooltip-animation" ? s with { Error = "denied" } : s).ToArray() });
        Assert.Equal(3, report.Rules.Count(r => r.Decision == BoostDecision.Apply));
        Assert.All(report.Rules.Where(r => r.Decision == BoostDecision.Apply), r => Assert.Contains(r.SettingKey!, SmartBoost.VisualKeys));
        Assert.Equal(BoostDecision.Ignore, report.Rules.Single(r => r.SettingKey == "menu-animation").Decision);
        Assert.Equal(BoostDecision.Ignore, report.Rules.Single(r => r.SettingKey == "tooltip-animation").Decision);
    }
    [Fact] public void SingleSpikeNeverBecomesSustainedPressure()
    {
        var input = Input(); var samples = input.Samples.ToArray(); samples[2] = samples[2] with { CpuPercent = 100, MemoryPercent = 95 };
        Assert.DoesNotContain(SmartBoost.Evaluate(input with { Samples = samples }).Rules, r => r.Decision == BoostDecision.Apply);
    }
    [Fact] public void CompetitiveChoicesStayUnselectedWithoutPressure()
    {
        var report = SmartBoost.Evaluate(Input(BoostGoal.Competitive));
        Assert.All(report.Rules.Where(r => r.SettingKey is not null), r => Assert.Equal(BoostDecision.ManualTest, r.Decision));
        Assert.DoesNotContain(report.Rules, r => r.Decision == BoostDecision.Apply);
    }
    [Fact] public void StreamingKeepsPriorityAndDoesNotTargetRecordingOrAudio()
    {
        var report = SmartBoost.Evaluate(Input(BoostGoal.Streaming) with { Processes = [new("obs64", 25), new("Discord", 10)] });
        Assert.Equal(BoostDecision.Ignore, report.Rules.Single(r => r.Id == "nexus.game.priority").Decision);
        Assert.All(GameLibraryProfiles(), priority => Assert.False(priority));
    }
    private static IEnumerable<bool> GameLibraryProfiles() => Nexus.Gaming.GameLibrary.Profiles.Where(p => p.Name != "Personalizado").Select(p => p.Priority);
    [Theory][InlineData(0)][InlineData(1)][InlineData(4)]
    public void IncompleteObservationNeverRecommendsWrites(int count)
    {
        var input = Input(cpu: 100, ram: 100);
        Assert.DoesNotContain(SmartBoost.Evaluate(input with { Samples = input.Samples.Take(count).ToArray() }).Rules, r => r.Decision == BoostDecision.Apply);
    }
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(-1)][InlineData(101)]
    public void InvalidPercentagesCannotDriveRecommendations(double value)
    {
        var input = Input(cpu: 100, ram: 100); var samples = input.Samples.ToArray(); samples[1] = samples[1] with { CpuPercent = value };
        Assert.DoesNotContain(SmartBoost.Evaluate(input with { Samples = samples }).Rules, r => r.Decision == BoostDecision.Apply);
    }
    [Fact] public void UnknownCpuIsNotInterpretedAsAnIdleOrLoadedMeasurement()
    {
        var input = Input(cpu: 100); var samples = input.Samples.ToArray(); samples[1] = samples[1] with { CpuPercent = null };
        var report = SmartBoost.Evaluate(input with { Samples = samples });
        Assert.False(report.SustainedCpuPressure); Assert.DoesNotContain(report.Rules, r => r.Decision == BoostDecision.Apply);
    }
    [Fact] public void UnspacedAndOutOfOrderSamplesAreRejected()
    {
        var input = Input(cpu: 100, ram: 100);
        Assert.DoesNotContain(SmartBoost.Evaluate(input with { Samples = input.Samples.Select(s => s with { At = input.Samples[0].At }).ToArray() }).Rules, r => r.Decision == BoostDecision.Apply);
        Assert.DoesNotContain(SmartBoost.Evaluate(input with { Samples = input.Samples.Reverse().ToArray() }).Rules, r => r.Decision == BoostDecision.Apply);
    }
    [Theory][InlineData(false, false)][InlineData(null, false)][InlineData(true, true)][InlineData(true, null)]
    public void BatteryUnknownSupplyAndSaverNeverRecommendPowerChanges(bool? ac, bool? saver)
    {
        var report = SmartBoost.Evaluate(Input() with { OnAcPower = ac, BatterySaver = saver });
        Assert.Equal(BoostDecision.Ignore, report.Rules.Single(r => r.Id == "nexus.power.session").Decision);
    }
    [Fact] public void LowSpacePromptsManualReviewWithoutRecyclingFiles()
    {
        var report = SmartBoost.Evaluate(Input() with { Volumes = [new("C:", 1000, 100), new("invalid", 0, -1)] });
        Assert.Equal(BoostDecision.ManualTest, report.Rules.Single(r => r.Id == "nexus.storage.space").Decision);
        Assert.DoesNotContain(report.Rules, r => r.Decision == BoostDecision.Apply);
    }
    [Fact] public void OldOrFutureReportsCannotBeApplied()
    {
        var report = SmartBoost.Evaluate(Input());
        Assert.True(SmartBoost.IsFresh(report, report.At.AddMinutes(1)));
        Assert.False(SmartBoost.IsFresh(report, report.At.AddMinutes(6)));
        Assert.False(SmartBoost.IsFresh(report, report.At.AddSeconds(-1)));
        Assert.False(SmartBoost.IsFresh(null, DateTimeOffset.Now));
    }
}
