using Microsoft.Win32;
using System.Text.Json;

namespace Nexus.Optimization;

public sealed record UserPolicy(string Key, string Name, string Description, string Path, string ValueName,
    int Desired, bool EnterpriseOnly = false, bool Edge = false)
{
    public string DesiredState => JsonSerializer.Serialize(new PolicyValue(true, Desired));
}
public sealed record PolicyValue(bool Exists, int Value);
public interface IUserPolicyStore
{
    PolicyValue Read(UserPolicy policy);
    void Write(UserPolicy policy, PolicyValue value);
}

// Only documented user-scoped preferences. No security, updates, services or device policies.
public static class UserPolicies
{
    private const string Cloud = @"Software\Policies\Microsoft\Windows\CloudContent";
    public static IReadOnlyList<UserPolicy> All { get; } = Array.AsReadOnly(new UserPolicy[] {
        new("policy:tailored", "Limitar experiências personalizadas", "Impede a personalização com dados de diagnóstico. Não desliga o diagnóstico do Windows. Pro/Enterprise/Education.", Cloud, "DisableTailoredExperiencesWithDiagnosticData", 1),
        new("policy:third-party", "Reduzir sugestões de terceiros", "Limita sugestões de terceiros no Spotlight; sugestões da Microsoft podem continuar. Pro/Enterprise/Education.", Cloud, "DisableThirdPartySuggestions", 1),
        new("policy:spotlight", "Desativar funções Spotlight", "Desativa as funções Spotlight, incluindo imagens e sugestões. Enterprise/Education.", Cloud, "DisableWindowsSpotlightFeatures", 1, true),
        new("policy:spotlight-notifications", "Reduzir notificações Spotlight", "Retira sugestões Spotlight do centro de notificações. As notificações das apps continuam. Enterprise/Education.", Cloud, "DisableWindowsSpotlightOnActionCenter", 1, true),
        new("policy:settings-suggestions", "Reduzir sugestões nas Definições", "Retira sugestões de conteúdo nas Definições. Enterprise/Education.", Cloud, "DisableWindowsSpotlightOnSettings", 1, true),
        new("policy:welcome", "Desativar apresentação após atualizações", "Retira a apresentação de novidades do Windows após atualizações. Enterprise/Education.", Cloud, "DisableWindowsSpotlightWindowsWelcomeExperience", 1, true),
        new("policy:edge-personalization", "Limitar personalização do Edge", "Impede o uso de dados de navegação para personalização. Não controla todo o diagnóstico do Edge; não se aplica a contas empresariais ou de criança.", @"Software\Policies\Microsoft\Edge", "PersonalizationReportingEnabled", 0, Edge: true)
    });
    public static bool Contains(string key) => All.Any(p => p.Key == key);
    public static ISystemSetting Resolve(string key, IUserPolicyStore? store = null) => new Setting(All.Single(p => p.Key == key), store ?? new WindowsStore());
    public static string? Unsupported(UserPolicy p)
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var edition = k?.GetValue("EditionID") as string ?? "";
        if (p.Edge) return null;
        var enterprise = edition.StartsWith("Enterprise", StringComparison.Ordinal) || edition.StartsWith("Education", StringComparison.Ordinal);
        if (p.EnterpriseOnly && !enterprise) return "Esta política requer Windows Enterprise/Education; usa as Definições nesta edição.";
        if (!enterprise && !edition.StartsWith("Professional", StringComparison.Ordinal)) return "Esta política requer Windows Pro/Enterprise/Education; usa as Definições nesta edição.";
        return null;
    }
    private sealed class Setting(UserPolicy policy, IUserPolicyStore store) : ISystemSetting
    {
        public string Key => policy.Key;
        public string Name => policy.Name;
        public string Read() => JsonSerializer.Serialize(store.Read(policy));
        public void Write(string value)
        {
            var parsed = JsonSerializer.Deserialize<PolicyValue>(value) ?? throw new ArgumentException("Estado inválido.");
            if (value != JsonSerializer.Serialize(parsed)) throw new ArgumentException("Estado não canónico.");
            store.Write(policy, parsed);
        }
    }
    private sealed class WindowsStore : IUserPolicyStore
    {
        public PolicyValue Read(UserPolicy p)
        {
            using var k = Registry.CurrentUser.OpenSubKey(p.Path);
            if (k is null || !k.GetValueNames().Contains(p.ValueName, StringComparer.OrdinalIgnoreCase)) return new(false, 0);
            if (k.GetValueKind(p.ValueName) != RegistryValueKind.DWord) throw new InvalidOperationException("O valor existente não é compatível; não foi substituído.");
            return new(true, (int)k.GetValue(p.ValueName)!);
        }
        public void Write(UserPolicy p, PolicyValue value)
        {
            if (Unsupported(p) is { } reason) throw new NotSupportedException(reason);
            using var machine = Registry.LocalMachine.OpenSubKey(p.Path);
            if (machine?.GetValue(p.ValueName) is not null) throw new InvalidOperationException("Existe uma política do computador para esta opção. Não foi substituída.");
            if (!value.Exists)
            {
                using var k = Registry.CurrentUser.OpenSubKey(p.Path, true);
                k?.DeleteValue(p.ValueName, false);
            }
            else
            {
                using var k = Registry.CurrentUser.CreateSubKey(p.Path, true);
                k.SetValue(p.ValueName, value.Value, RegistryValueKind.DWord);
            }
        }
    }
}
