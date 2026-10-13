using Microsoft.Data.Sqlite;
using Nexus.Benchmark;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nexus.Storage;

public sealed class FrameTimeStore
{
    private readonly string connectionString;
    public FrameTimeStore(string path)
    {
        var fullPath = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "CREATE TABLE IF NOT EXISTS frame_captures(id INTEGER PRIMARY KEY AUTOINCREMENT, identity TEXT NOT NULL UNIQUE, result TEXT NOT NULL)";
        q.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public void Save(FpsCapture capture)
    {
        if (!FrameTimes.Valid(capture)) throw new ArgumentException("Captura inválida.", nameof(capture));
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Hash = capture.Sha256.ToUpperInvariant(), capture.Summary.Application, capture.Summary.ProcessId,
            capture.Summary.SwapChain, capture.Context }))));
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "INSERT OR IGNORE INTO frame_captures(identity,result) VALUES($identity,$result)";
        q.Parameters.AddWithValue("$identity", identity); q.Parameters.AddWithValue("$result", JsonSerializer.Serialize(capture));
        if (q.ExecuteNonQuery() != 1) throw new InvalidOperationException("Esta captura e estas condições já estão guardadas.");
    }
    public IReadOnlyList<FpsCapture> History(int limit = 30)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "SELECT result FROM frame_captures ORDER BY id DESC LIMIT $limit"; q.Parameters.AddWithValue("$limit", limit);
        using var reader = q.ExecuteReader(); var result = new List<FpsCapture>();
        while (reader.Read())
        {
            var capture = JsonSerializer.Deserialize<FpsCapture>(reader.GetString(0));
            if (capture is not null && FrameTimes.Valid(capture)) result.Add(capture);
        }
        return result;
    }
}
