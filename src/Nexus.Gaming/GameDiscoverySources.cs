using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace Nexus.Gaming;

public static class GameDiscoverySources
{
    public static GameDiscoveryLocations Windows(ICollection<string>? warnings = null)
    {
        var steam = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var registered = new List<RegisteredGame>();
        var packages = new List<string>();
        void Safe(string source, Action action)
        {
            try { action(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or
                System.Runtime.InteropServices.COMException or InvalidOperationException)
            { warnings?.Add(source + ": fonte indisponível nesta conta."); }
        }
        Safe("Steam", () =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string path) steam.Add(path);
        });
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            Safe("Registo de instalações", () =>
            {
                using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using (var key = machine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    if (key?.GetValue("InstallPath") is string path) steam.Add(path);
                using (var installs = machine.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs"))
                    foreach (var id in installs?.GetSubKeyNames().Take(512) ?? [])
                    {
                        using var key = installs!.OpenSubKey(id);
                        if (key?.GetValue("InstallDir") is string directory)
                            registered.Add(new(id, key.GetValue("DisplayName") as string ?? Path.GetFileName(directory.TrimEnd('\\')), "Ubisoft Connect", directory));
                    }
                foreach (var registryPath in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\EA Games", @"SOFTWARE\Electronic Arts\EA Games", @"SOFTWARE\Blizzard Entertainment" })
                {
                    using var installs = machine.OpenSubKey(registryPath);
                    foreach (var id in installs?.GetSubKeyNames().Take(512) ?? [])
                    {
                        using var key = installs!.OpenSubKey(id);
                        var publisher = key?.GetValue("Publisher") as string ?? "";
                        var name = key?.GetValue("DisplayName") as string ?? id;
                        var launcher = registryPath.Contains("Blizzard") || publisher.Contains("Blizzard", StringComparison.OrdinalIgnoreCase) ? "Battle.net" :
                            publisher.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase) ? "Ubisoft Connect" :
                            registryPath.Contains("EA Games") || publisher.Equals("Electronic Arts", StringComparison.OrdinalIgnoreCase) ||
                            publisher.StartsWith("Electronic Arts ", StringComparison.OrdinalIgnoreCase) ? "EA App" : null;
                        var directory = key?.GetValue("InstallLocation") as string ?? key?.GetValue("InstallDir") as string ?? key?.GetValue("InstallPath") as string;
                        if (launcher is null || string.IsNullOrWhiteSpace(directory) || Regex.IsMatch(name, @"^(EA app|EA Desktop|Battle\.net|Ubisoft Connect|Ubisoft Game Launcher)$", RegexOptions.IgnoreCase)) continue;
                        var icon = key?.GetValue("DisplayIcon") as string ?? "";
                        var match = Regex.Match(icon, @"^""?(.+?\.exe)""?(?:,\s*-?\d+)?$", RegexOptions.IgnoreCase);
                        var executable = match.Success ? Path.GetRelativePath(directory, match.Groups[1].Value) : null;
                        registered.Add(new(id, name, launcher, directory, executable));
                    }
                }
            });
        // Per-user uninstall records cover EA/Ubisoft installs registered only
        // for the current account, without looking into another user's profile.
        Safe("Instalações do utilizador", () =>
        {
            using var installs = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (var id in installs?.GetSubKeyNames().Take(512) ?? [])
            {
                using var key = installs!.OpenSubKey(id);
                var name = key?.GetValue("DisplayName") as string;
                var directory = key?.GetValue("InstallLocation") as string;
                var publisher = key?.GetValue("Publisher") as string ?? "";
                if (name is null || directory is null) continue;
                var launcher = publisher.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase) ? "Ubisoft Connect" :
                    publisher.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase) ? "EA App" :
                    publisher.Contains("Blizzard", StringComparison.OrdinalIgnoreCase) ? "Battle.net" : null;
                if (launcher is not null && !name.Contains("Launcher", StringComparison.OrdinalIgnoreCase))
                    registered.Add(new(id, name, launcher, directory));
            }
        });
        Safe("Microsoft Store", () =>
        {
            var manager = new Windows.Management.Deployment.PackageManager();
            foreach (var package in manager.FindPackagesForUser("").Take(512))
                Safe("Pacote Microsoft Store", () =>
                {
                    if (!package.IsFramework && !package.IsResourcePackage) packages.Add(package.InstalledLocation.Path);
                });
        });
        var xbox = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
            Safe("Bibliotecas Xbox", () => { if (drive.IsReady && drive.DriveType == DriveType.Fixed) xbox.Add(Path.Combine(drive.RootDirectory.FullName, "XboxGames")); });
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var data = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var apps = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        steam.Add(Path.Combine(apps, "Steam"));
        return new(steam.ToArray(), [Path.Combine(data, "Epic", "EpicGamesLauncher", "Data", "Manifests")],
            [Path.Combine(data, "Riot Games")], xbox, registered, packages, local,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    }
}
