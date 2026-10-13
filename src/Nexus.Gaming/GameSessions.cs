using Microsoft.Data.Sqlite;
using Nexus.Optimization;

namespace Nexus.Gaming;

public sealed record GameSessionRecord(string Id, string ProcessKey, string Name, string Started, string? Ended, string Status, string? Error);

// Settings belong to a durable session before any Windows write, so recovery does not
// depend on a list of ids that could be lost if the app exits during a write.
public sealed class GameSessions
{
    private readonly SystemOptimizer optimizer;
    private readonly Func<string, ISystemSetting> resolve;
    private readonly string connectionString;
    private string? runningSessionId;

    public GameSessions(string path, SystemOptimizer optimizer, Func<string, ISystemSetting>? resolve = null)
    {
        this.optimizer = optimizer;
        this.resolve = resolve ?? WindowsSettings.Resolve;
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(fullPath, optimizer.DatabasePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("As sessões e o diário devem usar a mesma base de dados.", nameof(path));
        connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
        optimizer.ExecuteExclusive(() =>
        {
            using var c = Open(); using var q = c.CreateCommand();
            q.CommandText = "CREATE TABLE IF NOT EXISTS game_sessions(id TEXT PRIMARY KEY, process_key TEXT NOT NULL, name TEXT NOT NULL, started TEXT NOT NULL, ended TEXT NULL, status TEXT NOT NULL, error TEXT NULL)";
            q.ExecuteNonQuery();
            // RecoveryRequired is local to this instance. Opening another window must
            // not disable the live owner's automatic process-exit restoration.
            return true;
        });
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public IReadOnlyList<GameSessionRecord> History() => optimizer.ExecuteExclusive(ReadHistory);
    private IReadOnlyList<GameSessionRecord> ReadHistory()
    {
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "SELECT id,process_key,name,started,ended,status,error FROM game_sessions ORDER BY rowid DESC";
        using var r = q.ExecuteReader(); var sessions = new List<GameSessionRecord>();
        while (r.Read()) sessions.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)));
        return sessions;
    }
    private GameSessionRecord? ReadActive() => ReadHistory().FirstOrDefault(s => s.Ended is null);
    public GameSessionRecord? Active => optimizer.ExecuteExclusive(ReadActive);
    public bool RecoveryRequired => optimizer.ExecuteExclusive(() => ReadActive() is { } s && (s.Status == "recovery" || s.Id != runningSessionId));
    private void SetStatus(string id, string status, string? error = null, bool ended = false)
    {
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "UPDATE game_sessions SET status=$status,error=$error,ended=$ended WHERE id=$id";
        q.Parameters.AddWithValue("$status", status); q.Parameters.AddWithValue("$id", id);
        q.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        q.Parameters.AddWithValue("$ended", ended ? DateTimeOffset.Now.ToString("O") : DBNull.Value);
        q.ExecuteNonQuery();
    }
    public GameSessionRecord Start(string processKey, string name, string? powerPlan = null, bool priority = true, bool animations = false) => optimizer.ExecuteExclusive(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!processKey.StartsWith("priority:", StringComparison.Ordinal)) throw new ArgumentException("Identidade de processo inválida.", nameof(processKey));
        if (ReadActive() is not null) throw new InvalidOperationException("Termina e repõe a sessão anterior antes de iniciar outra.");
        var currentPriority = resolve(processKey).Read();
        if (currentPriority == "ended") throw new InvalidOperationException("A aplicação já terminou.");
        if (priority && currentPriority is not ("Normal" or "AboveNormal")) throw new InvalidOperationException("A aplicação já tem uma prioridade diferente. Seleciona uma aplicação com prioridade Normal.");
        var id = Guid.NewGuid().ToString("N");
        using (var c = Open())
        using (var q = c.CreateCommand())
        {
            q.CommandText = "INSERT INTO game_sessions(id,process_key,name,started,status) VALUES($id,$key,$name,$started,'starting')";
            q.Parameters.AddWithValue("$id", id); q.Parameters.AddWithValue("$key", processKey);
            q.Parameters.AddWithValue("$name", name); q.Parameters.AddWithValue("$started", DateTimeOffset.Now.ToString("O"));
            q.ExecuteNonQuery();
        }
        runningSessionId = id;
        try
        {
            if (powerPlan is not null) optimizer.ApplyOwned(id, "power", powerPlan);
            if (priority) optimizer.ApplyOwned(id, processKey, "AboveNormal");
            if (animations) optimizer.ApplyOwned(id, "animations", "0");
            SetStatus(id, "active");
        }
        catch (Exception e)
        {
            SetStatus(id, "recovery", "O início ficou incompleto. Termina e repõe a sessão. " + e.Message);
        }
        return ReadActive()!;
    });
    public GameSessionRecord? End() => optimizer.ExecuteExclusive(EndCore);
    private GameSessionRecord? EndCore()
    {
        var session = ReadActive();
        if (session is null) return null;
        SetStatus(session.Id, "ending");
        var errors = new List<string>();
        foreach (var change in optimizer.History().Where(x => x.Owner == session.Id && x.State is "applied" or "pending"))
        {
            try { optimizer.Undo(change.Id); }
            catch (Exception e) { errors.Add(change.Name + ": " + e.Message); }
        }
        var remaining = optimizer.History().Any(x => x.Owner == session.Id && x.State is "applied" or "pending");
        if (remaining)
        {
            SetStatus(session.Id, "recovery", errors.Count == 0 ? "Há definições por repor. Tenta Terminar e repor novamente." : string.Join(" ", errors));
        }
        else
        {
            SetStatus(session.Id, "ended", ended: true);
            runningSessionId = null;
        }
        return ReadHistory().First(x => x.Id == session.Id);
    }
    // Call from the app's monitor. Recovered sessions require explicit End; only a
    // session started by this live instance may end automatically when its process exits.
    public GameSessionRecord? Poll() => optimizer.ExecuteExclusive(() =>
    {
        var session = ReadActive();
        if (session is null || session.Id != runningSessionId || session.Status != "active") return session;
        try
        {
            if (resolve(session.ProcessKey).Read() == "ended") return EndCore();
        }
        catch (Exception e) { SetStatus(session.Id, "recovery", "Não foi possível acompanhar o processo. Usa Terminar e repor. " + e.Message); }
        return ReadActive();
    });
}
