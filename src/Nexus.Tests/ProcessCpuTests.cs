using Nexus.Hardware;
using System.Diagnostics;
using Xunit;

namespace Nexus.Tests;

public sealed class ProcessCpuTests
{
    [Fact] public void CpuIsNormalizedByElapsedTimeAndLogicalProcessors()
    {
        var a = new ProcessCpuObservation(1, 1, "app", TimeSpan.FromSeconds(2), 1000);
        var b = a with { CpuTime = TimeSpan.FromSeconds(3), Timestamp = 3000 };
        var usage = ProcessCpuProbe.Compare(a, b, 4, 1000);
        Assert.NotNull(usage); Assert.Equal(12.5, usage.Percent);
    }
    [Fact] public void ReusedPidRegressingCountersAndInvalidTimingAreUnavailable()
    {
        var a = new ProcessCpuObservation(1, 1, "app", TimeSpan.FromSeconds(2), 1000);
        Assert.Null(ProcessCpuProbe.Compare(a, a with { Started = 2, Timestamp = 3000 }, 4, 1000));
        Assert.Null(ProcessCpuProbe.Compare(a, a with { CpuTime = TimeSpan.Zero, Timestamp = 3000 }, 4, 1000));
        Assert.Null(ProcessCpuProbe.Compare(a, a, 4, 1000));
        Assert.Null(ProcessCpuProbe.Compare(a, a with { Timestamp = 1100 }, 4, 1000));
        Assert.Null(ProcessCpuProbe.Compare(a, a with { Timestamp = 3000 }, 0, 1000));
        Assert.Null(ProcessCpuProbe.Compare(a, a with { CpuTime = TimeSpan.FromSeconds(100), Timestamp = 3000 }, 4, 1000));
    }
    [Fact] public void NativeSnapshotIncludesThisAccessibleProcessWithoutChangingIt()
    {
        using var self = Process.GetCurrentProcess(); var sample = ProcessCpuProbe.Read();
        var own = Assert.Single(sample, x => x.Id == self.Id);
        Assert.Equal(self.StartTime.ToUniversalTime().Ticks, own.Started); Assert.True(own.CpuTime >= TimeSpan.Zero); Assert.True(own.Timestamp > 0);
    }
    [Fact] public async Task CancelledMeasurementProducesNoPartialResult()
    {
        using var c = new CancellationTokenSource(); c.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProcessCpuProbe.MeasureAsync(c.Token));
    }
}
