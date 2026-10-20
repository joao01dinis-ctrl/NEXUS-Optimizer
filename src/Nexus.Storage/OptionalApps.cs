using Windows.Management.Deployment;

namespace Nexus.Storage;

public sealed record OptionalApp(string PackageName, string Name, string? FullName)
{
    public string Display => Name + (FullName is null ? " · não instalada" : " · instalada");
    public override string ToString() => Display;
}
public static class OptionalApps
{
    // Exact optional package names. Store, security, shell, frameworks and dependencies are excluded.
    private static readonly (string Id, string Name)[] Catalog = [
        ("Microsoft.3DBuilder", "3D Builder"), ("Microsoft.WindowsAlarms", "Alarms & Clock"),
        ("Microsoft.WindowsCalculator", "Calculator"), ("Microsoft.WindowsCamera", "Camera"),
        ("Microsoft.MicrosoftOfficeHub", "Microsoft 365 / Office Hub"), ("Microsoft.SkypeApp", "Skype"),
        ("Microsoft.Getstarted", "Tips"), ("Microsoft.ZuneMusic", "Media Player / Groove Music"),
        ("microsoft.windowscommunicationsapps", "Mail and Calendar"), ("Microsoft.WindowsMaps", "Maps"),
        ("Microsoft.ZuneVideo", "Movies & TV"), ("Microsoft.Office.OneNote", "OneNote"),
        ("Microsoft.People", "People"), ("Microsoft.BingNews", "Novidades / News"), ("Microsoft.Windows.Photos", "Photos"),
        ("Microsoft.MicrosoftSolitaireCollection", "Solitaire"), ("Microsoft.BingWeather", "Weather"),
        ("Microsoft.WindowsSoundRecorder", "Voice Recorder"), ("Microsoft.GamingApp", "Xbox"),
        ("Microsoft.WindowsFeedbackHub", "Feedback Hub"), ("Microsoft.YourPhone", "Phone Link"),
        ("Microsoft.XboxGamingOverlay", "Xbox Game Bar"), ("Microsoft.Copilot", "Copilot"),
        ("Clipchamp.Clipchamp", "Clipchamp"), ("Microsoft.Windows.DevHome", "Dev Home"),
        ("MicrosoftCorporationII.MicrosoftFamily", "Family"), ("Microsoft.PowerAutomateDesktop", "Power Automate"),
        ("Microsoft.OutlookForWindows", "Outlook (novo)")
    ];
    public static IReadOnlyList<OptionalApp> Read()
    {
        var packages = new PackageManager().FindPackagesForUser("").Where(p => !p.IsFramework && !p.IsResourcePackage)
            .GroupBy(p => p.Id.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id.Version.Major).ThenByDescending(p => p.Id.Version.Minor).ThenByDescending(p => p.Id.Version.Build).ThenByDescending(p => p.Id.Version.Revision).First().Id.FullName, StringComparer.OrdinalIgnoreCase);
        return Catalog.Select(x => new OptionalApp(x.Id, x.Name, packages.GetValueOrDefault(x.Id))).ToArray();
    }
    public static async Task RemoveAsync(OptionalApp reviewed)
    {
        if (!Catalog.Any(x => string.Equals(x.Id, reviewed.PackageName, StringComparison.Ordinal)) || reviewed.FullName is null)
            throw new ArgumentException("Esta aplicação não é elegível.");
        var current = Read().Single(x => x.PackageName == reviewed.PackageName);
        if (current.FullName != reviewed.FullName) throw new InvalidOperationException("A versão da aplicação mudou. Atualiza a lista antes de remover.");
        var result = await new PackageManager().RemovePackageAsync(reviewed.FullName);
        if (result.ExtendedErrorCode is not null) throw new IOException("O Windows recusou remover esta aplicação: " + result.ErrorText, result.ExtendedErrorCode);
    }
    public static Uri StoreSearch(OptionalApp app) => new("ms-windows-store://search/?query=" + Uri.EscapeDataString(app.Name));
}
