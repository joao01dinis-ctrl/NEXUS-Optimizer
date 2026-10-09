using Nexus.Gaming;
using Nexus.Optimization;
using Xunit;

namespace Nexus.Tests;

public sealed class GameSessionsTests : IDisposable
{
    private const string ProcessKey = "priority:123:100";
    private readonly string root = Path.Combine(Path.GetTempPath(), "Nexus-sessions-tests-" + Guid.NewGuid());
    private string PathName => Path.Combine(root, "sessions.db");
    private sealed class Setting(string key, string value) : ISystemSetting
    {
        public string Key => key; public string Name => key;
        public string Value = value;
        public bool FailBefore, FailAfter;
        public int Writes;
        public string Read() => Value;
        public void Write(string next)
        {
            if (FailBefore) throw new IOException("write-before");
            Writes++; Value = next;
            if (FailAfter) throw new IOException("write-after");
        }
    }
    private readonly Dictionary<string, Setting> settings = new()
    {
        [ProcessKey] = new(ProcessKey, "Normal"), ["power"] = new("power", "balanced"), ["animations"] = new("animations", "1")
    };
    private ISystemSetting Resolve(string key) => settings[key];
    private SystemOptimizer Optimizer() => new(PathName, Resolve);
    private GameSessions Sessions(SystemOptimizer optimizer) => new(PathName, optimizer, Resolve);
    [Fact] public void ProcessExitRestoresPowerAndEndsOnlyOwnedSession()
    {
        var o = Optimizer(); var sessions = Sessions(o);
        var session = sessions.Start(ProcessKey, "Game", "performance");
        Assert.Equal("active", session.Status); Assert.Equal("AboveNormal", settings[ProcessKey].Value);
        Assert.All(o.History(), x => Assert.Equal(session.Id, x.Owner));
        settings[ProcessKey].Value = "ended";
        var ended = sessions.Poll();
        Assert.Equal("ended", ended!.Status); Assert.NotNull(ended.Ended);
        Assert.Equal("balanced", settings["power"].Value); Assert.Equal(1, settings[ProcessKey].Writes);
        Assert.Null(sessions.Active); Assert.All(o.History(), x => Assert.Equal("reverted", x.State));
    }
    [Fact] public void SessionEndPreservesNewerUnrelatedUserChanges()
    {
        var o = Optimizer(); var sessions = Sessions(o);
        sessions.Start(ProcessKey, "Game", "performance");
        o.Apply("animations", "0");
        Assert.Equal("ended", sessions.End()!.Status);
        Assert.Equal("0", settings["animations"].Value);
        Assert.Equal("balanced", settings["power"].Value); Assert.Equal("Normal", settings[ProcessKey].Value);
        Assert.Equal("applied", o.History()[0].State); Assert.Null(o.History()[0].Owner);
    }
    [Fact] public void RestartRequiresExplicitRecoveryAndDoesNotRepeatStart()
    {
        var o = Optimizer(); Sessions(o).Start(ProcessKey, "Game", "performance");
        var powerWrites = settings["power"].Writes;
        settings[ProcessKey].Value = "ended";
        var recovered = Sessions(Optimizer());
        Assert.True(recovered.RecoveryRequired); Assert.Equal("active", recovered.Active!.Status);
        recovered.Poll();
        Assert.Equal("performance", settings["power"].Value); Assert.Equal(powerWrites, settings["power"].Writes);
        Assert.Throws<InvalidOperationException>(() => recovered.Start(ProcessKey, "Another game", "performance"));
        Assert.Equal("ended", recovered.End()!.Status); Assert.Equal("balanced", settings["power"].Value);
    }
    [Fact] public void PartialStartAndFailedUndoRemainVisibleUntilExplicitRetry()
    {
        var o = Optimizer(); var sessions = Sessions(o); settings[ProcessKey].FailAfter = true;
        var failed = sessions.Start(ProcessKey, "Game", "performance");
        Assert.Equal("recovery", failed.Status); Assert.NotNull(failed.Error);
        Assert.Contains(o.History(), x => x.Key == ProcessKey && x.Owner == failed.Id && x.State == "pending");
        sessions.Poll(); Assert.Equal("performance", settings["power"].Value);
        Assert.Equal("recovery", sessions.End()!.Status);
        Assert.Equal("balanced", settings["power"].Value);
        settings[ProcessKey].FailAfter = false; settings[ProcessKey].FailBefore = true;
        Assert.Equal("ended", sessions.End()!.Status); Assert.Equal("Normal", settings[ProcessKey].Value);
    }
    [Fact] public void LaterChangeOnSameSettingRequiresUndoBeforeSessionRecovery()
    {
        var o = Optimizer(); var sessions = Sessions(o); sessions.Start(ProcessKey, "Game", "performance");
        o.Apply("power", "user-choice");
        var result = sessions.End();
        Assert.Equal("recovery", result!.Status); Assert.NotNull(result.Error);
        Assert.Equal("user-choice", settings["power"].Value); Assert.Equal("Normal", settings[ProcessKey].Value);
        o.Undo(); Assert.Equal("performance", settings["power"].Value);
        Assert.Equal("ended", sessions.End()!.Status); Assert.Equal("balanced", settings["power"].Value);
    }
    [Fact] public void InterruptedStartWithoutSettingsCanBeClosedWithoutAnyWrite()
    {
        var o = Optimizer(); _ = Sessions(o);
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + PathName))
        {
            c.Open(); using var q = c.CreateCommand();
            q.CommandText = "INSERT INTO game_sessions(id,process_key,name,started,status) VALUES('interrupted','priority:123:100','Game','old','starting')";
            q.ExecuteNonQuery();
        }
        var recovered = Sessions(Optimizer());
        Assert.True(recovered.RecoveryRequired); Assert.Equal("starting", recovered.Active!.Status); Assert.Equal("ended", recovered.End()!.Status);
        Assert.All(settings.Values, s => Assert.Equal(0, s.Writes));
    }
    [Fact] public void SecondInstanceDoesNotDisableLiveOwnersAutomaticRestoration()
    {
        var first = Sessions(Optimizer()); first.Start(ProcessKey, "Game", "performance");
        var second = Sessions(Optimizer());
        Assert.True(second.RecoveryRequired); Assert.False(first.RecoveryRequired);
        Assert.Equal("active", first.Active!.Status);
        second.Poll(); Assert.Equal("performance", settings["power"].Value);
        settings[ProcessKey].Value = "ended";
        Assert.Equal("ended", first.Poll()!.Status); Assert.Equal("balanced", settings["power"].Value);
        Assert.Null(second.Active);
    }
    [Fact] public void SessionVisualAdjustmentsRestoreAfterRestart()
    {
        var o = Optimizer(); var sessions = Sessions(o);
        sessions.Start(ProcessKey, "Streaming", priority: false, animations: true);
        Assert.Equal("0", settings["animations"].Value); Assert.Equal("Normal", settings[ProcessKey].Value);
        Assert.Single(o.History()); Assert.Equal("ended", Sessions(Optimizer()).End()!.Status);
        Assert.Equal("1", settings["animations"].Value);
    }
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
