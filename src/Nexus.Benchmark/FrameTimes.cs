using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nexus.Benchmark;

public sealed record FrameTimeSummary(string Application, int ProcessId, string SwapChain, string Metric,
    int Frames, int Omitted, int? Dropped, double Seconds, double AverageFps, double OnePercentLowFps,
    double MedianMs, double P99Ms)
{
    public override string ToString() => $"{Application} · PID {ProcessId} · {SwapChain} · {Frames} intervalos";
}
public sealed record FrameTimeImport(string FileName, string Sha256, IReadOnlyList<FrameTimeSummary> Streams);
public sealed record FpsCapture(DateTimeOffset ImportedAt, string FileName, string Sha256, string Context,
    FrameTimeSummary Summary)
{
    public override string ToString() => $"{ImportedAt:dd/MM HH:mm} · {Summary.Application} · {Summary.AverageFps:F1} FPS · {Context}";
}
public sealed record FpsComparison(double AveragePercent, double LowPercent, double P99Percent);

// Counts presentation intervals, not photons, input latency or displayed/generated frames.
public static class FrameTimes
{
    public const string Metric = "MsBetweenPresents";
    public const long MaxBytes = 64 * 1024 * 1024;
    public const int MaxRows = 250_000;
    private const int MaxLine = 32768;
    private sealed class StreamData
    {
        public List<double> Times { get; } = [];
        public int Omitted, Dropped;
    }
    public static Task<FrameTimeImport> ReadFileAsync(string path, CancellationToken token = default) => Task.Run(() =>
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > MaxBytes) throw new InvalidDataException("O CSV excede 64 MiB. Escolhe uma captura mais curta.");
        IReadOnlyList<FrameTimeSummary> streams;
        using (var reader = new StreamReader(file, new UTF8Encoding(false, true), true, 4096, leaveOpen: true))
            streams = Parse(reader, token);
        token.ThrowIfCancellationRequested();
        file.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(file));
        token.ThrowIfCancellationRequested();
        return new FrameTimeImport(Path.GetFileName(path), hash, streams);
    }, token);

    public static IReadOnlyList<FrameTimeSummary> Parse(TextReader reader, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var header = Split(ReadLine(reader) ?? throw new InvalidDataException("O CSV está vazio."));
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
            if (!indexes.TryAdd(header[i].Trim(), i)) throw new InvalidDataException("O CSV contém colunas duplicadas.");
        int Column(string name) => indexes.TryGetValue(name, out var i) ? i : throw new InvalidDataException(
            $"Falta a coluna {name}. Usa um CSV PresentMon com MsBetweenPresents (métricas v1); não um resumo ou FPS calculados.");
        var app = Column("Application"); var pid = Column("ProcessID"); var chain = Column("SwapChainAddress"); var time = Column(Metric);
        var hasDropped = indexes.TryGetValue("Dropped", out var dropped);
        var groups = new Dictionary<(string App, int Pid, string Chain), StreamData>();
        var rows = 0;
        while (ReadLine(reader) is string line)
        {
            token.ThrowIfCancellationRequested();
            if (line.Length == 0) continue;
            if (++rows > MaxRows) throw new InvalidDataException("O CSV excede 250 mil linhas. Divide a captura.");
            var cells = Split(line);
            if (cells.Count != header.Count) throw new InvalidDataException($"Linha {rows + 1}: número de colunas diferente do cabeçalho.");
            var application = cells[app].Trim(); var swap = cells[chain].Trim();
            if (application.Length is < 1 or > 260 || swap.Length is < 1 or > 128 ||
                application.Any(char.IsControl) || swap.Any(char.IsControl) ||
                !int.TryParse(cells[pid], NumberStyles.None, CultureInfo.InvariantCulture, out var process) || process <= 0)
                throw new InvalidDataException($"Linha {rows + 1}: identidade da aplicação inválida.");
            var key = (application, process, swap);
            if (!groups.TryGetValue(key, out var data))
            {
                if (groups.Count >= 256) throw new InvalidDataException("Há demasiadas aplicações/swap chains nesta captura.");
                data = new(); groups.Add(key, data);
            }
            if (hasDropped)
            {
                if (cells[dropped] is not ("0" or "1")) throw new InvalidDataException($"Linha {rows + 1}: Dropped deve ser 0 ou 1.");
                if (cells[dropped] == "1") data.Dropped++;
            }
            var value = cells[time].Trim();
            if (value.Equals("NA", StringComparison.OrdinalIgnoreCase) || value.Length == 0) { data.Omitted++; continue; }
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) || !double.IsFinite(ms) || ms < 0)
                throw new InvalidDataException($"Linha {rows + 1}: intervalo inválido. Não serão ocultadas pausas ou linhas corrompidas.");
            if (ms == 0) { data.Omitted++; continue; }
            data.Times.Add(ms);
        }
        var results = new List<FrameTimeSummary>();
        foreach (var (key, data) in groups)
        {
            if (data.Times.Count < 2) continue;
            data.Times.Sort();
            var times = data.Times;
            var total = times.Sum(); var slowest = Math.Max(1, (int)Math.Ceiling(times.Count * .01));
            var lowMean = times.Skip(times.Count - slowest).Average();
            var result = new FrameTimeSummary(key.App, key.Pid, key.Chain, Metric, times.Count, data.Omitted,
                hasDropped ? data.Dropped : null, total / 1000, times.Count * 1000d / total, 1000 / lowMean,
                Percentile(times, .5), Percentile(times, .99));
            if (!Valid(result)) throw new InvalidDataException("Os intervalos não produzem estatísticas finitas válidas.");
            results.Add(result);
        }
        if (results.Count == 0) throw new InvalidDataException("Não há uma aplicação/swap chain com pelo menos dois intervalos válidos.");
        return results.OrderByDescending(x => x.Frames).ToArray();
    }
    private static double Percentile(List<double> sorted, double fraction) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Count * fraction) - 1)];
    private static string? ReadLine(TextReader reader)
    {
        var line = new StringBuilder();
        while (reader.Read() is var ch && ch != -1)
        {
            if (ch == '\n') return line.ToString();
            if (ch == '\r') { if (reader.Peek() == '\n') reader.Read(); return line.ToString(); }
            if (line.Length >= MaxLine) throw new InvalidDataException("Uma linha do CSV excede 32 mil caracteres.");
            line.Append((char)ch);
        }
        return line.Length == 0 ? null : line.ToString();
    }
    private static List<string> Split(string line)
    {
        var cells = new List<string>(); var cell = new StringBuilder(); var quoted = false; var closedQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closedQuote = true; }
                }
                else cell.Append(ch);
            }
            else if (ch == ',') { cells.Add(cell.ToString()); cell.Clear(); closedQuote = false; }
            else if (ch == '"' && cell.Length == 0 && !closedQuote) quoted = true;
            else if (ch == '"' || closedQuote) throw new InvalidDataException("CSV mal formado: aspas ou texto depois de aspas.");
            else cell.Append(ch);
        }
        if (quoted) throw new InvalidDataException("CSV mal formado: campo entre aspas sem fim. Não são aceites campos com várias linhas.");
        cells.Add(cell.ToString()); return cells;
    }
    public static bool Valid(FrameTimeSummary? s) => s is not null && s.Metric == Metric && s.Frames is >= 2 and <= MaxRows &&
        s.Omitted is >= 0 and <= MaxRows && s.Frames + s.Omitted <= MaxRows && s.ProcessId > 0 &&
        !string.IsNullOrWhiteSpace(s.Application) && s.Application.Length <= 260 && !s.Application.Any(char.IsControl) &&
        !string.IsNullOrWhiteSpace(s.SwapChain) && s.SwapChain.Length <= 128 && !s.SwapChain.Any(char.IsControl) &&
        (s.Dropped is null || s.Dropped >= 0 && s.Dropped <= s.Frames + s.Omitted) &&
        new[] { s.Seconds, s.AverageFps, s.OnePercentLowFps, s.MedianMs, s.P99Ms }.All(x => double.IsFinite(x) && x > 0) &&
        s.OnePercentLowFps <= s.AverageFps * 1.000001 &&
        s.OnePercentLowFps >= s.AverageFps * Math.Ceiling(s.Frames * .01) / s.Frames * .999999 &&
        s.P99Ms >= s.MedianMs && s.P99Ms / 1000 <= s.Seconds &&
        Math.Abs(s.AverageFps - s.Frames / s.Seconds) <= Math.Max(double.Epsilon, s.AverageFps * .000001);
    public static bool Valid(FpsCapture? c) => c is not null && Valid(c.Summary) && c.Sha256 is { Length: 64 } hash && hash.All(Uri.IsHexDigit) &&
        !string.IsNullOrWhiteSpace(c.FileName) && c.FileName.Length <= 260 && !c.FileName.Any(char.IsControl) &&
        !string.IsNullOrWhiteSpace(c.Context) && c.Context.Length <= 600 && !c.Context.Any(char.IsControl);

    public static FpsComparison? Compare(FpsCapture current, FpsCapture baseline, out string reason)
    {
        reason = "";
        if (!Valid(current) || !Valid(baseline)) reason = "Captura inválida.";
        else if (current.Sha256.Equals(baseline.Sha256, StringComparison.OrdinalIgnoreCase)) reason = "É o mesmo ficheiro de origem: não é um teste antes/depois.";
        else if (!current.Summary.Application.Equals(baseline.Summary.Application, StringComparison.OrdinalIgnoreCase) || current.Context != baseline.Context)
            reason = "Escolhe a mesma aplicação e uma descrição de condições exatamente igual. Confirma PC, jogo, cena, resolução, qualidade, driver e limite de FPS.";
        else if (new[] { current, baseline }.Any(c => c.Summary.Frames < 300 || c.Summary.Seconds < 20 || c.Summary.Omitted > 1))
            reason = "Cada captura precisa de pelo menos 300 intervalos, 20 segundos analisados e no máximo um intervalo inicial omitido.";
        else if (Math.Abs(current.Summary.Seconds / baseline.Summary.Seconds - 1) > .2)
            reason = "A duração das capturas difere mais de 20%. Usa o mesmo percurso e duração.";
        if (reason.Length != 0) return null;
        return new((current.Summary.AverageFps / baseline.Summary.AverageFps - 1) * 100,
            (current.Summary.OnePercentLowFps / baseline.Summary.OnePercentLowFps - 1) * 100,
            (current.Summary.P99Ms / baseline.Summary.P99Ms - 1) * 100);
    }
}
