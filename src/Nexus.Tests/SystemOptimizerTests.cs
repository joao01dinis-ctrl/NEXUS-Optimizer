using Nexus.Optimization;
using Xunit;

namespace Nexus.Tests;

public sealed class SystemOptimizerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Nexus-tests-" + Guid.NewGuid());
    private sealed class Setting : ISystemSetting
    {
        public string Key => "test"; public string Name => "Test";
        public string Value = "original";
        public bool FailBefore, FailAfter;
        public string Read() => Value;
        public void Write(string value) { if (FailBefore) throw new IOException("before"); Value = value; if (FailAfter) throw new IOException("after"); }
    }
    private SystemOptimizer Create(Setting s) => new(Path.Combine(root, "test.db"), _ => s);
    [Fact] public void BackupIsConsistentAndDoesNotAlterOriginalOrOverwriteFiles()
    {
        var setting = new Setting(); var optimizer = Create(setting); optimizer.ApplyOwned("session", "test", "changed");
        var backup = Path.Combine(root, "backup.sqlite"); optimizer.ExportBackup(backup);
        Assert.Throws<IOException>(() => optimizer.ExportBackup(backup));
        Assert.Throws<IOException>(() => optimizer.ExportBackup(optimizer.DatabasePath));
        optimizer.Undo();
        using var snapshot = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = backup, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        snapshot.Open(); using var q = snapshot.CreateCommand();
        q.CommandText = "SELECT state || ':' || before_value || ':' || owner FROM system_changes";
        Assert.Equal("applied:original:session", q.ExecuteScalar());
        Assert.Equal("original", setting.Value); Assert.Equal("reverted", Assert.Single(optimizer.History()).State);
        Assert.Empty(Directory.GetFiles(root, ".nexus-backup-*"));
    }
    [Fact] public void UndoSurvivesRestartAndPreservesExactOriginal()
    {
        var s = new Setting { Value = "custom-user-value" }; Create(s).Apply("test", "new");
        Create(s).Undo(); Assert.Equal("custom-user-value", s.Value); Assert.Equal("reverted", Create(s).History()[0].State);
    }
    [Fact] public void NoOpDoesNotAddHistory()
    {
        var s = new Setting(); var o = Create(s); Assert.False(o.Apply("test", "original")); Assert.Empty(o.History());
    }
    [Fact] public void UndoRefusesExternalChanges()
    {
        var s = new Setting(); var o = Create(s); o.Apply("test", "new"); s.Value = "external";
        Assert.Throws<InvalidOperationException>(() => o.Undo()); Assert.Equal("external", s.Value); Assert.Equal("applied", o.History()[0].State);
    }
    [Fact] public void InterruptedWriteCanBeRecoveredAfterRestart()
    {
        var s = new Setting { FailAfter = true }; var o = Create(s);
        Assert.Throws<IOException>(() => o.Apply("test", "new")); Assert.Equal("pending", o.History()[0].State);
        Assert.Throws<InvalidOperationException>(() => o.Apply("test", "another"));
        s.FailAfter = false; Create(s).Undo(); Assert.Equal("original", s.Value);
    }
    [Fact] public void FailureBeforeWriteDoesNotBlockSubsequentOperations()
    {
        var s = new Setting { FailBefore = true }; var o = Create(s);
        Assert.Throws<IOException>(() => o.Apply("test", "new")); Assert.Equal("failed", o.History()[0].State);
        s.FailBefore = false; Assert.True(o.Apply("test", "new"));
    }
    [Fact] public void MultipleChangesUndoInReverseOrder()
    {
        var s = new Setting(); var o = Create(s); o.Apply("test", "first"); o.Apply("test", "second");
        o.Undo(); Assert.Equal("first", s.Value); o.Undo(); Assert.Equal("original", s.Value);
    }
    [Fact] public void InterruptedUndoCanFinishAfterRestartWithoutWritingAgain()
    {
        var s = new Setting(); var o = Create(s); o.Apply("test", "new"); s.FailAfter = true;
        Assert.Throws<IOException>(() => o.Undo()); Assert.Equal("original", s.Value);
        Assert.Equal("applied", o.History()[0].State);
        s.FailBefore = true;
        Create(s).Undo(); Assert.Equal("reverted", o.History()[0].State);
    }
    [Fact] public void UndoFailureBeforeMutationRemainsRetryable()
    {
        var s = new Setting(); var o = Create(s); o.Apply("test", "new"); s.FailBefore = true;
        Assert.Throws<IOException>(() => o.Undo()); Assert.Equal("new", s.Value);
        Assert.Equal("applied", o.History()[0].State);
        s.FailBefore = false; Create(s).Undo(); Assert.Equal("original", s.Value);
    }
    [Fact] public void TargetedUndoPreservesNewerUnrelatedChangesAndIsIdempotent()
    {
        var first = new Setting(); var other = new Setting();
        var o = new SystemOptimizer(Path.Combine(root, "test.db"), key => key == "first" ? first : other);
        o.Apply("first", "changed-first"); var id = o.History()[0].Id;
        o.Apply("other", "changed-other");
        o.Undo(id); Assert.Equal("original", first.Value); Assert.Equal("changed-other", other.Value);
        o.Undo(id); Assert.Equal("original", first.Value); Assert.Equal("changed-other", other.Value);
        Assert.Equal("applied", o.History()[0].State);
    }
    [Fact] public void TargetedUndoRefusesToRevertOlderChangeOnSameSetting()
    {
        var s = new Setting(); var o = Create(s); o.Apply("test", "first"); var id = o.History()[0].Id;
        o.Apply("test", "second");
        var error = Assert.Throws<InvalidOperationException>(() => o.Undo(id));
        Assert.Contains("mais recente", error.Message); Assert.Equal("second", s.Value);
        o.Undo(); Assert.Equal("first", s.Value);
        o.Undo(id); Assert.Equal("original", s.Value);
    }
    [Fact] public void OwnedIntentSurvivesWriteFailureAndRestart()
    {
        var s = new Setting { FailAfter = true }; var o = Create(s);
        Assert.Throws<IOException>(() => o.ApplyOwned("session-1", "test", "new"));
        var pending = Create(s).History()[0];
        Assert.Equal("session-1", pending.Owner); Assert.Equal("pending", pending.State);
        s.FailAfter = false; o.Undo(pending.Id); Assert.Equal("original", s.Value);
        Assert.Null(o.ApplyOwned("session-2", "test", "original"));
    }
    [Fact] public void LegacyJournalMigrationPreservesHistoryWithoutInventingOwner()
    {
        Directory.CreateDirectory(root);
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + Path.Combine(root, "test.db")))
        {
            c.Open(); using var q = c.CreateCommand();
            q.CommandText = "CREATE TABLE system_changes(id INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, key TEXT NOT NULL, name TEXT NOT NULL, before_value TEXT NOT NULL, after_value TEXT NOT NULL, state TEXT NOT NULL); INSERT INTO system_changes(at,key,name,before_value,after_value,state) VALUES('old','test','Test','original','new','applied')";
            q.ExecuteNonQuery();
        }
        var s = new Setting { Value = "new" }; var o = Create(s);
        Assert.Null(o.History()[0].Owner); o.Undo(); Assert.Equal("original", s.Value);
        var id = o.ApplyOwned("session", "test", "owned"); Assert.NotNull(id);
        Assert.Equal("session", Create(s).History()[0].Owner);
    }
    private sealed class CapturedPriority : ISystemSetting
    {
        public string Key => "priority:123:100"; public string Name => "Game priority";
        public string Value = "Normal"; public int Writes;
        public string Read() => Value;
        public void Write(string value)
        {
            if (value is not ("Normal" or "AboveNormal")) throw new ArgumentException("Unsupported priority.");
            Writes++; Value = value;
        }
    }
    [Theory]
    [InlineData("BelowNormal")]
    [InlineData("High")]
    [InlineData("Idle")]
    [InlineData("RealTime")]
    public void PriorityChangedAfterPrecheckIsRejectedBeforeMutation(string changedPriority)
    {
        var s = new CapturedPriority(); var o = new SystemOptimizer(Path.Combine(root, "test.db"), _ => s);
        Assert.Equal("Normal", s.Read()); // The UI or session's eligibility check.
        s.Value = changedPriority; // The game adjusts its own priority before journal capture.
        Assert.Throws<InvalidOperationException>(() => o.ApplyOwned("session", s.Key, "AboveNormal"));
        Assert.Equal(changedPriority, s.Value); Assert.Equal(0, s.Writes); Assert.Empty(o.History());
    }
    private sealed class BlockingSetting : ISystemSetting, IDisposable
    {
        public string Key => "test"; public string Name => "Test";
        public string Value = "original";
        public readonly ManualResetEventSlim WritingFirst = new(), ReleaseFirst = new();
        public string Read() => Volatile.Read(ref Value);
        public void Write(string value)
        {
            if (value == "first")
            {
                WritingFirst.Set();
                if (!ReleaseFirst.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test did not release the first writer.");
            }
            Volatile.Write(ref Value, value);
        }
        public void Dispose() { WritingFirst.Dispose(); ReleaseFirst.Dispose(); }
    }
    [Fact] public async Task SeparateInstancesSerializeCaptureAndMutation()
    {
        using var s = new BlockingSetting();
        var first = new SystemOptimizer(Path.Combine(root, "test.db"), _ => s);
        // Normalize a different spelling to the same database and mutex identity.
        var second = new SystemOptimizer(Path.Combine(root, ".", "test.db"), _ => s);
        var firstTask = Task.Factory.StartNew(() => first.Apply("test", "first"), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Assert.True(s.WritingFirst.Wait(TimeSpan.FromSeconds(5)));
        using var secondStarted = new ManualResetEventSlim();
        var secondTask = Task.Factory.StartNew(() => { secondStarted.Set(); return second.Apply("test", "second"); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.NotSame(secondTask, await Task.WhenAny(secondTask, Task.Delay(150)));
        }
        finally { s.ReleaseFirst.Set(); }
        await Task.WhenAll(firstTask, secondTask);
        Assert.Equal("second", s.Value);
        Assert.Equal("first", second.History()[0].Before);
        second.Undo(); Assert.Equal("first", s.Value);
        first.Undo(); Assert.Equal("original", s.Value);
    }
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
