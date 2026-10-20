namespace Nexus.Benchmark;

public sealed record FpsRange(double Minimum, double Median, double Maximum);
public sealed record FpsSeriesSummary(int Runs, FpsRange AverageFps, FpsRange LowFps, FpsRange P99Ms);
public sealed record FpsSeriesComparison(FpsSeriesSummary Baseline, FpsSeriesSummary Current,
    double AveragePercent, double LowPercent, double P99Percent, string Outcome);

// Run-to-run ranges, not a confidence interval or proof of causation. Files are
// independent by content hash; re-imports/other streams of one CSV are not repeats.
public static class FrameTimeSeries
{
    public static FpsSeriesComparison? Compare(IReadOnlyList<FpsCapture> baseline, IReadOnlyList<FpsCapture> current, out string reason)
    {
        reason = "";
        if (baseline.Count is < 3 or > 20 || current.Count is < 3 or > 20)
        { reason = "Seleciona entre três e vinte capturas independentes para cada condição."; return null; }
        var all = baseline.Concat(current).ToArray();
        if (all.Any(c => !FrameTimes.Valid(c))) { reason = "Há uma captura inválida."; return null; }
        if (all.Select(c => c.Sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count() != all.Length)
        { reason = "O mesmo ficheiro aparece mais de uma vez. Usa capturas independentes, não outros fluxos ou cópias do CSV."; return null; }
        var first = all[0];
        if (all.Any(c => c.Context != first.Context || !c.Summary.Application.Equals(first.Summary.Application, StringComparison.OrdinalIgnoreCase)))
        { reason = "Todas as capturas precisam da mesma aplicação e condições declaradas exatamente iguais."; return null; }
        if (all.Any(c => c.Summary.Frames < 300 || c.Summary.Seconds < 20 || c.Summary.Omitted > 1))
        { reason = "Cada captura precisa de 300 intervalos, 20 segundos e no máximo um intervalo omitido."; return null; }
        if (all.Max(c => c.Summary.Seconds) / all.Min(c => c.Summary.Seconds) > 1.2)
        { reason = "As durações diferem mais de 20%. Repete o mesmo percurso e duração."; return null; }
        static FpsRange Range(IEnumerable<double> values)
        {
            var sorted = values.Order().ToArray(); var middle = sorted.Length / 2;
            return new(sorted[0], sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle], sorted[^1]);
        }
        static FpsSeriesSummary Summary(IReadOnlyList<FpsCapture> captures) => new(captures.Count,
            Range(captures.Select(c => c.Summary.AverageFps)), Range(captures.Select(c => c.Summary.OnePercentLowFps)), Range(captures.Select(c => c.Summary.P99Ms)));
        var before = Summary(baseline); var after = Summary(current);
        var better = after.AverageFps.Minimum > before.AverageFps.Maximum && after.LowFps.Minimum > before.LowFps.Maximum && after.P99Ms.Maximum < before.P99Ms.Minimum;
        var worse = after.AverageFps.Maximum < before.AverageFps.Minimum && after.LowFps.Maximum < before.LowFps.Minimum && after.P99Ms.Minimum > before.P99Ms.Maximum;
        var outcome = better ? "Variação favorável consistente nas repetições selecionadas. Não prova que o ajuste causou o resultado." :
            worse ? "Variação desfavorável consistente nas repetições selecionadas. Revê condições e considera reverter o ajuste no Histórico." :
            "Resultado inconclusivo ou misto: as faixas sobrepõem-se ou as métricas discordam. Não anunciar um ganho de desempenho.";
        return new(before, after, (after.AverageFps.Median / before.AverageFps.Median - 1) * 100,
            (after.LowFps.Median / before.LowFps.Median - 1) * 100, (after.P99Ms.Median / before.P99Ms.Median - 1) * 100, outcome);
    }
}
