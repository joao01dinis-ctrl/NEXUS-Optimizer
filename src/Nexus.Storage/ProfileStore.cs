using Microsoft.Data.Sqlite;
using Nexus.Core;
namespace Nexus.Storage;

public sealed class ProfileStore : IProfileStore
{
    private readonly string connectionString;
    public ProfileStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
  PRAGMA journal_mode=WAL;
  CREATE TABLE IF NOT EXISTS profiles(name TEXT PRIMARY KEY, seconds INTEGER NOT NULL, description TEXT NOT NULL);
  CREATE TABLE IF NOT EXISTS settings(id INTEGER PRIMARY KEY CHECK(id=1), profile TEXT NOT NULL);
  INSERT OR IGNORE INTO settings VALUES(1,'Normal');
  CREATE TABLE IF NOT EXISTS history(id INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, before_profile TEXT NOT NULL, after_profile TEXT NOT NULL, undone INTEGER NOT NULL DEFAULT 0);
  PRAGMA user_version=1;
  """;
        cmd.ExecuteNonQuery();
        foreach (var p in Profiles.All)
        {
            using var q = c.CreateCommand();
            q.CommandText = "INSERT OR REPLACE INTO profiles VALUES($n,$s,$d)";
            q.Parameters.AddWithValue("$n", p.Name);
            q.Parameters.AddWithValue("$s", p.SampleSeconds);
            q.Parameters.AddWithValue("$d", p.Description);
            q.ExecuteNonQuery();
        }
    }
    private SqliteConnection Open()
    {
        var c = new SqliteConnection(connectionString);
        c.Open();
        return c;
    }
    public string Current
    {
        get
        {
            using var c = Open();
            using var q = c.CreateCommand();
            q.CommandText = "SELECT profile FROM settings WHERE id=1";
            return (string)q.ExecuteScalar()!;
        }
    }
    public IReadOnlyList<HistoryEntry> History()
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT id,at,before_profile,after_profile,undone FROM history ORDER BY id DESC LIMIT 100";
        using var reader = q.ExecuteReader();
        var list = new List<HistoryEntry>();
        while (reader.Read())
            list.Add(new(reader.GetInt64(0), DateTimeOffset.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4)));
        return list;
    }
    public void Apply(string profile)
    {
        Profiles.Get(profile);
        using var c = Open();
        using var tx = c.BeginTransaction();
        using var q = c.CreateCommand();
        q.Transaction = tx;
        q.CommandText = "SELECT profile FROM settings WHERE id=1";
        var before = (string)q.ExecuteScalar()!;
        if (before == profile)
        {
            tx.Commit();
            return;
        }
        q.CommandText = "INSERT INTO history(at,before_profile,after_profile) VALUES($at,$before,$after); UPDATE settings SET profile=$after WHERE id=1";
        q.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        q.Parameters.AddWithValue("$before", before);
        q.Parameters.AddWithValue("$after", profile);
        q.ExecuteNonQuery();
        tx.Commit();
    }
    public bool Undo()
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        using var q = c.CreateCommand();
        q.Transaction = tx;
        q.CommandText = "SELECT id,before_profile,after_profile FROM history WHERE undone=0 ORDER BY id DESC LIMIT 1";
        long id;
        string before, after;
        using (var reader = q.ExecuteReader())
        {
            if (!reader.Read())
            {
                tx.Commit();
                return false;
            }
            id = reader.GetInt64(0);
            before = reader.GetString(1);
            after = reader.GetString(2);
        }
        q.CommandText = "SELECT profile FROM settings WHERE id=1";
        if ((string)q.ExecuteScalar()! != after)
            throw new InvalidOperationException("O estado mudou; undo cancelado.");
        q.CommandText = "UPDATE settings SET profile=$before WHERE id=1; UPDATE history SET undone=1 WHERE id=$id";
        q.Parameters.AddWithValue("$before", before);
        q.Parameters.AddWithValue("$id", id);
        q.ExecuteNonQuery();
        tx.Commit();
        return true;
    }
}
