using Nexus.Benchmark;
using Nexus.Storage;
using Xunit;

namespace Nexus.Tests;

public sealed class BenchmarkTests
{
    private static BenchmarkResult Sample(double cpu = 100, double copy = 200) => new(DateTimeOffset.Now, LocalBenchmark.Method, "same PC and runtime", cpu, copy, 2, 2);
    [Fact] public async Task CancelledRunDoesNotProducePartialResult()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LocalBenchmark().RunAsync("test CPU", cancellation.Token));
    }
    [Fact] public async Task ShortMeasurementProducesFiniteRates()
    {
        var result = await new LocalBenchmark().RunAsync("test CPU", sampleTime: TimeSpan.FromMilliseconds(50));
        Assert.True(LocalBenchmark.Valid(result)); Assert.True(result.CpuSeconds >= .05); Assert.True(result.CopySeconds >= .05);
    }
    [Fact] public void IncompatibleOrInvalidBaselineIsNotCompared()
    {
        Assert.Null(LocalBenchmark.Compare(Sample(), Sample() with { Environment = "another PC" }));
        Assert.Null(LocalBenchmark.Compare(Sample(), Sample() with { Method = "different algorithm" }));
        Assert.Null(LocalBenchmark.Compare(Sample(), Sample(0)));
        Assert.Null(LocalBenchmark.Compare(Sample(double.NaN), Sample()));
    }
    [Fact] public void ComparisonUsesPreviousMeasurementAsBaseline()
    {
        var result = LocalBenchmark.Compare(Sample(120, 150), Sample(100, 200));
        Assert.NotNull(result); Assert.Equal(20, result.CpuPercent, 7); Assert.Equal(-25, result.CopyPercent, 7);
    }
    [Fact] public void HistorySurvivesRestartAndRejectsInvalidMeasurements()
    {
        var root = Path.Combine(Path.GetTempPath(), "Nexus-bench-tests-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "test.db"); var store = new BenchmarkStore(path); var first = Sample(); var second = Sample(130);
            store.Save(first); store.Save(second);
            Assert.Equal(new[] { second, first }, new BenchmarkStore(path).History());
            Assert.Throws<ArgumentException>(() => store.Save(Sample(double.PositiveInfinity)));
            Assert.Equal(second, Assert.Single(store.History(1)));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }
    [Fact] public async Task ExcessiveDurationIsRejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await new LocalBenchmark().RunAsync("CPU", sampleTime: TimeSpan.FromHours(1)));
    }
}
