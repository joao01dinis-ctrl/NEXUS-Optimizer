namespace Nexus.Gaming;

public sealed record AutomationConfiguration(IReadOnlyList<LibraryApp> Apps, string ProfileName,
    bool Priority, bool Animations, string? PowerPlan);

// Permission is a frozen per-opening snapshot. Changing controls cannot expand
// it. PID plus start time allows each process instance only one automatic start.
public sealed class SessionAutomation
{
    public AutomationConfiguration? Configuration { get; private set; }
    private readonly HashSet<string> attempted = new(StringComparer.Ordinal);
    public bool Enabled => Configuration is not null;
    public void Enable(AutomationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.Apps);
        if (configuration.Apps.Count == 0 || configuration.Apps.Count > 128) throw new ArgumentException("Adiciona entre 1 e 128 aplicações à biblioteca.");
        if (string.IsNullOrWhiteSpace(configuration.ProfileName)) throw new ArgumentException("Perfil inválido.");
        Configuration = configuration with { Apps = Array.AsReadOnly(configuration.Apps.ToArray()) };
        attempted.Clear();
    }
    public void Disable() { Configuration = null; attempted.Clear(); }
    public DetectedApp? Next(IReadOnlyList<DetectedApp> detected, IReadOnlyList<LibraryApp> currentLibrary, bool sessionActive)
    {
        if (Configuration is null || sessionActive || attempted.Count >= 512) return null;
        return detected.FirstOrDefault(d =>
            Configuration.Apps.Any(a => string.Equals(a.Path, d.App.Path, StringComparison.OrdinalIgnoreCase)) &&
            currentLibrary.Any(a => string.Equals(a.Path, d.App.Path, StringComparison.OrdinalIgnoreCase)) &&
            !attempted.Contains(d.ProcessKey));
    }
    public void MarkAttempt(DetectedApp app) => attempted.Add(app.ProcessKey);
}
