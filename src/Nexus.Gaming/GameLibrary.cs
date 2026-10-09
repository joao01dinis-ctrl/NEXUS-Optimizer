using Microsoft.Data.Sqlite;
using System.ComponentModel;
using System.Diagnostics;

namespace Nexus.Gaming;

public sealed record LibraryApp(string Path, string Name)
{
    public string Display => Name + "\n" + Path;
}
public sealed record DetectedApp(LibraryApp App, int Id, long Started)
{
    public string ProcessKey => $"priority:{Id}:{Started}";
    public string Display => $"{App.Name} · PID {Id}";
}
public sealed record GameProfile(string Name, bool Priority, bool Animations, string Description)
{
    public override string ToString() => Name;
}
public sealed class GameLibrary
{
    private readonly string connectionString;
    public static IReadOnlyList<GameProfile> Profiles { get; } = Array.AsReadOnly(new GameProfile[] {
        new("Balanceado", false, false, "Só acompanha a sessão; os ajustes são opcionais."),
        new("Competitivo", true, true, "Prioridade acima do normal e menos animações, com reposição. Não mede ganhos de FPS."),
        new("Extremo", true, true, "Usa os mesmos limites seguros do Competitivo. Não desativa proteções, core parking ou serviços."),
        new("Streaming", false, true, "Mantém prioridade normal para partilhar CPU com a transmissão; reduz animações durante a sessão."),
        new("Personalizado", true, false, "Revê prioridade, animações e plano disponível para esta sessão.")
    });
    public GameLibrary(string database)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = database }.ToString();
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "CREATE TABLE IF NOT EXISTS app_library(path TEXT PRIMARY KEY COLLATE NOCASE, name TEXT NOT NULL)"; q.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public LibraryApp Add(string path)
    {
        var full = ValidatePath(path);
        var app = new LibraryApp(full, System.IO.Path.GetFileNameWithoutExtension(full));
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO app_library(path,name) VALUES($p,$n) ON CONFLICT(path) DO NOTHING";
        q.Parameters.AddWithValue("$p", app.Path); q.Parameters.AddWithValue("$n", app.Name); q.ExecuteNonQuery(); return app;
    }
    public static string ValidatePath(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || path.IndexOf(':', 2) >= 0) throw new ArgumentException("Escolhe um executável local.");
        var full = System.IO.Path.GetFullPath(path);
        if (!string.Equals(System.IO.Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) throw new ArgumentException("O executável escolhido não existe.");
        return full;
    }
    public IReadOnlyList<LibraryApp> Read()
    {
        using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT path,name FROM app_library ORDER BY name";
        using var r = q.ExecuteReader(); var apps = new List<LibraryApp>(); while (r.Read()) apps.Add(new(r.GetString(0), r.GetString(1))); return apps;
    }
    public void Remove(string path)
    {
        using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "DELETE FROM app_library WHERE path=$p"; q.Parameters.AddWithValue("$p", path); q.ExecuteNonQuery();
    }
    public static IReadOnlyList<DetectedApp> Detect(IReadOnlyList<LibraryApp> apps)
    {
        var result = new List<DetectedApp>(); using var self = Process.GetCurrentProcess();
        foreach (var p in Process.GetProcesses())
        {
            using (p) try
            {
                if (p.Id == self.Id || p.SessionId != self.SessionId || p.HasExited) continue;
                var path = p.MainModule?.FileName;
                var app = apps.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
                if (app is not null) result.Add(new(app, p.Id, p.StartTime.ToUniversalTime().Ticks));
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException) { }
        }
        return result;
    }
}
