using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Nexus.Gaming;

public sealed record GameSessionOptions(string Name, IReadOnlyList<string> VisualKeys)
{
    public static IReadOnlySet<string> AllowedVisualKeys { get; } = new HashSet<string>(["animations", "menu-animation", "tooltip-animation", "combo-animation", "minimize-animation"], StringComparer.Ordinal);
    public static IReadOnlyList<GameSessionOptions> BuiltIn { get; } = Array.AsReadOnly(new GameSessionOptions[] {
        new("Padrão", Array.AsReadOnly(Array.Empty<string>())),
        new("Competitivo", Array.AsReadOnly(new[] { "animations", "menu-animation" })),
        new("Máximo", Array.AsReadOnly(new[] { "animations", "menu-animation", "tooltip-animation", "combo-animation", "minimize-animation" }))
    });
    public GameSessionOptions Freeze()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 60 || Name.Any(char.IsControl) || VisualKeys is null ||
            VisualKeys.Count > 5 || VisualKeys.Distinct(StringComparer.Ordinal).Count() != VisualKeys.Count ||
            VisualKeys.Any(k => !AllowedVisualKeys.Contains(k))) throw new ArgumentException("Perfil inválido. Só são aceites preferências visuais reversíveis.");
        return this with { VisualKeys = Array.AsReadOnly(VisualKeys.ToArray()) };
    }
    public string Summary => VisualKeys.Count == 0 ? "Mantém as definições do Windows; acompanha a sessão." :
        string.Join(", ", VisualKeys.Select(k => k switch { "animations" => "animações de conteúdo", "menu-animation" => "animação de menus",
            "tooltip-animation" => "animação de dicas", "combo-animation" => "animação de listas", _ => "animação ao minimizar" })) +
        ". Repõe os valores anteriores ao terminar. Sem energia, prioridades, ficheiros do jogo ou ganho de FPS presumido.";
    public override string ToString() => Name;
}
public sealed record GamePreference(string GameId, string ProfileName, bool Automatic = false);

public sealed class GameDiscoveryStore
{
    private readonly string connectionString;
    public GameDiscoveryStore(string database)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = database }.ToString();
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = """
            CREATE TABLE IF NOT EXISTS discovered_games(id TEXT PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS game_preferences(game_id TEXT PRIMARY KEY, profile TEXT NOT NULL, automatic INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS game_profile_definitions(name TEXT PRIMARY KEY COLLATE NOCASE, data TEXT NOT NULL);
            """;
        q.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public void Synchronize(GameDiscoveryResult result)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        // Do not discard the previous catalog on a partial/unavailable launcher.
        // Read() independently verifies existence; preferences survive uninstall.
        foreach (var game in result.Games)
        {
            using var q = c.CreateCommand(); q.Transaction = tx;
            q.CommandText = "INSERT INTO discovered_games(id,data) VALUES($id,$data) ON CONFLICT(id) DO UPDATE SET data=$data";
            q.Parameters.AddWithValue("$id", game.Id); q.Parameters.AddWithValue("$data", JsonSerializer.Serialize(game)); q.ExecuteNonQuery();
        }
        tx.Commit();
    }
    public IReadOnlyList<InstalledGame> Read()
    {
        using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT data FROM discovered_games";
        using var r = q.ExecuteReader(); var games = new List<InstalledGame>();
        while (r.Read())
        {
            try
            {
                var game = JsonSerializer.Deserialize<InstalledGame>(r.GetString(0));
                if (game is null || string.IsNullOrWhiteSpace(game.Id) || string.IsNullOrWhiteSpace(game.Name) || game.Executables is null ||
                    !GameDiscovery.SafeDirectory(game.Directory) || game.EvidenceFile is not null && !File.Exists(game.EvidenceFile)) continue;
                var executablePaths = game.Executables.Where(p => Path.IsPathFullyQualified(p) &&
                    GameDiscovery.ResolveInside(game.Directory, Path.GetRelativePath(game.Directory, p)) == p && File.Exists(p)).ToArray();
                games.Add(game with { Executables = Array.AsReadOnly(executablePaths) });
            }
            catch (Exception e) when (e is JsonException or ArgumentException or IOException or UnauthorizedAccessException) { }
        }
        return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public IReadOnlyList<GameSessionOptions> Profiles()
    {
        var profiles = GameSessionOptions.BuiltIn.ToList();
        using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT data FROM game_profile_definitions ORDER BY name";
        using var r = q.ExecuteReader();
        while (r.Read()) try
        {
            if (JsonSerializer.Deserialize<GameSessionOptions>(r.GetString(0)) is { } p &&
                !profiles.Any(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))) profiles.Add(p.Freeze());
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { }
        return profiles.AsReadOnly();
    }
    public void SaveProfile(GameSessionOptions profile)
    {
        var frozen = profile.Freeze();
        if (GameSessionOptions.BuiltIn.Any(p => p.Name.Equals(frozen.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Escolhe um nome próprio para o perfil personalizado.");
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO game_profile_definitions(name,data) VALUES($name,$data) ON CONFLICT(name) DO UPDATE SET data=$data";
        q.Parameters.AddWithValue("$name", frozen.Name); q.Parameters.AddWithValue("$data", JsonSerializer.Serialize(frozen)); q.ExecuteNonQuery();
    }
    public GamePreference Preference(InstalledGame game)
    {
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "SELECT profile,automatic FROM game_preferences WHERE game_id=$id"; q.Parameters.AddWithValue("$id", game.Id);
        using var r = q.ExecuteReader();
        return r.Read() ? new(game.Id, r.GetString(0), r.GetInt32(1) == 1) : new(game.Id, game.RecommendedProfile);
    }
    public void SavePreference(GamePreference preference)
    {
        if (!Profiles().Any(p => p.Name.Equals(preference.ProfileName, StringComparison.Ordinal)))
            throw new ArgumentException("Perfil desconhecido.");
        if (!Read().Any(g => g.Id == preference.GameId)) throw new ArgumentException("O jogo já não está instalado ou acessível.");
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO game_preferences(game_id,profile,automatic) VALUES($id,$profile,$auto) ON CONFLICT(game_id) DO UPDATE SET profile=$profile,automatic=$auto";
        q.Parameters.AddWithValue("$id", preference.GameId); q.Parameters.AddWithValue("$profile", preference.ProfileName);
        q.Parameters.AddWithValue("$auto", preference.Automatic ? 1 : 0); q.ExecuteNonQuery();
    }
    public static string ExportProfile(GameSessionOptions profile) => JsonSerializer.Serialize(profile.Freeze(), new JsonSerializerOptions { WriteIndented = true });
    public static GameSessionOptions ImportProfile(string json)
    {
        if (json.Length > 16384) throw new ArgumentException("Ficheiro de perfil demasiado grande.");
        return (JsonSerializer.Deserialize<GameSessionOptions>(json, new JsonSerializerOptions
        { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 }) ?? throw new ArgumentException("Perfil vazio.")).Freeze();
    }
}
