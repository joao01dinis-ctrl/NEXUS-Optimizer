using Nexus.Gaming;
using Nexus.Optimization;
using Xunit;

namespace Nexus.Tests;

public sealed class ProductWorkflowTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Nexus-product-tests-" + Guid.NewGuid());
    private sealed class Setting(string key, string value) : ISystemSetting
    {
        public string Key => key; public string Name => key;
        public string Value = value;
        public bool Fail;
        public bool FailAfterWrite;
        public string Read() => Value;
        public void Write(string next) { if (Fail) throw new IOException("test-write-failure"); Value = next; if (FailAfterWrite) { FailAfterWrite = false; throw new IOException("test-partial-write"); } }
    }
    private readonly Dictionary<string, Setting> settings = new() { ["a"] = new("a", "original"), ["b"] = new("b", "original-b") };
    private ISystemSetting Resolve(string key) => settings[key];
    private SystemOptimizer Optimizer() => new(Path.Combine(root, "state.db"), Resolve);
    [Fact] public void BatchRecoversCompletedAndInterruptedWritesButPreservesEarlierManualChange()
    {
        var o = Optimizer(); o.Apply("a", "earlier"); settings["b"].FailAfterWrite = true;
        Assert.Throws<IOException>(() => o.ApplyBatch([new("a", "boost", "earlier"), new("b", "boost-b", "original-b")]));
        Assert.Equal("earlier", settings["a"].Value); Assert.Equal("original-b", settings["b"].Value);
        Assert.Equal(2, o.History().Count(x => x.State == "reverted"));
        Assert.Single(o.History(), x => x.State == "applied");
        Assert.DoesNotContain(o.History(), x => x.State == "pending");
    }
    [Fact] public void BatchRejectsStaleAnalysisBeforeAnyWrite()
    {
        var o = Optimizer(); settings["b"].Value = "external";
        Assert.Throws<InvalidOperationException>(() => o.ApplyBatch([new("a", "boost", "original"), new("b", "boost-b", "original-b")]));
        Assert.Empty(o.History()); Assert.Equal("original", settings["a"].Value);
    }
    [Fact] public void BatchReportsChangesAndRestoresTheirCapturedValues()
    {
        var o = Optimizer();
        Assert.Equal(1, o.ApplyBatch([new("a", "original", "original"), new("b", "boost-b", "original-b")]));
        Assert.True(o.RestoreAll().Complete); Assert.Equal("original-b", settings["b"].Value);
    }
    [Fact] public void RestoreAllReplaysChainsAndLeavesExternalConflictUntouched()
    {
        var o = Optimizer(); o.Apply("a", "first"); o.Apply("a", "second"); o.Apply("b", "new-b");
        settings["b"].Value = "external";
        var result = o.RestoreAll();
        Assert.Equal(2, result.Restored); Assert.Single(result.Errors);
        Assert.Equal("original", settings["a"].Value); Assert.Equal("external", settings["b"].Value);
        Assert.Contains(o.History(), x => x.Key == "b" && x.State == "applied");
        Assert.Equal(0, o.RestoreAll().Restored);
    }
    [Fact] public void RestoreAllCanSeparateManualFromSessionOwnedChanges()
    {
        var o = Optimizer(); o.Apply("a", "manual"); o.ApplyOwned("session", "b", "session-b");
        var result = o.RestoreAll(x => x.Owner is null);
        Assert.True(result.Complete); Assert.Equal(1, result.Restored);
        Assert.Equal("session-b", settings["b"].Value);
    }
    [Fact] public void DirectToggleRestoresCapturedNonDefaultValue()
    {
        var o = Optimizer(); var before = AdjustmentStates.Read(o, "a", "desired", Resolve);
        Assert.False(before.IsOn); AdjustmentStates.Set(o, before, true, Resolve);
        var applied = AdjustmentStates.Read(o, "a", "desired", Resolve);
        Assert.True(applied.IsOn); Assert.True(applied.CanRestore);
        AdjustmentStates.Set(o, applied, false, Resolve);
        Assert.Equal("original", settings["a"].Value);
    }
    [Fact] public void DirectToggleNeverInventsABaselineForExistingSetting()
    {
        settings["a"].Value = "desired"; var o = Optimizer();
        var state = AdjustmentStates.Read(o, "a", "desired", Resolve);
        Assert.True(state.IsOn); Assert.False(state.CanRestore);
        Assert.Throws<InvalidOperationException>(() => AdjustmentStates.Set(o, state, false, Resolve));
        Assert.Empty(o.History());
    }
    [Fact] public void DirectToggleRejectsStaleStateAndRetainsFailedWrite()
    {
        var o = Optimizer(); var observed = AdjustmentStates.Read(o, "a", "desired", Resolve);
        settings["a"].Value = "external";
        Assert.Throws<InvalidOperationException>(() => AdjustmentStates.Set(o, observed, true, Resolve));
        Assert.Empty(o.History());
        settings["a"].Fail = true;
        Assert.Throws<IOException>(() => AdjustmentStates.Set(o, AdjustmentStates.Read(o, "a", "desired", Resolve), true, Resolve));
        Assert.Equal("external", settings["a"].Value); Assert.Equal("failed", o.History()[0].State);
    }
    [Fact] public void SessionOwnedSettingCannotBeSwitchedOffByPermanentControl()
    {
        var o = Optimizer(); o.ApplyOwned("session", "a", "desired");
        var state = AdjustmentStates.Read(o, "a", "desired", Resolve);
        Assert.False(state.CanRestore);
        Assert.Throws<InvalidOperationException>(() => AdjustmentStates.Set(o, state, false, Resolve));
    }
    [Fact] public void AutomaticModeUsesFrozenLibraryAndDoesNotAddNewPathsSilently()
    {
        var app = new LibraryApp(@"C:\games\game.exe", "Game");
        var other = new LibraryApp(@"C:\games\new.exe", "New");
        var apps = new List<LibraryApp> { app }; var automation = new SessionAutomation();
        automation.Enable(new(apps, "Competitivo", true, true, null)); apps.Add(other);
        var first = new DetectedApp(app, 10, 100); var second = new DetectedApp(other, 20, 200);
        Assert.Equal(first, automation.Next([second, first], apps, false));
        Assert.Null(automation.Next([second], apps, false));
        Assert.Null(automation.Next([first], [other], false));
        Assert.Null(automation.Next([first], apps, true));
    }
    [Fact] public void AutomaticModeDoesNotRetryFailureOrRestartManuallyEndedInstance()
    {
        var app = new LibraryApp(@"C:\games\game.exe", "Game"); var automation = new SessionAutomation();
        automation.Enable(new([app], "Competitivo", true, true, null));
        var old = new DetectedApp(app, 10, 100); var next = new DetectedApp(app, 10, 200);
        automation.MarkAttempt(old);
        Assert.Null(automation.Next([old], [app], false));
        Assert.Equal(next, automation.Next([next], [app], false));
        automation.Disable(); Assert.Null(automation.Next([next], [app], false));
    }
    [Fact] public void StickyKeysAdjustmentPreservesUnrelatedAccessibilityFlags()
    {
        const uint original = 0xFFFFFFFF;
        Assert.Equal(original & ~0xDu, UserAccessibility.DisableStickyKeys(original));
        Assert.Equal(0x12u, UserAccessibility.DisableStickyKeys(0x1Fu));
        Assert.Equal("18", UserAccessibility.DisabledState("31"));
    }
    [Theory]
    [InlineData("MsMpEng", @"C:\apps\MsMpEng.exe")]
    [InlineData("EasyAntiCheat_EOS", @"C:\apps\EasyAntiCheat.exe")]
    [InlineData("Steam", @"C:\apps\Steam.exe")]
    [InlineData("Nexus.UI", @"C:\apps\Nexus.UI.exe")]
    public void ProtectedProcessesAreNotEligibleForPriorityChanges(string name, string path)
    { Assert.False(WindowsSettings.PriorityEligible(name, path)); }
    [Fact] public void PathsInWindowsAreNotEligibleEvenWithAnUnknownProcessName()
    {
        Assert.False(WindowsSettings.PriorityEligible("custom", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "unknown.exe")));
        Assert.False(WindowsSettings.PriorityEligible("custom", null));
        Assert.True(WindowsSettings.PriorityEligible("Game", @"C:\Games\Game.exe"));
    }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
