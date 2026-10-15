using Nexus.Optimization;
using System.Text.Json;
using Xunit;

namespace Nexus.Tests;

public sealed class HostedMouseFactAttribute : FactAttribute
{
    public HostedMouseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXUS_TEST_NATIVE_PREFERENCE") != "1" ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            Skip = "Only on an explicitly enabled disposable GitHub-hosted Windows runner; restores the exact mouse state.";
    }
}
[Collection("Native Windows preferences")]
public sealed class MousePrecisionNativeTests
{
    [HostedMouseFact]
    public void ActualMouseSpeedRoundTripRestoresTheCapturedValue()
    {
        var original = MousePrecision.ResolveSpeed().Read();
        var current = MousePrecision.ParseSpeed(original);
        var target = (current == 20 ? 19 : current + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "NexusOptimizer.sln"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var root = Path.Combine(repository!.FullName, "artifacts", "native-mouse-validation"); Directory.CreateDirectory(root);
        var optimizer = new SystemOptimizer(Path.Combine(root, "speed-test.db"), WindowsSettings.Resolve);
        try
        {
            Assert.Equal(1, optimizer.ApplyBatch([new(MousePrecision.SpeedKey, target, original)]));
            Assert.Equal(target, MousePrecision.ResolveSpeed().Read());
            Assert.True(optimizer.RestoreAll().Complete); Assert.Equal(original, MousePrecision.ResolveSpeed().Read());
            File.WriteAllText(Path.Combine(root, "speed-result.json"), JsonSerializer.Serialize(new { Result = "PASS", Original = original, Applied = target, Restored = MousePrecision.ResolveSpeed().Read() }));
        }
        finally { Assert.True(optimizer.RestoreAll().Complete); Assert.Equal(original, MousePrecision.ResolveSpeed().Read()); }
    }
    [HostedMouseFact]
    public void ActualMouseAccelerationRoundTripRestoresAllThreeOriginalValues()
    {
        var original = MousePrecision.Resolve().Read();
        var target = MousePrecision.ParseState(original)[2] == 0 ? "6,10,1" : "0,0,0";
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "NexusOptimizer.sln"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var root = Path.Combine(repository!.FullName, "artifacts", "native-mouse-validation");
        Directory.CreateDirectory(root);
        var optimizer = new SystemOptimizer(Path.Combine(root, "test.db"), WindowsSettings.Resolve);
        try
        {
            Assert.Equal(1, optimizer.ApplyBatch([new(MousePrecision.AccelerationKey, target, original)]));
            Assert.Equal(target, MousePrecision.Resolve().Read());
            Assert.True(optimizer.RestoreAll().Complete);
            Assert.Equal(original, MousePrecision.Resolve().Read());
            Assert.All(optimizer.History(), r => Assert.Equal("reverted", r.State));
            File.WriteAllText(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new
            { Result = "PASS", Original = original, Applied = target, Restored = MousePrecision.Resolve().Read(), Journal = "reverted" }));
        }
        finally
        {
            var restored = optimizer.RestoreAll();
            Assert.True(restored.Complete, string.Join("\n", restored.Errors));
            Assert.Equal(original, MousePrecision.Resolve().Read());
        }
    }
}
