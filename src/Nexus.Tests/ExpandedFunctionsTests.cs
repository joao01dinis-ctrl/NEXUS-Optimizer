using Microsoft.Data.Sqlite;
using Nexus.Gaming;
using Nexus.Network;
using Nexus.Optimization;
using Nexus.Storage;
using Xunit;

namespace Nexus.Tests;

public sealed class ExpandedFunctionsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Nexus-expanded-tests-" + Guid.NewGuid());
    private string Database => Path.Combine(root, "nexus.db");
    public ExpandedFunctionsTests() => Directory.CreateDirectory(root);
    private sealed class PolicyStore : IUserPolicyStore
    {
        public PolicyValue Value = new(false, 0);
        public PolicyValue Read(UserPolicy policy) => Value;
        public void Write(UserPolicy policy, PolicyValue value) => Value = value;
    }
    private sealed class DnsStore : IDnsConfigurationStore
    {
        public DnsConfiguration Value = new(true, []);
        public int Writes;
        public DnsConfiguration Read(Guid id) => Value;
        public void Write(Guid id, DnsConfiguration value) { Value = value; Writes++; }
    }
    [Fact] public void UserPolicyUndoRestoresAbsenceAndPreservesExternalChanges()
    {
        var store = new PolicyStore(); var policy = UserPolicies.All[0];
        var o = new SystemOptimizer(Database, k => UserPolicies.Resolve(k, store));
        o.Apply(policy.Key, policy.DesiredState); Assert.Equal(new(true, 1), store.Value);
        store.Value = new(true, 9); Assert.Throws<InvalidOperationException>(() => o.Undo()); Assert.Equal(9, store.Value.Value);
        store.Value = new(true, 1); o.Undo(); Assert.Equal(new(false, 0), store.Value);
    }
    [Fact] public void DnsUndoPreservesStaticOrderAndCanRestoreAutomaticAfterRestart()
    {
        var id = Guid.NewGuid(); var key = "dns:" + id; var store = new DnsStore();
        var o = new SystemOptimizer(Database, k => DnsSettings.Resolve(k, store));
        o.Apply(key, DnsSettings.State(false, "1.1.1.1", "1.0.0.1"));
        var reopened = new SystemOptimizer(Database, k => DnsSettings.Resolve(k, store)); reopened.Undo();
        Assert.True(store.Value.Automatic); Assert.Empty(store.Value.Servers);
        store.Value = new(false, ["8.8.8.8", "9.9.9.9"]);
        o.Apply(key, DnsSettings.State(true)); o.Undo(); Assert.Equal(new[] { "8.8.8.8", "9.9.9.9" }, store.Value.Servers);
    }
    [Fact] public void InvalidDnsInputDoesNotWriteOrLeavePendingIntent()
    {
        var store = new DnsStore(); var id = Guid.NewGuid();
        var o = new SystemOptimizer(Database, k => DnsSettings.Resolve(k, store));
        Assert.Throws<ArgumentException>(() => o.Apply("dns:" + id, "{\"Automatic\":false,\"Servers\":[\"127.0.0.1\"]}"));
        Assert.Equal(0, store.Writes); Assert.DoesNotContain(o.History(), x => x.State == "pending");
        Assert.Throws<ArgumentException>(() => DnsSettings.State(true, "1.1.1.1"));
        Assert.Throws<ArgumentException>(() => DnsSettings.Resolve("dns:invalid", store));
    }
    [Fact] public void LibraryPersistsAndRejectsCommandsOrNetworkExecutables()
    {
        var exe = Path.Combine(root, "example.exe"); File.WriteAllBytes(exe, []);
        var library = new GameLibrary(Database); library.Add(exe); library.Add(exe);
        Assert.Single(new GameLibrary(Database).Read());
        Assert.Throws<ArgumentException>(() => library.Add("powershell.exe -command test"));
        Assert.Throws<ArgumentException>(() => library.Add(@"\\server\game.exe"));
        Assert.Throws<ArgumentException>(() => library.Add(Path.Combine(root, "script.cmd")));
        library.Remove(exe); Assert.Empty(library.Read()); Assert.True(File.Exists(exe));
    }
    [Fact] public void IcmpLossAndJitterUseOnlySuccessfulSamples()
    {
        var result = new LatencyResult("1.1.1.1", 5, [10, 30, 20]);
        Assert.Equal(40, result.LossPercent); Assert.Equal(20, result.Average); Assert.Equal(15, result.Jitter);
        Assert.Null(new LatencyResult("1.1.1.1", 5, []).Average);
    }
    [Fact] public async Task InvalidLatencyDestinationSendsNoProbe()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => LatencyProbe.MeasureAsync("https://example.com"));
        await Assert.ThrowsAsync<ArgumentException>(() => LatencyProbe.MeasureAsync("127.0.0.1"));
    }
    [Fact] public async Task OptionalAppRemovalRejectsUnknownPackageBeforeAccessingWindows()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => OptionalApps.RemoveAsync(new("Microsoft.WindowsStore", "Store", "fake")));
    }
    [Fact] public async Task CancelledLatencySendsNoProbe()
    {
        using var c = new CancellationTokenSource(); c.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => LatencyProbe.MeasureAsync("1.1.1.1", c.Token));
    }
    [Fact] public void WorkingSetRejectsProtectedProcessBeforeOpeningIt()
    {
        Assert.Throws<InvalidOperationException>(() => Nexus.Hardware.WorkingSetTrim.Trim(new(999999, "game", 10, 10, 1), new HashSet<int> { 999999 }));
    }
    [Fact] public void DriftIsReadOnlyAndReapplyCapturesExternalValueForUndo()
    {
        var store = new PolicyStore(); var p = UserPolicies.All[0];
        var o = new SystemOptimizer(Database, k => UserPolicies.Resolve(k, store));
        o.Apply(p.Key, p.DesiredState); Assert.Empty(o.FindDrift());
        store.Value = new(true, 0); var drift = Assert.Single(o.FindDrift());
        Assert.Equal(0, store.Value.Value); Assert.Single(o.History()); Assert.Equal(p.Key, drift.Record.Key);
        o.Apply(p.Key, drift.Record.After); o.Undo(); Assert.Equal(0, store.Value.Value);
    }
    [Fact] public void CatalogDistinguishesExcludedProtectionAndUnknownFeatures()
    {
        var catalog = Nexus.Core.FeatureCatalog.Read();
        Assert.NotEmpty(catalog);
        Assert.Equal(catalog.Count, catalog.Select(x => x.Name).Distinct().Count());
        Assert.Contains(catalog, x => x.Name == "Desabilitar o SmartScreen" && x.Page is null && x.State.StartsWith("Excluída"));
        Assert.Contains(catalog, x => x.Name == "Ajustes Premium desfocados" && x.Page is null && x.State == "Não identificados");
    }
    [Fact] public void AutomaticCleanupRequiresThirtyDaysWithoutAccessAndCapsSelection()
    {
        var old = DateTime.UtcNow.AddDays(-40);
        for (var i = 0; i < 110; i++)
        {
            var path = Path.Combine(root, $"old-{i:D3}.tmp"); File.WriteAllText(path, "old");
            File.SetLastWriteTimeUtc(path, old); File.SetLastAccessTimeUtc(path, old);
        }
        var recent = Path.Combine(root, "recent-access.tmp"); File.WriteAllText(recent, "recent");
        File.SetLastWriteTimeUtc(recent, old); File.SetLastAccessTimeUtc(recent, DateTime.UtcNow.AddDays(-10));
        var scan = new TemporaryCleaner().Scan(root);
        var selection = AutomaticCleanupPolicy.Select(scan.Files, DateTime.UtcNow);
        Assert.Equal(100, selection.Count); Assert.DoesNotContain(selection, x => x.Path == recent);
        Assert.All(selection, x => Assert.True(File.Exists(x.Path)));
        Assert.Throws<ArgumentException>(() => AutomaticCleanupPolicy.Select(scan.Files, DateTime.Now));
    }
    [Fact] public void AutomaticCleanupEnforcesFileAndTotalSizeCaps()
    {
        var now = DateTime.UtcNow; var old = now.AddDays(-40);
        var files = Enumerable.Range(0, 40).Select(i => new TemporaryFileCandidate(Path.Combine(root, $"{i:D3}.tmp"), 10 * 1048576L, old, old)).ToList();
        files.Add(new(Path.Combine(root, "negative.tmp"), -1, old, old));
        files.Add(new(Path.Combine(root, "oversized.tmp"), 11 * 1048576L, old, old));
        var selected = AutomaticCleanupPolicy.Select(files, now);
        Assert.Equal(25, selected.Count); Assert.True(selected.Sum(x => x.Bytes) <= 256 * 1048576L);
        Assert.All(selected, x => Assert.InRange(x.Bytes, 0, 10 * 1048576L));
    }
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
}
