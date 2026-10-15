using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Nexus.Gaming;

public sealed record InstalledGame(string Id, string Name, string Launcher, string Directory,
    string? EvidenceFile, IReadOnlyList<string> Executables)
{
    public bool CanTrack => Executables.Count > 0;
    public string RecommendedProfile => Name.Contains("Minecraft", StringComparison.OrdinalIgnoreCase) ? "Padrão" : "Competitivo";
    public string InstallationState => CanTrack ? "Instalação e executável encontrados" : "Instalação encontrada; processo não identificado";
}
public sealed record RegisteredGame(string Id, string Name, string Launcher, string Directory, string? Executable = null);
public sealed record GameDiscoveryLocations(
    IReadOnlyList<string> SteamRoots, IReadOnlyList<string> EpicManifests,
    IReadOnlyList<string> RiotRoots, IReadOnlyList<string> XboxRoots,
    IReadOnlyList<RegisteredGame> Registered, IReadOnlyList<string> StorePackages,
    string LocalAppData, string AppData);
public sealed record GameDiscoveryResult(IReadOnlyList<InstalledGame> Games, IReadOnlyList<string> Warnings, DateTimeOffset At);

// Reads bounded launcher metadata only. Never executes a launcher, changes a game
// file, searches an entire disk or follows junctions into another directory.
public static class GameDiscovery
{
    private const int MaxMetadataBytes = 2 * 1024 * 1024;
    private const int MaxEntries = 512;
    public static GameDiscoveryResult Scan(GameDiscoveryLocations locations, CancellationToken token = default)
    {
        var games = new Dictionary<string, InstalledGame>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        void Guard(string source, Action action)
        {
            token.ThrowIfCancellationRequested();
            try { action(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or
                JsonException or XmlException or InvalidDataException or System.Security.SecurityException)
            { warnings.Add(source + ": não foi possível ler todos os dados. " + e.GetType().Name); }
        }
        void Add(string id, string name, string launcher, string directory, string? evidence, string? declaredExe = null)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || !SafeDirectory(directory)) return;
            var root = Path.GetFullPath(directory);
            var candidates = KnownExecutables(name).Concat(declaredExe is null ? [] : new[] { declaredExe });
            var exes = candidates.Select(p => ResolveInside(root, p)).OfType<string>()
                .Where(p => Path.GetExtension(p).Equals(".exe", StringComparison.OrdinalIgnoreCase) && SafeFile(p))
                .Where(p => !Regex.IsMatch(Path.GetFileNameWithoutExtension(p), @"^(unins|uninstall|setup|install|touchup|crash|riotclientservices|epicgameslauncher)", RegexOptions.IgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
            var stable = launcher + ":" + id + ":" + root.ToUpperInvariant();
            games[stable] = new(stable, name.Trim(), launcher, root, evidence, Array.AsReadOnly(exes));
        }
        foreach (var steam in locations.SteamRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steam };
            Guard("Bibliotecas Steam", () =>
            {
                var file = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (ReadMetadata(file) is not string text) return;
                foreach (var pair in ParseKeyValues(text))
                    if ((pair.Key.Equals("path", StringComparison.OrdinalIgnoreCase) || int.TryParse(pair.Key, out _)) &&
                        Path.IsPathFullyQualified(pair.Value) && SafeDirectory(pair.Value)) libraries.Add(pair.Value);
            });
            foreach (var library in libraries)
                Guard("Steam", () =>
                {
                    foreach (var manifest in Files(Path.Combine(library, "steamapps"), "appmanifest_*.acf"))
                        Guard("Manifesto Steam", () =>
                        {
                            var values = ParseKeyValues(ReadMetadata(manifest) ?? "").GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(p => p.Key, p => p.Last().Value, StringComparer.OrdinalIgnoreCase);
                            if (values.TryGetValue("name", out var name) && values.TryGetValue("installdir", out var install) &&
                                values.TryGetValue("appid", out var appId) && ResolveInside(Path.Combine(library, "steamapps", "common"), install) is string root)
                                Add(appId, name, "Steam", root, manifest);
                        });
                });
        }
        foreach (var manifests in locations.EpicManifests)
            Guard("Epic Games", () =>
            {
                foreach (var file in Files(manifests, "*.item"))
                    Guard("Manifesto Epic", () =>
                    {
                        using var doc = JsonDocument.Parse(ReadMetadata(file) ?? "{}");
                        var r = doc.RootElement;
                        if (r.ValueKind != JsonValueKind.Object) throw new JsonException("Manifesto Epic inválido.");
                        if (r.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True) return;
                        var name = Text(r, "DisplayName"); var install = Text(r, "InstallLocation");
                        if (name is not null && install is not null)
                            Add(Text(r, "AppName") ?? name, name, "Epic Games", install, file, Text(r, "LaunchExecutable"));
                    });
            });
        foreach (var riot in locations.RiotRoots)
            Guard("Riot Games", () =>
            {
                var clients = Path.Combine(riot, "RiotClientInstalls.json");
                if (ReadMetadata(clients) is string content)
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Manifesto Riot inválido.");
                    if (doc.RootElement.TryGetProperty("associated_client", out var apps) && apps.ValueKind == JsonValueKind.Object)
                        foreach (var app in apps.EnumerateObject().Take(MaxEntries))
                            if (SafeFile(app.Name) && Path.GetFileName(app.Name).Equals("VALORANT.exe", StringComparison.OrdinalIgnoreCase))
                                Add("valorant", "Valorant", "Riot Games", Path.GetDirectoryName(app.Name)!, clients, Path.GetFileName(app.Name));
                }
                foreach (var directory in Directories(Path.Combine(riot, "Metadata")))
                    foreach (var file in Files(directory, "*.product_settings.yaml"))
                    {
                        var contentText = ReadMetadata(file);
                        if (contentText is null) continue;
                        var match = Regex.Match(contentText, @"(?m)^\s*product_install_full_path:\s*[""']?([^""'\r\n]+)", RegexOptions.CultureInvariant);
                        if (match.Success && Path.GetFileName(directory).StartsWith("valorant.", StringComparison.OrdinalIgnoreCase))
                            Add("valorant", "Valorant", "Riot Games", match.Groups[1].Value.Trim(), file);
                    }
            });
        void ReadMicrosoftGame(string directory, string launcher)
        {
            var config = Path.Combine(directory, "MicrosoftGame.config");
            if (ReadMetadata(config) is not string content) return;
            using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxMetadataBytes });
            var doc = XDocument.Load(reader);
            var name = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals")?.Attribute("DefaultDisplayName")?.Value ??
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "DefaultDisplayName")?.Value ??
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Identity")?.Attribute("Name")?.Value ?? Path.GetFileName(directory);
            if (name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)) name = Path.GetFileName(Path.GetDirectoryName(directory)) ?? Path.GetFileName(directory);
            var exe = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Executable")?.Attribute("Name")?.Value;
            Add(name, name, launcher, directory, config, exe);
        }
        foreach (var xbox in locations.XboxRoots)
            Guard("Xbox App", () =>
            {
                foreach (var game in Directories(xbox))
                    Guard("Manifesto Xbox", () => ReadMicrosoftGame(Path.Combine(game, "Content"), "Xbox App"));
            });
        foreach (var package in locations.StorePackages)
            Guard("Microsoft Store", () =>
            {
                ReadMicrosoftGame(package, "Microsoft Store");
                var folder = Path.GetFileName(package);
                var name = folder.Contains("Minecraft", StringComparison.OrdinalIgnoreCase) ? "Minecraft" :
                    folder.Contains("ROBLOX", StringComparison.OrdinalIgnoreCase) ? "Roblox" : null;
                if (name is null || ReadMetadata(Path.Combine(package, "AppxManifest.xml")) is not string manifest) return;
                using var reader = XmlReader.Create(new StringReader(manifest), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxMetadataBytes });
                var doc = XDocument.Load(reader);
                var executable = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Application")?.Attribute("Executable")?.Value;
                Add(folder, name, "Microsoft Store", package, Path.Combine(package, "AppxManifest.xml"), executable);
            });
        foreach (var registered in locations.Registered.Take(MaxEntries))
            Guard(registered.Launcher, () => Add(registered.Id, registered.Name, registered.Launcher, registered.Directory, null, registered.Executable));
        Guard("FiveM", () => { var root = Path.Combine(locations.LocalAppData, "FiveM"); if (SafeFile(Path.Combine(root, "FiveM.exe"))) Add("fivem", "FiveM", "Cfx.re", root, null, "FiveM.exe"); });
        Guard("Roblox", () =>
        {
            foreach (var version in Directories(Path.Combine(locations.LocalAppData, "Roblox", "Versions")))
                if (SafeFile(Path.Combine(version, "RobloxPlayerBeta.exe"))) Add("roblox", "Roblox", "Roblox", version, null, "RobloxPlayerBeta.exe");
        });
        Guard("Minecraft Java", () =>
        {
            var root = Path.Combine(locations.AppData, ".minecraft");
            if (Directories(Path.Combine(root, "versions")).Any(v => Files(v, "*.jar").Any()))
                Add("minecraft-java", "Minecraft Java", "Minecraft Launcher", root, null);
        });
        // One installation may be described by several manifests. Keep the richest
        // observation, while preserving distinct installs on different drives.
        var merged = games.Values.GroupBy(g => g.Directory, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Executables.Count).First())
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        return new(Array.AsReadOnly(merged), warnings.AsReadOnly(), DateTimeOffset.Now);
    }
    private static string? Text(JsonElement e, string key) =>
        e.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static IEnumerable<string> KnownExecutables(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("counter-strike")) return [@"game\bin\win64\cs2.exe"];
        if (n.Contains("grand theft auto") || n.Contains("gta v")) return ["GTA5.exe", "GTA5_Enhanced.exe"];
        if (n.Contains("valorant")) return ["VALORANT.exe", @"ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe"];
        if (n.Contains("fortnite")) return [@"FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe"];
        if (n.Contains("fivem")) return ["FiveM.exe"];
        if (n.Contains("roblox")) return ["RobloxPlayerBeta.exe"];
        return [];
    }
    public static string? ResolveInside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathFullyQualified(relative) || relative.Contains(':')) return null;
        var parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(parent, relative));
        return full.StartsWith(parent, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
    internal static bool SafeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || path.IndexOf(':', 2) >= 0 || !System.IO.Directory.Exists(path)) return false;
        for (var item = new DirectoryInfo(path); item is not null; item = item.Parent)
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }
    private static bool SafeFile(string path) => File.Exists(path) && SafeDirectory(Path.GetDirectoryName(path)!) &&
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
    private static IEnumerable<string> Files(string root, string pattern) =>
        SafeDirectory(root) ? System.IO.Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly).Take(MaxEntries).Where(SafeFile) : [];
    private static IEnumerable<string> Directories(string root) =>
        SafeDirectory(root) ? System.IO.Directory.EnumerateDirectories(root).Take(MaxEntries).Where(SafeDirectory) : [];
    private static string? ReadMetadata(string path)
    {
        if (!SafeFile(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaxMetadataBytes) throw new InvalidDataException("Manifesto demasiado grande.");
        using var reader = new StreamReader(stream);
        var content = new System.Text.StringBuilder((int)Math.Min(stream.Length, 8192));
        var buffer = new char[4096]; int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (content.Length + count > MaxMetadataBytes) throw new InvalidDataException("Manifesto demasiado grande.");
            content.Append(buffer, 0, count);
        }
        return content.ToString();
    }
    // Valve KeyValues: scalar pairs are retained at every nesting level. Parsing
    // is bounded; escape handling does not interpret paths as commands.
    public static IReadOnlyList<KeyValuePair<string, string>> ParseKeyValues(string text)
    {
        if (text.Length > MaxMetadataBytes) throw new InvalidDataException("Manifesto demasiado grande.");
        var tokens = Regex.Matches(text, @"//[^\r\n]*|""((?:\\.|[^""\\])*)""|([{}])|([^\s{}""]+)");
        var values = new List<KeyValuePair<string, string>>(); string? key = null;
        foreach (Match token in tokens)
        {
            if (token.Value.StartsWith("//", StringComparison.Ordinal)) continue;
            var value = token.Groups[1].Success ? token.Groups[1].Value.Replace(@"\\", @"\").Replace("\\\"", "\"") : token.Value;
            if (value is "{" or "}") { key = null; continue; }
            if (key is null) key = value;
            else { values.Add(new(key, value)); key = null; }
        }
        return values;
    }
}
