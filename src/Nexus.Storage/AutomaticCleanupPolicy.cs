namespace Nexus.Storage;

public static class AutomaticCleanupPolicy
{
    public const int MaximumFiles = 100;
    public const long MaximumFileBytes = 10 * 1048576L, MaximumTotalBytes = 256 * 1048576L;
    public static readonly TimeSpan MinimumAge = TimeSpan.FromDays(30);
    public static string UserRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
    public static IReadOnlyList<TemporaryFileCandidate> Select(IReadOnlyList<TemporaryFileCandidate> candidates, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("A seleção exige data UTC.");
        var cutoff = utcNow - MinimumAge; var result = new List<TemporaryFileCandidate>(); long total = 0;
        foreach (var candidate in candidates.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (result.Count == MaximumFiles) break;
            if (candidate.LastWriteUtc > cutoff || candidate.LastAccessUtc > cutoff || candidate.Bytes < 0 || candidate.Bytes > MaximumFileBytes || total + candidate.Bytes > MaximumTotalBytes) continue;
            result.Add(candidate); total += candidate.Bytes;
        }
        return result;
    }
}
