using Nexus.Core;
namespace Nexus.Service;
// Runs in the UI process, without elevation or installing a Windows service.
public sealed class MonitoringService(IHardwareService hardware)
{
    public Task<Sample> SampleAsync(CancellationToken cancellationToken = default) => Task.Run(hardware.ReadSample, cancellationToken);
}
