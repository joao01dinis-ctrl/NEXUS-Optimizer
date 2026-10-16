using Nexus.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Nexus.Tests;

public sealed class ProtectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 20, 0, 0, TimeSpan.Zero);
    private static DefenderSnapshot Active => DefenderSnapshot.Parse("""{"AMRunningMode":"Normal","AMServiceEnabled":true,"AntivirusEnabled":true,"RealTimeProtectionEnabled":true,"DefenderSignaturesOutOfDate":false}""", Now);
    private sealed class Backend : IProtectionBackend
    {
        public AntivirusHealth Health = AntivirusHealth.Good;
        public bool Admin = true, FailRead;
        public DefenderSnapshot Snapshot = Active;
        public int Requests;
        public ProtectionAction? LastAction;
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? Finish;
        public AntivirusHealth ReadWindowsHealth() => Health;
        public bool IsAdministrator => Admin;
        public Task<DefenderSnapshot> ReadDefenderAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return FailRead ? Task.FromException<DefenderSnapshot>(new InvalidOperationException("Unavailable")) : Task.FromResult(Snapshot);
        }
        public async Task ExecuteAsync(ProtectionAction action, CancellationToken cancellation)
        {
            Requests++; LastAction = action; Entered.TrySetResult();
            if (Finish is not null) await Finish.Task.WaitAsync(cancellation);
        }
    }
    [Fact]
    public void MissingFlagsDoNotBecomeHealthyOrAuthorizeAnAction()
    {
        var snapshot = DefenderSnapshot.Parse("{}", Now);
        Assert.Null(snapshot.RealTimeEnabled); Assert.Null(snapshot.SignaturesOutOfDate); Assert.False(snapshot.Active);
        Assert.False(new ProtectionStatus(Now, AntivirusHealth.Unknown, snapshot, true, null).CanRequestAction);
    }
    [Theory]
    [InlineData("Passive")]
    [InlineData("EDR Block Mode")]
    [InlineData("")]
    public async Task AnotherOrUnknownProviderCannotReceiveDefenderRequests(string mode)
    {
        var backend = new Backend { Snapshot = Active with { Mode = mode } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new WindowsProtection(backend).RequestAsync(ProtectionAction.QuickScan, true));
        Assert.Equal(0, backend.Requests);
    }
    [Fact]
    public async Task AdministratorAndExplicitConfirmationAreBothRequired()
    {
        var backend = new Backend(); var service = new WindowsProtection(backend);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestAsync(ProtectionAction.FullScan, false));
        backend.Admin = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestAsync(ProtectionAction.FullScan, true));
        Assert.Equal(0, backend.Requests);
    }
    [Fact]
    public async Task StatusIsRecheckedBeforeRequestingAScan()
    {
        var backend = new Backend(); var service = new WindowsProtection(backend);
        Assert.True((await service.ReadAsync()).CanRequestAction);
        backend.Snapshot = Active with { Mode = "Passive" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestAsync(ProtectionAction.QuickScan, true));
        Assert.Equal(0, backend.Requests);
    }
    [Fact]
    public async Task UnavailableDefenderDoesNotEraseTheWindowsProviderReport()
    {
        var backend = new Backend { FailRead = true, Health = AntivirusHealth.Snoozed };
        var status = await new WindowsProtection(backend).ReadAsync();
        Assert.Null(status.Defender); Assert.NotNull(status.ReadError); Assert.False(status.CanRequestAction);
        Assert.Contains("pausa", status.Summary);
    }
    [Theory]
    [InlineData(ProtectionAction.QuickScan)]
    [InlineData(ProtectionAction.FullScan)]
    [InlineData(ProtectionAction.UpdateSignatures)]
    public async Task SupportedConfirmedActionsAreDelegatedToTheDefender(ProtectionAction action)
    {
        var backend = new Backend();
        await new WindowsProtection(backend).RequestAsync(action, true);
        Assert.Equal(action, backend.LastAction); Assert.Equal(1, backend.Requests);
    }
    [Fact]
    public async Task ConcurrentRequestsDoNotStartTwoScans()
    {
        var backend = new Backend { Finish = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var service = new WindowsProtection(backend);
        var first = service.RequestAsync(ProtectionAction.QuickScan, true);
        await backend.Entered.Task;
        try { await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestAsync(ProtectionAction.FullScan, true)); }
        finally { backend.Finish.TrySetResult(); await first; }
        Assert.Equal(1, backend.Requests);
    }
    [Fact]
    public async Task ArbitraryActionsCannotBeRequested()
    {
        var backend = new Backend();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new WindowsProtection(backend).RequestAsync((ProtectionAction)99, true));
        Assert.Equal(0, backend.Requests);
    }
    [Fact]
    public void ContradictoryRealTimeOrSignatureStatusRequiresAttention()
    {
        var status = new ProtectionStatus(Now, AntivirusHealth.Good, Active with { RealTimeEnabled = false }, true, null);
        Assert.Contains("desligada", status.Summary);
        Assert.Contains("desatualizadas", (status with { Defender = Active with { SignaturesOutOfDate = true } }).Summary);
    }
    [Theory]
    [InlineData("/Date(1792094400000)/", true)]
    [InlineData("/Date(1792094400000+0100)/", true)]
    [InlineData("2026-10-15T20:00:00Z", true)]
    [InlineData("1970-01-01T00:00:00Z", false)]
    [InlineData("2030-01-01T00:00:00Z", false)]
    [InlineData("/Date()/", false)]
    [InlineData("/Date(999999999999999999)/", false)]
    [InlineData("invalid", false)]
    public void MissingInvalidSentinelAndFutureDatesAreNotInvented(string date, bool valid)
    {
        var snapshot = DefenderSnapshot.Parse(JsonSerializer.Serialize(new { QuickScanEndTime = date }), Now);
        Assert.Equal(valid, snapshot.QuickScanEnded is not null);
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("broken")]
    public void MalformedResponseIsRejected(string json) => Assert.ThrowsAny<JsonException>(() => DefenderSnapshot.Parse(json, Now));
}
