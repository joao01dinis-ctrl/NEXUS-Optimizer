using Nexus.Benchmark;
using System.Text.Json;
using Xunit;

namespace Nexus.Tests;

public sealed class FrameTimeSeriesTests
{
    private static FpsCapture Capture(double ms, char hash, int frames = 1200) => new(DateTimeOffset.Now, "game.csv", new string(hash, 64), "same scene | same PC | same driver | 1080p",
        FrameTimes.Parse(new StringReader("Application,ProcessID,SwapChainAddress,MsBetweenPresents\n" + string.Concat(Enumerable.Repeat($"FiveM.exe,10,0x1,{ms.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n", frames))))[0]);
    private static FpsCapture[] Runs(double ms, string hashes, int frames = 1200) => hashes.Select(h => Capture(ms, h, frames)).ToArray();
    [Fact] public void RepeatedComparableIndependentRunsReportMediansAndObservedRanges()
    {
        var result = FrameTimeSeries.Compare(Runs(25, "ABC"), Runs(20, "DEF", 1500), out var reason);
        Assert.NotNull(result); Assert.Empty(reason); Assert.Equal(25, result.AveragePercent, 6);
        Assert.Equal(3, result.Baseline.Runs); Assert.Equal(40, result.Baseline.AverageFps.Median);
        Assert.Contains("favorável", result.Outcome); Assert.Contains("Não prova", result.Outcome);
    }
    [Fact] public void OverlapAndMixedMetricsDoNotBecomeAClaimOfGain()
    {
        var before = Runs(25, "ABC"); var after = new[] { Capture(20, 'D', 1500), Capture(25, 'E'), Capture(30, 'F', 1000) };
        var result = FrameTimeSeries.Compare(before, after, out var reason);
        Assert.NotNull(result); Assert.Empty(reason); Assert.Contains("inconclusivo", result.Outcome);
    }
    [Fact] public void ConsistentlyWorseRunsSuggestReviewAndRevert()
    {
        var result = FrameTimeSeries.Compare(Runs(20, "ABC", 1500), Runs(25, "DEF"), out _);
        Assert.NotNull(result); Assert.Contains("desfavorável", result.Outcome); Assert.Contains("reverter", result.Outcome);
    }
    [Fact] public void ReimportsAndStreamsOfTheSameCsvAreNotIndependentRepeats()
    {
        var before = Runs(25, "ABC"); var after = Runs(25, "DEF");
        after[1] = after[1] with { Sha256 = before[1].Sha256.ToLowerInvariant(), Summary = after[1].Summary with { SwapChain = "another" } };
        Assert.Null(FrameTimeSeries.Compare(before, after, out var reason)); Assert.Contains("mesmo ficheiro", reason);
        Assert.Null(FrameTimeSeries.Compare([before[0], before[0], before[2]], Runs(25, "DEF"), out _));
    }
    [Fact] public void TooFewShortLongMismatchedAndCorruptCapturesAreRejected()
    {
        var before = Runs(25, "ABC"); var after = Runs(25, "DEF");
        Assert.Null(FrameTimeSeries.Compare(before.Take(2).ToArray(), after, out _));
        Assert.Null(FrameTimeSeries.Compare(Runs(25, "ABC", 100), after, out _));
        Assert.Null(FrameTimeSeries.Compare(before, Runs(25, "DEF", 1800), out _));
        after[1] = after[1] with { Context = "different scene" }; Assert.Null(FrameTimeSeries.Compare(before, after, out _));
        after[1] = after[1] with { Context = before[0].Context, Summary = after[1].Summary with { Omitted = 2 } }; Assert.Null(FrameTimeSeries.Compare(before, after, out _));
        after[1] = after[1] with { Summary = after[1].Summary with { AverageFps = double.NaN } }; Assert.Null(FrameTimeSeries.Compare(before, after, out _));
    }
    [Fact] public void PointOneLowKeepsTheWorstPausesAndShortCapturesHaveNoValue()
    {
        var csv = "Application,ProcessID,SwapChainAddress,MsBetweenPresents\n" + string.Concat(Enumerable.Repeat("FiveM.exe,10,0x1,10\n", 999)) + "FiveM.exe,10,0x1,1000\n";
        var summary = FrameTimes.Parse(new StringReader(csv))[0];
        Assert.Equal(1d, summary.ZeroPointOnePercentLowFps); Assert.Equal(10000d / 1090, summary.OnePercentLowFps, 6);
        Assert.Null(Capture(25, 'A', 999).Summary.ZeroPointOnePercentLowFps);
        Assert.False(FrameTimes.Valid(summary with { ZeroPointOnePercentLowFps = 1000 }));
    }
    [Fact] public void PreviousJsonWithoutNewMetricsOrConditionStillLoads()
    {
        var capture = Capture(25, 'A'); var node = JsonSerializer.SerializeToNode(capture)!.AsObject();
        node.Remove("Condition"); node["Summary"]!.AsObject().Remove("ZeroPointOnePercentLowFps");
        var reopened = JsonSerializer.Deserialize<FpsCapture>(node.ToJsonString());
        Assert.True(FrameTimes.Valid(reopened)); Assert.Null(reopened!.Summary.ZeroPointOnePercentLowFps);
    }
}
