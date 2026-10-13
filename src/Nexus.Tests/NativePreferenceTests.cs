using Nexus.Optimization;
using Xunit;

namespace Nexus.Tests;

public sealed class NativePreferenceFactAttribute : FactAttribute
{
    public NativePreferenceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXUS_TEST_NATIVE_PREFERENCE") != "1")
            Skip = "Opt-in: briefly changes menu animation using Windows API and restores the captured original value.";
    }
}
public sealed class NativePreferenceTests
{
    [NativePreferenceFact]
    public void WindowsPreferenceAndJournalRoundTripRestoresActualOriginalValue()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "src/Nexus.UI/Nexus.UI.csproj"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var root = Path.GetFullPath(Path.Combine(repository!.FullName, "artifacts", "native-preference-validation", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Assert.StartsWith(repository.FullName + Path.DirectorySeparatorChar, root);
        Directory.CreateDirectory(root);
        const string key = "menu-animation";
        var original = WindowsSettings.Resolve(key).Read();
        Assert.Contains(original, new[] { "0", "1" });
        var target = original == "0" ? "1" : "0";
        var optimizer = new SystemOptimizer(Path.Combine(root, "test.db"), WindowsSettings.Resolve);
        try
        {
            AdjustmentStates.Set(optimizer, AdjustmentStates.Read(optimizer, key, target), true);
            Assert.Equal(target, WindowsSettings.Resolve(key).Read());
            var applied = AdjustmentStates.Read(optimizer, key, target);
            Assert.True(applied.IsOn); Assert.True(applied.CanRestore);
            AdjustmentStates.Set(optimizer, applied, false);
            Assert.Equal(original, WindowsSettings.Resolve(key).Read());
            Assert.All(optimizer.History(), x => Assert.Equal("reverted", x.State));
            File.WriteAllText(Path.Combine(root, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { Key = key, Original = original, Applied = target, Restored = WindowsSettings.Resolve(key).Read(), JournalState = optimizer.History()[0].State }));
        }
        finally
        {
            var recovery = optimizer.RestoreAll();
            Assert.True(recovery.Complete, string.Join("\n", recovery.Errors));
            Assert.Equal(original, WindowsSettings.Resolve(key).Read());
        }
    }
}
