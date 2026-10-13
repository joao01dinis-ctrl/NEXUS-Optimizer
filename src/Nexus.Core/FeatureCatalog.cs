using System.Text.Json;
namespace Nexus.Core;

public sealed record FeatureItem(string Name, string Area, string State, string? Page, string Note)
{
    public string Display => Name + " · " + Area + "\n" + State + "\n" + Note;
}
public static class FeatureCatalog
{
    public static IReadOnlyList<FeatureItem> Read()
    {
        using var stream = typeof(FeatureCatalog).Assembly.GetManifestResourceStream("Nexus.Core.FeatureCatalog.json")
            ?? throw new IOException("O catálogo de funções não está incluído na aplicação.");
        return JsonSerializer.Deserialize<FeatureItem[]>(stream) ?? throw new IOException("O catálogo não é válido.");
    }
}
