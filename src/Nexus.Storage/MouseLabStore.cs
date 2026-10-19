using Microsoft.Data.Sqlite;
using Nexus.Core;
using System.Text.Json;

namespace Nexus.Storage;

public sealed record MouseLabEntry(long Id, DateTimeOffset At, string Kind, string Label, string Json);
public sealed record MouseProfileBackup(int Schema, MouseProfile[] Profiles, Dictionary<string,string>? GameAssignments=null);
public sealed class MouseLabStore
{
    private readonly string connectionString;
    public const int MaxImportBytes = 262144;
    public MouseLabStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource=path }.ToString();
        using var c=Open(); using var q=c.CreateCommand();
        // Additive schema: intentionally independent of the existing profile store's user_version.
        q.CommandText="""
            CREATE TABLE IF NOT EXISTS mouse_lab_profiles(name TEXT PRIMARY KEY, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS mouse_lab_events(id INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, kind TEXT NOT NULL, label TEXT NOT NULL, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS mouse_lab_metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            INSERT OR IGNORE INTO mouse_lab_metadata VALUES('schema','1');
            """; q.ExecuteNonQuery();
        foreach(var profile in MouseProfile.Defaults)
        {
            q.CommandText="INSERT OR IGNORE INTO mouse_lab_profiles VALUES($name,$json)"; q.Parameters.Clear();
            q.Parameters.AddWithValue("$name",profile.Name); q.Parameters.AddWithValue("$json",JsonSerializer.Serialize(profile)); q.ExecuteNonQuery();
        }
    }
    private SqliteConnection Open() { var c=new SqliteConnection(connectionString); c.Open(); return c; }
    public IReadOnlyList<MouseProfile> Profiles()
    {
        using var c=Open(); using var q=c.CreateCommand(); q.CommandText="SELECT json FROM mouse_lab_profiles ORDER BY rowid";
        using var r=q.ExecuteReader(); var result=new List<MouseProfile>();
        while(r.Read()) { var p=JsonSerializer.Deserialize<MouseProfile>(r.GetString(0)) ?? throw new InvalidDataException("Perfil vazio."); p.Validate(); result.Add(p); } return result;
    }
    public void Save(MouseProfile profile)
    {
        profile.Validate(); using var c=Open();using var tx=c.BeginTransaction(); using var q=c.CreateCommand();q.Transaction=tx;
        q.CommandText="INSERT INTO mouse_lab_profiles VALUES($n,$j) ON CONFLICT(name) DO UPDATE SET json=$j";
        q.Parameters.AddWithValue("$n",profile.Name); q.Parameters.AddWithValue("$j",JsonSerializer.Serialize(profile)); q.ExecuteNonQuery();
        if(profile.Game is "CS2" or "FiveM")
        {q.CommandText="INSERT INTO mouse_lab_metadata VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v";q.Parameters.Clear();q.Parameters.AddWithValue("$k","game-profile-"+profile.Game);q.Parameters.AddWithValue("$v",profile.Name);q.ExecuteNonQuery();}
        tx.Commit();
    }
    public IReadOnlyList<MouseProfile> GameProfiles()
    {
        var profiles=Profiles();var result=new List<MouseProfile>();using var c=Open();
        foreach(var game in new[]{"CS2","FiveM"})
        {using var q=c.CreateCommand();q.CommandText="SELECT value FROM mouse_lab_metadata WHERE key=$k";q.Parameters.AddWithValue("$k","game-profile-"+game);var name=q.ExecuteScalar() as string;var profile=profiles.FirstOrDefault(p=>p.Game==game&&p.Name==name)??profiles.FirstOrDefault(p=>p.Game==game);if(profile is not null)result.Add(profile);}
        return result;
    }
    public long Record<T>(string kind,string label,T value)
    {
        if (kind is not ("analysis" or "training" or "health" or "sensitivity") || string.IsNullOrWhiteSpace(label) || label.Length>160)
            throw new ArgumentException("Tipo de registo inválido.");
        var json=JsonSerializer.Serialize(value);
        if(System.Text.Encoding.UTF8.GetByteCount(json)>MaxImportBytes) throw new ArgumentException("Registo demasiado grande.");
        using var c=Open(); using var tx=c.BeginTransaction(); using var q=c.CreateCommand(); q.Transaction=tx;
        q.CommandText="INSERT INTO mouse_lab_events(at,kind,label,json) VALUES($a,$k,$l,$j); SELECT last_insert_rowid()";
        q.Parameters.AddWithValue("$a",DateTimeOffset.UtcNow.ToString("O"));q.Parameters.AddWithValue("$k",kind);q.Parameters.AddWithValue("$l",label);q.Parameters.AddWithValue("$j",json);
        var id=(long)q.ExecuteScalar()!;
        q.CommandText="DELETE FROM mouse_lab_events WHERE id NOT IN (SELECT id FROM mouse_lab_events ORDER BY id DESC LIMIT 2000)";q.Parameters.Clear();q.ExecuteNonQuery();tx.Commit();return id;
    }
    public IReadOnlyList<MouseLabEntry> History(string? kind=null)
    {
        using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT id,at,kind,label,json FROM mouse_lab_events WHERE ($k IS NULL OR kind=$k) ORDER BY id DESC LIMIT 2000";
        q.Parameters.AddWithValue("$k",(object?)kind??DBNull.Value);using var r=q.ExecuteReader();var result=new List<MouseLabEntry>();
        while(r.Read())result.Add(new(r.GetInt64(0),DateTimeOffset.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4)));return result;
    }
    public string ExportProfiles()=>JsonSerializer.Serialize(new MouseProfileBackup(1,Profiles().ToArray(),GameProfiles().ToDictionary(p=>p.Game,p=>p.Name)),new JsonSerializerOptions{WriteIndented=true});
    public void ImportProfiles(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json)>MaxImportBytes) throw new InvalidDataException("Backup superior a 256 KiB.");
        var backup=JsonSerializer.Deserialize<MouseProfileBackup>(json,new JsonSerializerOptions{MaxDepth=12}) ?? throw new InvalidDataException("Backup vazio.");
        if(backup.Schema!=1 || backup.Profiles is null || backup.Profiles.Length is <1 or >100 || backup.Profiles.Any(p=>p is null) || backup.Profiles.Select(p=>p.Name).Distinct(StringComparer.Ordinal).Count()!=backup.Profiles.Length)
            throw new InvalidDataException("Formato, versão ou nomes duplicados no backup.");
        foreach(var p in backup.Profiles)p.Validate();
        if(backup.GameAssignments is {} assignments && (assignments.Count>2 || assignments.Any(a=>a.Key is not ("CS2" or "FiveM") || !backup.Profiles.Any(p=>p.Name==a.Value && p.Game==a.Key))))
            throw new InvalidDataException("Associação de jogo inválida no backup.");
        // All validation precedes the transaction: a malformed import cannot partially replace profiles.
        using var c=Open();using var tx=c.BeginTransaction();
        foreach(var p in backup.Profiles)
        {
            using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO mouse_lab_profiles VALUES($n,$j) ON CONFLICT(name) DO UPDATE SET json=$j";
            q.Parameters.AddWithValue("$n",p.Name);q.Parameters.AddWithValue("$j",JsonSerializer.Serialize(p));q.ExecuteNonQuery();
        }
        foreach(var a in backup.GameAssignments??[])
        {using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO mouse_lab_metadata VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v";q.Parameters.AddWithValue("$k","game-profile-"+a.Key);q.Parameters.AddWithValue("$v",a.Value);q.ExecuteNonQuery();}
        tx.Commit();
    }
}
