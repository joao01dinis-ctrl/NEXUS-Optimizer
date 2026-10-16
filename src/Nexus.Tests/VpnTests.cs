using Nexus.Network;
using Xunit;

namespace Nexus.Tests;

public sealed class VpnTests
{
    private sealed class Client : ICloudflareWarpClient
    {
        public WarpObservation Observation = Good(VpnConnectionState.Disconnected);
        public int Requests;
        public bool Complete = true;
        public TaskCompletionSource? Hold;
        public Task<WarpObservation> InspectAsync(CancellationToken cancellation) { cancellation.ThrowIfCancellationRequested(); return Task.FromResult(Observation); }
        public async Task RequestAsync(bool connect, CancellationToken cancellation)
        {
            Requests++;
            if (Hold is not null) await Hold.Task.WaitAsync(cancellation);
            if (Complete) Observation = Observation with { State = connect ? VpnConnectionState.Connected : VpnConnectionState.Disconnected };
        }
    }
    private static WarpObservation Good(VpnConnectionState state) => new(true, true, true, true, state, WarpTrafficMode.TrafficAndDns);
    [Theory]
    [InlineData("Status update: Connected", VpnConnectionState.Connected)]
    [InlineData("Status update: Disconnected\r\nReason: User request", VpnConnectionState.Disconnected)]
    [InlineData("Status update: Connecting", VpnConnectionState.Connecting)]
    [InlineData("Status update: Unable to connect", VpnConnectionState.Error)]
    [InlineData("Not connected", VpnConnectionState.Unknown)]
    [InlineData("Status update: Connected\nStatus update: Disconnected", VpnConnectionState.Unknown)]
    [InlineData("Erro desconhecido", VpnConnectionState.Unknown)]
    public void ParsesOnlyRecognizedUnambiguousStates(string text, VpnConnectionState expected) => Assert.Equal(expected, WarpOutput.State(text));
    [Theory]
    [InlineData("(default) Mode: WarpWithDnsOverHttps", WarpTrafficMode.TrafficAndDns)]
    [InlineData("Mode: TunnelOnly", WarpTrafficMode.TrafficOnly)]
    [InlineData("Mode: DnsOverHttps", WarpTrafficMode.DnsOnly)]
    [InlineData("Mode: WarpProxy", WarpTrafficMode.LocalProxy)]
    [InlineData("Mode: NewUnknownMode", WarpTrafficMode.Unknown)]
    [InlineData("Mode: Warp\nMode: DnsOnly", WarpTrafficMode.Unknown)]
    public void DnsAndProxyAreNotTunnels(string settings, WarpTrafficMode expected) => Assert.Equal(expected, WarpOutput.Mode(settings));
    [Theory]
    [InlineData("warp=on\nip=private-not-retained", WarpTraceState.On)]
    [InlineData("warp=plus\n", WarpTraceState.On)]
    [InlineData("warp=off\n", WarpTraceState.Off)]
    [InlineData("warp=on\nwarp=off", WarpTraceState.Unknown)]
    [InlineData("warp=maybe", WarpTraceState.Unknown)]
    [InlineData("<html>warp=on</html>", WarpTraceState.Unknown)]
    public void TraceRequiresExactSingleField(string text, WarpTraceState expected) => Assert.Equal(expected, WarpTrace.Parse(text));
    [Fact] public void HelpMustAdvertiseAllCommandsAndRejectsOversizedOutput()
    {
        Assert.True(WarpOutput.HasRequiredCommands("Commands:\n  status Show status\n settings Read settings\n connect Connect\n disconnect Disconnect\n"));
        Assert.False(WarpOutput.HasRequiredCommands("status settings connect disconnect"));
        Assert.Throws<InvalidDataException>(() => WarpOutput.State(new string('a', 65537)));
        Assert.True(WarpOutput.NeedsSetup("Accept Terms of Service?"));
        Assert.False(WarpOutput.RequiresInteractiveSetup("--help", "--accept-tos Accept Terms of Service"));
        Assert.True(WarpOutput.RequiresInteractiveSetup("status", "Accept Terms of Service?"));
        Assert.True(WarpOutput.OrganizationManaged("(user set) Organization: workplace"));
    }
    [Fact] public async Task ReadingAndMissingConsentNeverChangeTheConnection()
    {
        var client = new Client(); var provider = new CloudflareWarpProvider(client);
        Assert.True((await provider.ReadAsync()).CanConnect);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RequestConnectionAsync(true, false));
        Assert.Equal(0, client.Requests);
    }
    [Theory]
    [InlineData(false, true, true, WarpTrafficMode.TrafficAndDns, false, false)]
    [InlineData(true, false, true, WarpTrafficMode.TrafficAndDns, false, false)]
    [InlineData(true, true, false, WarpTrafficMode.TrafficAndDns, false, false)]
    [InlineData(true, true, true, WarpTrafficMode.DnsOnly, false, false)]
    [InlineData(true, true, true, WarpTrafficMode.Unknown, false, false)]
    [InlineData(true, true, true, WarpTrafficMode.TrafficAndDns, true, false)]
    [InlineData(true, true, true, WarpTrafficMode.TrafficAndDns, false, true)]
    public async Task UnsafeOrUnsupportedClientsCannotConnect(bool installed, bool trusted, bool compatible, WarpTrafficMode mode, bool managed, bool setup)
    {
        var client = new Client { Observation = new(installed, trusted, true, compatible, VpnConnectionState.Disconnected, mode, managed, setup) };
        var provider = new CloudflareWarpProvider(client);
        Assert.False((await provider.ReadAsync()).CanConnect);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RequestConnectionAsync(true, true));
        Assert.Equal(0, client.Requests);
    }
    [Fact] public async Task ConfirmedConnectionAndDisconnectionRequireObservedStates()
    {
        var client = new Client(); var provider = new CloudflareWarpProvider(client);
        Assert.True((await provider.RequestConnectionAsync(true, true)).ReportsTunnel);
        Assert.Equal(VpnConnectionState.Disconnected, (await provider.RequestConnectionAsync(false, true)).State);
        Assert.Equal(2, client.Requests);
    }
    [Fact] public async Task ACommandSuccessAloneDoesNotProduceConnectedStateOrAutomaticRollback()
    {
        var client = new Client { Complete = false }; var provider = new CloudflareWarpProvider(client, _ => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RequestConnectionAsync(true, true));
        Assert.Equal(VpnConnectionState.Disconnected, (await provider.ReadAsync()).State);
        Assert.Equal(1, client.Requests);
    }
    [Fact] public async Task ConcurrentCommandsAreRejectedAndCancellationDoesNotSendDisconnect()
    {
        var client = new Client { Hold = new() }; var provider = new CloudflareWarpProvider(client);
        using var cancellation = new CancellationTokenSource();
        var pending = provider.RequestConnectionAsync(true, true, cancellation.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RequestConnectionAsync(false, true));
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, client.Requests);
    }
    [Fact] public async Task WireGuardRemainsUnavailableWithoutRealInfrastructure()
    {
        var provider = new NexosWireGuardProvider(); var state = await provider.ReadAsync();
        Assert.False(state.CanConnect); Assert.False(state.ReportsTunnel);
        Assert.Equal(VpnConnectionState.Unavailable, state.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RequestConnectionAsync(true, true));
    }
}
