using Nexus.Benchmark;
using Nexus.Storage;
using Nexus.Optimization;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Nexus.Tests;

public sealed class FrameTimeTests
{
    private const string Header = "Application,ProcessID,SwapChainAddress,MsBetweenPresents\n";
    private static IReadOnlyList<FrameTimeSummary> Parse(string rows) => FrameTimes.Parse(new StringReader(Header + rows));
    private static FpsCapture Capture(double ms = 40, int count = 600, char hash = 'A') => new(DateTimeOffset.Now, "test.csv", new string(hash, 64),
        "PC A | scene A | 1080p | high | driver A | uncapped | VSync off",
        Assert.Single(Parse(string.Concat(Enumerable.Repeat($"game.exe,42,0x01,{ms.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n", count)))));

    [Fact] public void AverageUsesElapsedIntervalsAndKeepsRealPauses()
    {
        var s = Assert.Single(Parse("game.exe,42,0x01,10\ngame.exe,42,0x01,20\ngame.exe,42,0x01,1000\n"));
        Assert.Equal(3, s.Frames); Assert.Equal(1.03, s.Seconds, 8);
        Assert.Equal(3000d / 1030, s.AverageFps, 8); Assert.Equal(1, s.OnePercentLowFps, 8);
        Assert.Equal(20, s.MedianMs); Assert.Equal(1000, s.P99Ms);
    }
    [Fact] public void ApplicationsProcessesAndSwapChainsAreNeverMerged()
    {
        var streams = Parse("game.exe,42,0x01,10\ngame.exe,42,0x01,10\ngame.exe,42,0x02,20\ngame.exe,42,0x02,20\ngame.exe,43,0x01,40\ngame.exe,43,0x01,40\n");
        Assert.Equal(3, streams.Count); Assert.All(streams, s => Assert.Equal(2, s.Frames));
        Assert.Equal(new[] { 25d, 50d, 100d }, streams.Select(x => x.AverageFps).Order().ToArray());
    }
    [Fact] public void ZeroAndMissingIntervalsAreReportedAndDroppedPresentsAreKept()
    {
        var s = Assert.Single(FrameTimes.Parse(new StringReader(Header.TrimEnd() + ",Dropped\ngame.exe,42,0x01,0,0\ngame.exe,42,0x01,NA,1\ngame.exe,42,0x01,20,1\ngame.exe,42,0x01,40,0\n")));
        Assert.Equal(2, s.Frames); Assert.Equal(2, s.Omitted); Assert.Equal(2, s.Dropped);
        Assert.Equal(2000d / 60, s.AverageFps, 8);
    }
    [Theory]
    [InlineData("NaN")][InlineData("Infinity")][InlineData("-1")][InlineData("broken")]
    public void CorruptIntervalsInvalidateTheImportInsteadOfImprovingTheAverage(string bad)
        => Assert.Throws<InvalidDataException>(() => Parse($"game.exe,42,0x01,20\ngame.exe,42,0x01,{bad}\n"));
    [Fact] public void QuotedFieldsAndEscapedQuotesAreRead()
    {
        var s = Assert.Single(Parse("\"game,\"\"one\"\".exe\",42,0x01,20\n\"game,\"\"one\"\".exe\",42,0x01,20\n"));
        Assert.Equal("game,\"one\".exe", s.Application); Assert.Equal(50, s.AverageFps);
    }
    [Theory]
    [InlineData("game.exe,42,0x01\n")]
    [InlineData("\"game.exe,42,0x01,20\n")]
    [InlineData("game.exe,-1,0x01,20\n")]
    [InlineData("game.exe,42,0x01,20,extra\n")]
    public void MalformedRowsAreRejected(string row) => Assert.Throws<InvalidDataException>(() => Parse(row));
    [Fact] public void UnsupportedMetricsAndDuplicateColumnsAreNotGuessed()
    {
        Assert.Throws<InvalidDataException>(() => FrameTimes.Parse(new StringReader("Application,ProcessID,SwapChainAddress,CPUFrameTime\ngame.exe,42,0x01,20\n")));
        Assert.Throws<InvalidDataException>(() => FrameTimes.Parse(new StringReader(Header.TrimEnd() + ",MsBetweenPresents\n")));
    }
    [Fact] public void ParserEnforcesBoundsAndCancellation()
    {
        Assert.Throws<InvalidDataException>(() => Parse(new string('a', 32769)));
        Assert.Throws<InvalidDataException>(() => Parse(string.Concat(Enumerable.Repeat("game.exe,42,0x01,20\n", FrameTimes.MaxRows + 1))));
        using var c = new CancellationTokenSource(); c.Cancel();
        Assert.Throws<OperationCanceledException>(() => FrameTimes.Parse(new StringReader(Header), c.Token));
    }
    [Fact] public void ComparisonRequiresIndependentCompatibleCaptures()
    {
        var before = Capture(); var after = Capture(32, 750, 'B');
        var result = FrameTimes.Compare(after, before, out var reason);
        Assert.NotNull(result); Assert.Empty(reason); Assert.Equal(25, result.AveragePercent, 8); Assert.Equal(25, result.LowPercent, 8); Assert.Equal(-20, result.P99Percent, 8);
        Assert.Null(FrameTimes.Compare(before, before, out _));
        Assert.Null(FrameTimes.Compare(after with { Context = "other scene" }, before, out _));
        Assert.Null(FrameTimes.Compare(after with { Summary = after.Summary with { Application = "another.exe" } }, before, out _));
        Assert.Null(FrameTimes.Compare(Capture(40, 100, 'B'), before, out _));
        Assert.Null(FrameTimes.Compare(Capture(40, 1200, 'B'), before, out _));
        Assert.Null(FrameTimes.Compare(after with { Summary = after.Summary with { Omitted = 2 } }, before, out _));
    }
    [Fact] public void InvalidAggregatesAndIncompleteMetadataAreRejected()
    {
        var capture = Capture();
        Assert.False(FrameTimes.Valid(capture with { Summary = capture.Summary with { AverageFps = double.NaN } }));
        Assert.False(FrameTimes.Valid(capture with { Summary = capture.Summary with { AverageFps = 500 } }));
        Assert.False(FrameTimes.Valid(capture with { Sha256 = "not a file hash" }));
        Assert.False(FrameTimes.Valid(capture with { Context = "" }));
        Assert.False(FrameTimes.Valid((FpsCapture?)null));
    }
    [Fact] public async Task FileImportHashesOriginalBytesAndPreservesHistoryInBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "Nexus-frames-tests-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "source.csv"); var bytes = Encoding.UTF8.GetBytes(Header + "game.exe,42,0x01,20\ngame.exe,42,0x01,40\n");
            File.WriteAllBytes(path, bytes); var imported = await FrameTimes.ReadFileAsync(path);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), imported.Sha256); Assert.Equal("source.csv", imported.FileName);
            var capture = new FpsCapture(DateTimeOffset.Now, imported.FileName, imported.Sha256, "test fixture", Assert.Single(imported.Streams));
            var database = Path.Combine(root, "test.db"); var store = new FrameTimeStore(database); store.Save(capture);
            Assert.Equal(capture, Assert.Single(new FrameTimeStore(database).History()));
            Assert.Throws<InvalidOperationException>(() => store.Save(capture));
            Assert.Throws<ArgumentException>(() => store.Save(capture with { Context = "" }));
            var backup = Path.Combine(root, "backup.sqlite"); new SystemOptimizer(database, _ => throw new NotSupportedException()).ExportBackup(backup);
            Assert.Equal(capture, Assert.Single(new FrameTimeStore(backup).History()));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FrameTimes.ReadFileAsync(path, cancelled.Token));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }
}
