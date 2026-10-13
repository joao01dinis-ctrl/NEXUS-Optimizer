using Microsoft.Data.Sqlite;
using Nexus.Benchmark;
using System.Text.Json;

namespace Nexus.Storage;

public sealed class BenchmarkStore
{
    private readonly string connectionString;
    public BenchmarkStore(string path)
    {
        var fullPath = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "CREATE TABLE IF NOT EXISTS benchmarks(id INTEGER PRIMARY KEY AUTOINCREMENT, result TEXT NOT NULL)"; q.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public void Save(BenchmarkResult result)
    {
        if (!LocalBenchmark.Valid(result)) throw new ArgumentException("Resultado de teste inválido.", nameof(result));
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO benchmarks(result) VALUES($result)"; q.Parameters.AddWithValue("$result", JsonSerializer.Serialize(result)); q.ExecuteNonQuery();
    }
    public IReadOnlyList<BenchmarkResult> History(int limit = 20)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "SELECT result FROM benchmarks ORDER BY id DESC LIMIT $limit"; q.Parameters.AddWithValue("$limit", limit);
        using var r = q.ExecuteReader(); var results = new List<BenchmarkResult>();
        while (r.Read())
        {
            var value = JsonSerializer.Deserialize<BenchmarkResult>(r.GetString(0));
            if (value is not null && LocalBenchmark.Valid(value)) results.Add(value);
        }
        return results;
    }
}
