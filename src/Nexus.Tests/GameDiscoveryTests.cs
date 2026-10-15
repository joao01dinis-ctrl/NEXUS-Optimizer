using Nexus.Gaming;
using Nexus.Network;
using Nexus.Optimization;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Xunit;

namespace Nexus.Tests;

public sealed class GameDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Nexus-discovery-" + Guid.NewGuid().ToString("N"));
    private string FileAt(string relative, string content = "")
    {
        var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content); return path;
    }
    private GameDiscoveryLocations Locations(params RegisteredGame[] registered) => new(
        [Path.Combine(root, "steam")], [Path.Combine(root, "epic")], [Path.Combine(root, "riot")],
        [Path.Combine(root, "xbox")], registered, [Path.Combine(root, "store")], Path.Combine(root, "local"), Path.Combine(root, "roaming"));
    private GameDiscoveryResult Scan() => GameDiscovery.Scan(Locations());
    [Fact] public void SteamDiscoversSecondaryLibraryAndRealKnownExecutable()
    {
        var extra = Path.Combine(root, "other-library");
        FileAt(@"other-library\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe");
        var manifest = FileAt(@"other-library\steamapps\appmanifest_730.acf", "\"AppState\" { \"appid\" \"730\" \"name\" \"Counter-Strike 2\" \"installdir\" \"Counter-Strike Global Offensive\" }");
        FileAt(@"steam\steamapps\libraryfolders.vdf", "\"libraryfolders\" { \"0\" { \"path\" \"" + extra.Replace(@"\", @"\\") + "\" } }");
        var game = Assert.Single(Scan().Games);
        Assert.Equal("Counter-Strike 2", game.Name); Assert.Equal("Steam", game.Launcher);
        Assert.Single(game.Executables); Assert.Equal(manifest, game.EvidenceFile);
    }
    [Fact] public void EpicManifestCannotAuthorizeAnExecutableOutsideItsInstallDirectory()
    {
        var install = Path.Combine(root, "epic-game"); Directory.CreateDirectory(install);
        FileAt("escape.exe");
        FileAt(@"epic\sample.item", JsonSerializer.Serialize(new { DisplayName = "Example Game", AppName = "sample", InstallLocation = install, LaunchExecutable = @"..\escape.exe" }));
        var game = Assert.Single(Scan().Games); Assert.False(game.CanTrack); Assert.Empty(game.Executables);
        Assert.Null(GameDiscovery.ResolveInside(install, @"..\escape.exe"));
        Assert.Null(GameDiscovery.ResolveInside(install, @"C:\escape.exe"));
        Assert.Null(GameDiscovery.ResolveInside(install, "game.exe:stream"));
    }
    [Fact] public void IncompleteEpicInstallIsNotReportedAsInstalled()
    {
        var install = Path.GetDirectoryName(FileAt(@"game\game.exe"))!;
        FileAt(@"epic\sample.item", JsonSerializer.Serialize(new { DisplayName = "Example Game", InstallLocation = install, LaunchExecutable = "game.exe", bIsIncompleteInstall = true }));
        Assert.Empty(Scan().Games);
    }
    [Fact] public void DamagedMetadataDoesNotHideOtherLaunchersGames()
    {
        FileAt(@"epic\broken.item", "{ broken");
        FileAt(@"local\FiveM\FiveM.exe");
        var result = Scan(); Assert.Contains(result.Games, g => g.Name == "FiveM"); Assert.NotEmpty(result.Warnings);
    }
    [Fact] public void RiotAssociationAndXboxManifestDoNotNeedManualExeSelection()
    {
        var valorant = FileAt(@"valorant\VALORANT.exe");
        FileAt(@"riot\RiotClientInstalls.json", JsonSerializer.Serialize(new { associated_client = new Dictionary<string, string> { [valorant] = @"C:\Riot Games\Riot Client\RiotClientServices.exe" } }));
        FileAt(@"xbox\Example\Content\game.exe");
        FileAt(@"xbox\Example\Content\MicrosoftGame.config", "<Game><Identity Name=\"Example.Game\"/><ShellVisuals DefaultDisplayName=\"Example Xbox Game\"/><ExecutableList><Executable Name=\"game.exe\"/></ExecutableList></Game>");
        var games = Scan().Games;
        Assert.Contains(games, g => g.Name == "Valorant" && g.CanTrack);
        Assert.Contains(games, g => g.Name == "Example Xbox Game" && g.CanTrack);
    }
    [Fact] public void StoreManifestAndRegisteredInstallationsAreReadWithoutLaunchingThem()
    {
        FileAt(@"store\MicrosoftGame.config", "<Game><Identity Name=\"Example.Store\"/><ExecutableList><Executable Name=\"store-game.exe\"/></ExecutableList></Game>");
        FileAt(@"store\store-game.exe");
        var installed = Path.GetDirectoryName(FileAt(@"ea\ea-game.exe"))!;
        var result = GameDiscovery.Scan(Locations(new RegisteredGame("ea-game", "Example EA Game", "EA App", installed, "ea-game.exe")));
        Assert.Contains(result.Games, g => g.Launcher == "Microsoft Store" && g.CanTrack);
        Assert.Contains(result.Games, g => g.Launcher == "EA App" && g.CanTrack);
    }
    [Fact] public void XmlExternalEntitiesAreRejected()
    {
        FileAt(@"xbox\Unsafe\Content\MicrosoftGame.config", "<!DOCTYPE Game [<!ENTITY outside SYSTEM \"file:///C:/secret.txt\">]><Game><ShellVisuals><DefaultDisplayName>&outside;</DefaultDisplayName></ShellVisuals></Game>");
        var result = Scan(); Assert.Empty(result.Games); Assert.NotEmpty(result.Warnings);
    }
    [Fact] public void CancelledDiscoveryDoesNotReturnPartialResultsForSynchronization()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => GameDiscovery.Scan(Locations(), cancel.Token));
    }
    [Fact] public void FiveMRuntimeMatchingRequiresTheAuthorizedRootAndExactGameProcessPattern()
    {
        var app = new LibraryApp(@"C:\Games\FiveM\FiveM.exe", "FiveM") { RuntimeRoot = @"C:\Games\FiveM" };
        Assert.True(GameLibrary.MatchesRuntime(app, @"C:\Games\FiveM\FiveM.app\data\cache\subprocess\FiveM_b3095_GTAProcess.exe"));
        Assert.False(GameLibrary.MatchesRuntime(app, @"D:\Other\FiveM_b3095_GTAProcess.exe"));
        Assert.False(GameLibrary.MatchesRuntime(app, @"C:\Games\FiveM\FiveM_b3095_GTAProcessEvil.exe"));
        Assert.False(GameLibrary.MatchesRuntime(app, @"C:\Games\FiveM\RiotClientServices.exe"));
    }
    [Fact] public void PerGamePreferencesAndCustomProfilesPersistWithoutApplyingAnything()
    {
        FileAt(@"local\FiveM\FiveM.exe");
        var database = Path.Combine(root, "library.db"); var store = new GameDiscoveryStore(database);
        var result = Scan(); store.Synchronize(result); var game = Assert.Single(store.Read());
        Assert.False(store.Preference(game).Automatic);
        store.SaveProfile(new("O meu perfil", ["menu-animation"]));
        store.SavePreference(new(game.Id, "O meu perfil", true));
        var reopened = new GameDiscoveryStore(database);
        Assert.Equal("O meu perfil", reopened.Preference(game).ProfileName); Assert.True(reopened.Preference(game).Automatic);
        var library = new GameLibrary(database); Assert.Contains(library.Read(), a => a.Name == "FiveM");
        File.Delete(game.Executables[0]);
        Assert.False(Assert.Single(reopened.Read()).CanTrack);
    }
    [Fact] public void ExportImportCannotIntroduceCommandsOrSecuritySettings()
    {
        var exported = GameDiscoveryStore.ExportProfile(new("Testar", ["animations"]));
        Assert.Equal("Testar", GameDiscoveryStore.ImportProfile(exported).Name);
        Assert.Throws<ArgumentException>(() => new GameSessionOptions("Unsafe", ["disable-defender"]).Freeze());
        Assert.Throws<ArgumentException>(() => new GameSessionOptions("Duplicate", ["animations", "animations"]).Freeze());
        Assert.Throws<JsonException>(() => GameDiscoveryStore.ImportProfile("""{"Name":"Unsafe","VisualKeys":[],"command":"do-something"}"""));
        Assert.Throws<ArgumentException>(() => GameDiscoveryStore.ImportProfile(new string('x', 16385)));
    }
    [Fact] public void AutomaticPerGameProfilesAreFrozenAndNewGamesReceiveNoPermission()
    {
        var app = new LibraryApp(@"C:\game\game.exe", "Game");
        var keys = new List<string> { "animations" };
        var profiles = new Dictionary<string, GameSessionOptions> { [app.Path] = new("Custom", keys) };
        var automation = new SessionAutomation(); automation.Enable(new([app], "Por jogo", false, false, null, profiles));
        keys.Add("menu-animation"); profiles.Clear();
        Assert.Single(automation.Configuration!.PerApp![app.Path].VisualKeys);
        Assert.Null(automation.Next([new(new(@"C:\other\other.exe", "Other"), 1, 1)], [app], false));
    }
    [Theory] [InlineData("0")] [InlineData("21")] [InlineData("1.5")] [InlineData("-1")]
    public void MouseSpeedRejectsInvalidValuesBeforeAnyNativeWrite(string value) => Assert.Throws<ArgumentException>(() => MousePrecision.ParseSpeed(value));
    [Fact] public void VpnDoesNotClaimOperationalStatusWithoutAllPrerequisites()
    {
        Assert.False(VpnReadiness.Evaluate(0, true, true).CanConnect);
        Assert.False(VpnReadiness.Evaluate(1, false, true).CanConnect);
        Assert.False(VpnReadiness.Evaluate(1, true, false).CanConnect);
    }
    [Fact] public void QuickConnectRejectsUnavailableUnmeasuredAndStaleServers()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Null(VpnServerSelection.QuickConnect([], now));
        var servers = new[] {
            new VpnServerHealth("off", "Test", false, 1, now),
            new VpnServerHealth("stale", "Test", true, 2, now.AddMinutes(-2)),
            new VpnServerHealth("unknown", "Test", true, null, now),
            new VpnServerHealth("busy", "Test", true, 3, now, .99),
            new VpnServerHealth("eligible", "Test", true, 20, now) };
        Assert.Equal("eligible", VpnServerSelection.QuickConnect(servers, now)!.Id);
    }
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
