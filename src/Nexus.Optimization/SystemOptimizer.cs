using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace Nexus.Optimization;

public interface ISystemSetting
{
    string Key { get; }
    string Name { get; }
    string Read();
    void Write(string value);
}

public sealed record OptimizationRecord(long Id, string At, string Key, string Name, string Before, string After, string State, string? Owner = null);
public sealed record PreparedAdjustment(string Key, string Value, string Expected);
public sealed record SettingDrift(OptimizationRecord Record, string? Current, string? Error)
{
    public string Display => Record.Name + "\n" + (Error is null ? "O valor atual difere do último aplicado pelo NEXUS. Reaplicar exige revisão e guarda o valor atual para undo." : "Não foi possível consultar: " + Error);
}
public sealed record RestoreResult(int Restored, IReadOnlyList<string> Errors)
{
    public bool Complete => Errors.Count == 0;
}

// Each intent is durable before Windows is changed. Interrupted writes remain recoverable.
public sealed class SystemOptimizer
{
    private readonly string connectionString;
    private readonly Func<string, ISystemSetting> resolve;
    private readonly string mutexName;
    public string DatabasePath { get; }
    public SystemOptimizer(string path, Func<string, ISystemSetting> resolve)
    {
        this.resolve = resolve;
        var fullPath = Path.GetFullPath(path);
        DatabasePath = fullPath;
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
        // The same journal must protect the setting read/write as well as SQLite operations.
        // Windows paths are case insensitive; a stable hash also keeps the mutex name valid.
        var identity = OperatingSystem.IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
        mutexName = (OperatingSystem.IsWindows() ? @"Local\" : "") + "NexusOptimizer-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        using var operation = LockJournal();
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "CREATE TABLE IF NOT EXISTS system_changes(id INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, key TEXT NOT NULL, name TEXT NOT NULL, before_value TEXT NOT NULL, after_value TEXT NOT NULL, state TEXT NOT NULL, owner TEXT NULL)";
        q.ExecuteNonQuery();
        q.CommandText = "PRAGMA table_info(system_changes)";
        bool hasOwner;
        using (var columns = q.ExecuteReader())
        {
            hasOwner = false;
            while (columns.Read()) if (columns.GetString(1) == "owner") hasOwner = true;
        }
        if (!hasOwner) { q.CommandText = "ALTER TABLE system_changes ADD COLUMN owner TEXT NULL"; q.ExecuteNonQuery(); }
    }
    // The callback must remain synchronous: named mutex ownership belongs to its thread.
    public T ExecuteExclusive<T>(Func<T> action)
    {
        using var operation = LockJournal();
        return action();
    }
    public void ExportBackup(string destination)
    {
        var target = Path.GetFullPath(destination);
        if (string.Equals(target, DatabasePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || File.Exists(target))
            throw new IOException("Escolhe um ficheiro novo. A cópia de segurança não substitui dados existentes.");
        var staging = Path.Combine(Path.GetDirectoryName(target)!, ".nexus-backup-" + Guid.NewGuid().ToString("N") + ".sqlite");
        using var operation = LockJournal();
        using (new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        try
        {
            using (var source = Open())
            using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = staging, Pooling = false }.ToString()))
            { backup.Open(); source.BackupDatabase(backup); }
            File.Move(staging, target, overwrite: false);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }
    private IDisposable LockJournal()
    {
        var mutex = new Mutex(false, mutexName);
        try
        {
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(15)))
                    throw new InvalidOperationException("Outra janela do NEXUS está a alterar o sistema. Espera que termine e tenta novamente.");
            }
            catch (AbandonedMutexException)
            {
                // Ownership is granted after a crashed owner. Pending records guide recovery.
            }
            return new JournalLock(mutex);
        }
        catch { mutex.Dispose(); throw; }
    }
    private sealed class JournalLock(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            try { mutex.ReleaseMutex(); }
            finally { mutex.Dispose(); }
        }
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public IReadOnlyList<OptimizationRecord> History()
    {
        using var operation = LockJournal();
        return ReadHistory();
    }
    public IReadOnlyList<SettingDrift> FindDrift() => ExecuteExclusive(() =>
    {
        var records = ReadHistory();
        if (records.Any(x => x.State == "pending")) throw new InvalidOperationException("Repõe primeiro a alteração interrompida no Histórico.");
        var result = new List<SettingDrift>();
        foreach (var item in records.DistinctBy(x => x.Key).Where(x => x.State == "applied" && x.Owner is null && !x.Key.StartsWith("priority:", StringComparison.Ordinal)).Take(30))
        {
            try { var current = resolve(item.Key).Read(); if (current != item.After) result.Add(new(item, current, null)); }
            catch (Exception ex) { result.Add(new(item, null, ex.Message)); }
        }
        return (IReadOnlyList<SettingDrift>)result;
    });
    private IReadOnlyList<OptimizationRecord> ReadHistory()
    {
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "SELECT id,at,key,name,before_value,after_value,state,owner FROM system_changes ORDER BY id DESC";
        using var r = q.ExecuteReader(); var entries = new List<OptimizationRecord>();
        while (r.Read()) entries.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)));
        return entries;
    }
    private void State(long id, string state)
    {
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "UPDATE system_changes SET state=$s WHERE id=$id";
        q.Parameters.AddWithValue("$s", state); q.Parameters.AddWithValue("$id", id); q.ExecuteNonQuery();
    }
    public bool Apply(string key, string after) => ApplyCore(null, key, after) is not null;
    public int ApplyBatch(IReadOnlyList<PreparedAdjustment> prepared) => ExecuteExclusive(() =>
    {
        if (prepared.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != prepared.Count)
            throw new ArgumentException("A seleção tem definições repetidas.", nameof(prepared));
        foreach (var item in prepared)
            if (resolve(item.Key).Read() != item.Expected)
                throw new InvalidOperationException("Um ajuste mudou desde a análise. Volta a analisar.");
        var previousId = ReadHistory().FirstOrDefault()?.Id ?? 0;
        try
        {
            var applied = 0;
            foreach (var item in prepared) if (Apply(item.Key, item.Value)) applied++;
            return applied;
        }
        catch (Exception error)
        {
            // Include interrupted writes which changed Windows before failing verification.
            // The journal lock prevents another instance's changes entering this recovery.
            var recovery = RestoreAll(x => x.Id > previousId && x.Owner is null);
            if (!recovery.Complete)
                throw new AggregateException("PC Boost interrompido. Há ajustes por recuperar no Histórico: " + string.Join("\n", recovery.Errors), error);
            throw;
        }
    });
    public long? ApplyOwned(string owner, string key, string after)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        return ApplyCore(owner, key, after);
    }
    private long? ApplyCore(string? owner, string key, string after)
    {
        using var operation = LockJournal();
        if (ReadHistory().Any(x => x.State == "pending")) throw new InvalidOperationException("Existe uma alteração interrompida. Usa primeiro Desfazer última alteração.");
        var setting = resolve(key); var before = setting.Read();
        if (key.StartsWith("priority:", StringComparison.Ordinal) && before is not ("Normal" or "AboveNormal" or "ended"))
            throw new InvalidOperationException("A prioridade da aplicação mudou. Seleciona uma aplicação com prioridade Normal; este valor não pode ser reposto pelo NEXUS.");
        if (before == after) return null;
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO system_changes(at,key,name,before_value,after_value,state,owner) VALUES($at,$k,$n,$b,$a,'pending',$owner); SELECT last_insert_rowid()";
        q.Parameters.AddWithValue("$at", DateTimeOffset.Now.ToString("O")); q.Parameters.AddWithValue("$k", key);
        q.Parameters.AddWithValue("$n", setting.Name); q.Parameters.AddWithValue("$b", before); q.Parameters.AddWithValue("$a", after);
        q.Parameters.AddWithValue("$owner", (object?)owner ?? DBNull.Value);
        var id = (long)q.ExecuteScalar()!;
        try
        {
            // Refuse to overwrite an external change between capture and execution.
            if (setting.Read() != before) throw new InvalidOperationException("O estado mudou durante a operação. Tenta novamente.");
            setting.Write(after);
            if (setting.Read() != after) throw new InvalidOperationException("O Windows não confirmou a alteração. Usa Desfazer para recuperar.");
            State(id, "applied"); return id;
        }
        catch
        {
            try { if (setting.Read() == before) State(id, "failed"); } catch { /* Retain pending recovery. */ }
            throw;
        }
    }
    public string Undo()
    {
        using var operation = LockJournal();
        var item = ReadHistory().FirstOrDefault(x => x.State is "applied" or "pending");
        if (item is null) return "Não há alterações do sistema para desfazer.";
        return UndoRecord(item);
    }
    public string Undo(long id)
    {
        using var operation = LockJournal();
        var entries = ReadHistory();
        var item = entries.FirstOrDefault(x => x.Id == id);
        if (item is null || item.State is not ("applied" or "pending"))
            return "Esta alteração já não precisa de reposição.";
        if (entries.Any(x => x.Id > item.Id && x.Key == item.Key && x.State is "applied" or "pending"))
            throw new InvalidOperationException("Existe uma alteração mais recente para esta definição. Desfaz primeiro a alteração mais recente.");
        return UndoRecord(item);
    }
    // Descending order restores chains to the captured original value. Conflicts
    // remain durable and do not prevent unrelated settings from being recovered.
    public RestoreResult RestoreAll(Func<OptimizationRecord, bool>? include = null) => ExecuteExclusive(() =>
    {
        var restored = 0; var errors = new List<string>();
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in ReadHistory().Where(x => x.State is "applied" or "pending"))
        {
            if (include is not null && !include(item)) continue;
            if (blocked.Contains(item.Key)) continue;
            try { Undo(item.Id); restored++; }
            catch (Exception error) { blocked.Add(item.Key); errors.Add(item.Name + ": " + error.Message); }
        }
        return new RestoreResult(restored, errors);
    });
    private string UndoRecord(OptimizationRecord item)
    {
        var setting = resolve(item.Key); var current = setting.Read();
        if (item.Key.StartsWith("priority:") && current == "ended")
        { State(item.Id, "reverted"); return "O jogo já terminou; a prioridade temporária deixou de existir."; }
        if (current != item.Before)
        {
            if (current != item.After) throw new InvalidOperationException("Esta definição foi alterada fora do NEXUS. Não foi substituída. Repõe o valor aplicado pelo NEXUS antes de desfazer.");
            setting.Write(item.Before);
            if (setting.Read() != item.Before) throw new InvalidOperationException("Não foi possível confirmar a reposição. Podes tentar novamente.");
        }
        State(item.Id, "reverted"); return $"Reposto: {item.Name}.";
    }
}
