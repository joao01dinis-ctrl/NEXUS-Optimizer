using Nexus.Gaming;
using Nexus.Optimization;
using System.Text.Json;
using Xunit;

namespace Nexus.Tests;

[CollectionDefinition("Native Windows preferences", DisableParallelization = true)]
public sealed class NativeWindowsPreferenceCollection { }

[Collection("Native Windows preferences")]
public sealed class GameProfileNativePreferenceTests
{
    private sealed class ProcessState : ISystemSetting
    {
        public string Key => "priority:0:0"; public string Name => "Disposable test process identity";
        public bool Ended;
        public string Read() => Ended ? "ended" : "Normal";
        public void Write(string value) => throw new InvalidOperationException("This profile must not write process priority.");
    }
    [HostedMouseFact]
    public void ActualMaximumVisualProfileRestoresEveryCapturedWindowsValue()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "NexusOptimizer.sln"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var root = Path.Combine(repository!.FullName, "artifacts", "native-game-profile-validation"); Directory.CreateDirectory(root);
        var profile = GameSessionOptions.BuiltIn.Single(p => p.Name == "Máximo");
        var original = profile.VisualKeys.ToDictionary(k => k, k => WindowsSettings.Resolve(k).Read());
        var process = new ProcessState();
        ISystemSetting Resolve(string key) => key == process.Key ? process : WindowsSettings.Resolve(key);
        var optimizer = new SystemOptimizer(Path.Combine(root, "session.db"), Resolve);
        var sessions = new GameSessions(optimizer.DatabasePath, optimizer, Resolve);
        try
        {
            Assert.Equal("active", sessions.Start(process.Key, "Disposable profile test", priority: false, visualKeys: profile.VisualKeys).Status);
            var applied = profile.VisualKeys.ToDictionary(k => k, k => WindowsSettings.Resolve(k).Read());
            Assert.All(applied.Values, value => Assert.Equal("0", value));
            process.Ended = true;
            Assert.Equal("ended", sessions.Poll()!.Status);
            var restored = profile.VisualKeys.ToDictionary(k => k, k => WindowsSettings.Resolve(k).Read());
            Assert.All(original, pair => Assert.Equal(pair.Value, restored[pair.Key]));
            Assert.All(optimizer.History(), record => Assert.Equal("reverted", record.State));
            File.WriteAllText(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { Result = "PASS", Original = original, Applied = applied, Restored = restored, PriorityWrites = 0 }));
        }
        finally
        {
            Assert.True(optimizer.RestoreAll().Complete);
            Assert.All(original, pair => Assert.Equal(pair.Value, WindowsSettings.Resolve(pair.Key).Read()));
        }
    }
}
