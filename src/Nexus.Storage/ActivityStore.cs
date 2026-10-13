using Microsoft.Data.Sqlite;
namespace Nexus.Storage;

public sealed record ActivityEntry(string At, string Area, string Message)
{
    public string Display => (DateTimeOffset.TryParse(At, out var time) ? time.ToString("dd/MM HH:mm") : At) + " · " + Area + "\n" + Message;
}
public sealed class ActivityStore
{
    private readonly string connectionString;
    public ActivityStore(string database)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = database }.ToString();
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "CREATE TABLE IF NOT EXISTS activity_events(id INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, area TEXT NOT NULL, message TEXT NOT NULL)"; q.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public void Save(string area, string message)
    {
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO activity_events(at,area,message) VALUES($at,$area,$message)";
        q.Parameters.AddWithValue("$at", DateTimeOffset.Now.ToString("O")); q.Parameters.AddWithValue("$area", area); q.Parameters.AddWithValue("$message", message); q.ExecuteNonQuery();
    }
    public IReadOnlyList<ActivityEntry> History()
    {
        using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT at,area,message FROM activity_events ORDER BY id DESC LIMIT 80";
        using var r = q.ExecuteReader(); var entries = new List<ActivityEntry>();
        while (r.Read()) entries.Add(new(r.GetString(0), r.GetString(1), r.GetString(2))); return entries;
    }
}
