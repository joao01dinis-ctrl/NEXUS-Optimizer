using Nexus.Core;
using Nexus.Storage;
using Nexus.Hardware;
using Xunit;
namespace Nexus.Tests;

public sealed class ProfileTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "NexusTests", Guid.NewGuid().ToString("N"));
    private ProfileStore Store() => new(Path.Combine(directory, "test.db"));
    [Fact]
    public void DefaultIsNormal()
    {
        Assert.Equal("Normal", Store().Current);
        Assert.Empty(Store().History());
    }
    [Fact]
    public void ApplySurvivesReopening()
    {
        Store().Apply("Gaming");
        var s = Store();
        Assert.Equal("Gaming", s.Current);
        Assert.Single(s.History());
    }
    [Fact]
    public void UndoRestoresInReverseOrder()
    {
        var s = Store();
        s.Apply("Gaming");
        s.Apply("Trabalho");
        Assert.True(s.Undo());
        Assert.Equal("Gaming", s.Current);
        Assert.True(s.Undo());
        Assert.Equal("Normal", s.Current);
        Assert.False(s.Undo());
        Assert.All(s.History(), h => Assert.True(h.Undone));
    }
    [Fact]
    public void InvalidProfileDoesNotChangeState()
    {
        var s = Store();
        Assert.Throws<ArgumentException>(() => s.Apply("Turbo"));
        Assert.Equal("Normal", s.Current);
        Assert.Empty(s.History());
    }
    [Fact]
    public void SameProfileIsNoOp()
    {
        var s = Store();
        s.Apply("Normal");
        Assert.Empty(s.History());
    }
    [Fact]
    public void NewBranchAfterUndoRemainsReversible()
    {
        var s = Store();
        s.Apply("Gaming");
        s.Undo();
        s.Apply("Trabalho");
        Assert.True(s.Undo());
        Assert.Equal("Normal", s.Current);
        Assert.False(s.Undo());
    }
    [Fact]
    public void UndoSurvivesRestart()
    {
        Store().Apply("Gaming");
        Assert.True(Store().Undo());
        Assert.Equal("Normal", Store().Current);
    }
    [Fact]
    public void ProfilesHaveConservativeIntervals()
    {
        Assert.All(Profiles.All, p => Assert.InRange(p.SampleSeconds, 2, 5));
    }
    [Fact]
    public void LiveMemorySampleIsValid()
    {
        var s = new WindowsHardwareService().ReadSample();
        Assert.True(s.TotalGb > 0);
        Assert.InRange(s.MemoryPercent, 0, 100);
        Assert.InRange(s.UsedGb, 0, s.TotalGb);
    }
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}
