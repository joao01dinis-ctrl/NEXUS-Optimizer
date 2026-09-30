using Nexus.Hardware;
using Xunit;

namespace Nexus.Tests;

public sealed class HardwareTests
{
    [Fact]
    public void DetectionReturnsRealHardwareOrExplicitWarnings()
    {
        var hardware = new WindowsHardwareService().Detect();
        Assert.False(string.IsNullOrWhiteSpace(hardware.Cpu));
        Assert.False(string.IsNullOrWhiteSpace(hardware.Gpu));
        Assert.True(hardware.RamGb > 0);
        Assert.NotEmpty(hardware.Disks);
    }

    [Fact]
    public async Task CpuBecomesAvailableAfterSecondSample()
    {
        var service = new WindowsHardwareService();
        Assert.Null(service.ReadSample().CpuPercent);
        await Task.Delay(250);
        var sample = service.ReadSample();
        Assert.NotNull(sample.CpuPercent);
        Assert.InRange(sample.CpuPercent.Value, 0, 100);
    }
}
