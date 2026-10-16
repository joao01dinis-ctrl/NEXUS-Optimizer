using Nexus.Network;
using Xunit;

namespace Nexus.Tests;

public sealed class VpnMeasurementTests
{
    private sealed class Probe : IVpnProbe
    {
        public int Calls;
        public VpnProbeSample Sample = new(DateTimeOffset.UtcNow, true, WarpTraceState.On, 12);
        public Task<VpnProbeSample> MeasureAsync(CancellationToken cancellation) { cancellation.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(Sample); }
    }
    [Fact] public async Task ExternalMeasurementsRequireConsentAndRespectCancellation()
    {
        var probe = new Probe(); var measurements = new VpnMeasurements(probe);
        await Assert.ThrowsAsync<InvalidOperationException>(() => measurements.SampleAsync(false)); Assert.Equal(0, probe.Calls);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => measurements.SampleAsync(true, cancellation.Token)); Assert.Equal(0, probe.Calls);
        Assert.Empty(measurements.Samples);
    }
    [Fact] public async Task KeepsAtMostTwelveSamplesAndDoesNotInventFailedLatency()
    {
        var probe = new Probe { Sample = new(DateTimeOffset.UtcNow, false, WarpTraceState.Unknown, null) };
        var measurements = new VpnMeasurements(probe);
        for (var i = 0; i < 14; i++) await measurements.SampleAsync(true);
        Assert.Equal(12, measurements.Samples.Count); Assert.Contains("0/12", measurements.Summary);
        Assert.Contains("Sem latência", measurements.Summary); Assert.DoesNotContain("0,0 ms", measurements.Summary);
        probe.Sample = probe.Sample with { IcmpMilliseconds = -1 };
        Assert.Null((await measurements.SampleAsync(true)).IcmpMilliseconds);
        measurements.Clear(); Assert.Empty(measurements.Samples);
    }
    [Fact] public async Task JitterDoesNotBridgeMissingSamples()
    {
        var probe = new Probe(); var measurements = new VpnMeasurements(probe);
        await measurements.SampleAsync(true);
        probe.Sample = probe.Sample with { IcmpMilliseconds = null }; await measurements.SampleAsync(true);
        probe.Sample = probe.Sample with { IcmpMilliseconds = 42 }; await measurements.SampleAsync(true);
        Assert.Contains("2/3", measurements.Summary); Assert.Contains("sem amostras suficientes", measurements.Summary);
    }
    [Theory]
    [InlineData(VpnConnectionState.Connected, WarpTrafficMode.TrafficAndDns, WarpTraceState.On, 0, true)]
    [InlineData(VpnConnectionState.Connected, WarpTrafficMode.TrafficOnly, WarpTraceState.On, 0, true)]
    [InlineData(VpnConnectionState.Connected, WarpTrafficMode.DnsOnly, WarpTraceState.On, 0, false)]
    [InlineData(VpnConnectionState.Connected, WarpTrafficMode.LocalProxy, WarpTraceState.On, 0, false)]
    [InlineData(VpnConnectionState.Disconnected, WarpTrafficMode.TrafficAndDns, WarpTraceState.On, 0, false)]
    [InlineData(VpnConnectionState.Error, WarpTrafficMode.TrafficAndDns, WarpTraceState.On, 0, false)]
    [InlineData(VpnConnectionState.Connected, WarpTrafficMode.TrafficAndDns, WarpTraceState.Off, 0, false)]
    [InlineData(VpnConnectionState.Connected, WarpTrafficMode.TrafficAndDns, WarpTraceState.Unknown, 0, false)]
    [InlineData(VpnConnectionState.Connected, WarpTrafficMode.TrafficAndDns, WarpTraceState.On, 31, false)]
    [InlineData(VpnConnectionState.Connected, WarpTrafficMode.TrafficAndDns, WarpTraceState.On, -1, false)]
    public void OnlyFreshCombinedEvidenceConfirmsTheTestRequest(VpnConnectionState state, WarpTrafficMode mode, WarpTraceState trace, int sampleAge, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var status = new VpnStatus("cloudflare-warp", "WARP", now, state, true, true, true, true, true, mode, "");
        var sample = new VpnProbeSample(now.AddSeconds(-sampleAge), true, trace, 10);
        Assert.Equal(expected, VpnMeasurements.ConfirmsTestTraffic(status, sample, now));
        Assert.False(VpnMeasurements.ConfirmsTestTraffic(status with { CheckedAt = now.AddSeconds(-21) }, sample, now));
        Assert.False(VpnMeasurements.ConfirmsTestTraffic(status with { Trusted = false }, sample, now));
    }
    [Fact] public void NeverExecuteAnArbitraryOrMissingWarpExecutable()
    {
        Assert.False(CloudflareFileTrust.Verify(Path.Combine(Path.GetTempPath(), "warp-cli.exe"), Path.GetTempPath()));
        Assert.False(CloudflareFileTrust.Verify(Path.Combine(Environment.SystemDirectory, "notepad.exe"), Environment.SystemDirectory));
        Assert.Equal("https", InstalledWarpClient.SetupPage.Scheme); Assert.Equal("developers.cloudflare.com", InstalledWarpClient.SetupPage.Host);
    }
    [Fact] public void WinTrustReadsTheVerifiedSignerOfTheOfficialDotnetHostWithoutExecutingIt()
    {
        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? throw new InvalidOperationException("DOTNET_ROOT is required for the read-only Windows signature check.");
        var file = Path.Combine(root, "dotnet.exe");
        Assert.True(File.Exists(file));
        // The official host's certificate CN is .NET (organization Microsoft Corporation).
        Assert.True(CloudflareFileTrust.ReadVerifiedPublisher(file) is ".NET" or "Microsoft Corporation");
        Assert.False(CloudflareFileTrust.Verify(file, root));
        Assert.Null(CloudflareFileTrust.ReadVerifiedPublisher(typeof(VpnMeasurementTests).Assembly.Location));
    }
}
