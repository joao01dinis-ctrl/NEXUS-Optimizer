namespace Nexus.Core;

public enum MouseAccess { ReadOnly, OfficialSoftware, Unsupported }
public sealed record MouseCapability(string Name, MouseAccess Access, string Explanation);
public sealed record MouseDevice(string Id, string Name, string? VendorId, string? ProductId, string Connection, string Provider,
    int? Dpi = null, int? ConfiguredPolling = null, int? MaximumPolling = null, int? BatteryPercent = null, string? Firmware = null, string? Sensor = null)
{
    public override string ToString() => Name + (VendorId is null ? "" : $" · {VendorId}:{ProductId}");
}
public interface IMouseProvider
{
    string Name { get; }
    IReadOnlyList<MouseDevice> Detect();
    IReadOnlyList<MouseCapability> Capabilities(MouseDevice device);
}
public enum MouseActivation { Ask, Automatic, Never }
public sealed record MouseProfile(string Name, string Game, int DesiredDpi, int DesiredPolling, double Sensitivity,
    int WindowsSpeed, bool DisableAcceleration, MouseActivation Activation = MouseActivation.Ask, string SensorNotes = "")
{
    public override string ToString() => Name;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Game is not ("CS2" or "FiveM" or "Other") ||
            DesiredDpi is < 50 or > 100000 || !MouseLabMath.PollingRates.Contains(DesiredPolling) ||
            !double.IsFinite(Sensitivity) || Sensitivity is <= 0 or > 1000 || WindowsSpeed is < 1 or > 20 ||
            !Enum.IsDefined(Activation) || SensorNotes is null || SensorNotes.Length > 1000)
            throw new ArgumentException("Perfil inválido. Revê nome, jogo, DPI, polling e sensibilidade.");
    }
    public static IReadOnlyList<MouseProfile> Defaults { get; } = Array.AsReadOnly(new[] {
        new MouseProfile("CS2 Competitive", "CS2", 800, 1000, 1, 10, true),
        new MouseProfile("FiveM PvP", "FiveM", 800, 1000, 1, 10, true),
        new MouseProfile("Balanced Gaming", "Other", 800, 1000, 1, 10, false),
        new MouseProfile("Everyday Use", "Other", 800, 500, 1, 10, false),
        new MouseProfile("Custom Profile", "Other", 800, 1000, 1, 10, false)
    });
}
public static class MouseLabMath
{
    public static IReadOnlyList<int> PollingRates { get; } = Array.AsReadOnly(new[] {125, 250, 500, 1000, 2000, 4000, 8000});
    public static double Edpi(double dpi, double sensitivity)
    { Validate(dpi, sensitivity); return dpi * sensitivity; }
    public static double ConvertDpi(double dpi, double sensitivity, double targetDpi)
    { Validate(targetDpi, sensitivity); return Edpi(dpi, sensitivity) / targetDpi; }
    public static double Cm360(double dpi, double sensitivity, double degreesPerCount)
    {
        Validate(dpi, sensitivity);
        if (!double.IsFinite(degreesPerCount) || degreesPerCount is <= 0 or > 360) throw new ArgumentException("Indica um coeficiente angular conhecido e positivo.");
        return 360 * 2.54 / (dpi * sensitivity * degreesPerCount);
    }
    public static double Calibrate(double distanceCm, double rotationDegrees)
    {
        if (!double.IsFinite(distanceCm) || distanceCm is <= 0 or > 1000 || !double.IsFinite(rotationDegrees) || rotationDegrees is <= 0 or > 36000)
            throw new ArgumentException("Introduz distância e rotação medidas, positivas.");
        return distanceCm * 360 / rotationDegrees;
    }
    private static void Validate(double dpi, double sensitivity)
    {
        if (!double.IsFinite(dpi) || dpi is < 50 or > 100000 || !double.IsFinite(sensitivity) || sensitivity is <= 0 or > 1000)
            throw new ArgumentException("DPI entre 50 e 100000; sensibilidade positiva até 1000.");
    }
}

// Times represent delivery to this application's foreground message queue, not physical latency.
public readonly record struct MouseSample(double Ms, int X, int Y, ushort Buttons, short Wheel, bool Absolute = false);
public sealed record MouseAnalysis(int Samples, int Movements, int Clicks, int WheelEvents, int AbsoluteEvents, int Discarded,
    double? EventsPerSecond, double? MedianIntervalMs, double? P95IntervalMs, double? IntervalDeviationMs,
    int GapsOver20Ms, double? MeanClickIntervalMs, int CloseClickPairs, int? DiagnosticScore, double[] IntervalGraph)
{
    public const string Limitation = "Intervalos de entrega de eventos no Windows, não latência física. Pausas, movimento lento, fila da aplicação e carga de CPU alteram o resultado. Cliques próximos não provam defeito ou debounce incorreto.";
}
public static class MouseAnalyzer
{
    public const int MaximumSamples = 120000;
    public static MouseAnalysis Analyze(IReadOnlyList<MouseSample> samples, int discarded = 0)
    {
        if (samples.Count > MaximumSamples || discarded < 0) throw new ArgumentException("Limite de amostras excedido.");
        double previous = -1;
        foreach (var s in samples)
        { if (!double.IsFinite(s.Ms) || s.Ms < 0 || s.Ms < previous) throw new ArgumentException("Cronologia inválida."); previous = s.Ms; }
        var movement = samples.Where(s => !s.Absolute && (s.X != 0 || s.Y != 0)).ToArray();
        var intervals = movement.Zip(movement.Skip(1), (a,b) => b.Ms - a.Ms).ToArray();
        var sorted = intervals.Order().ToArray();
        var clicks = samples.Where(s => (s.Buttons & 1) != 0).ToArray();
        var clickTimes = clicks.Zip(clicks.Skip(1), (a,b) => b.Ms-a.Ms).ToArray();
        var duration = movement.Length > 1 ? movement[^1].Ms - movement[0].Ms : 0;
        double? median = sorted.Length == 0 ? null : Percentile(sorted, .5);
        var mean = sorted.Length == 0 ? 0 : intervals.Average();
        double? deviation = sorted.Length == 0 ? null : Math.Sqrt(intervals.Average(x => Math.Pow(x - mean, 2)));
        // A descriptive delivery consistency index, only for sufficiently long populated tests.
        int? score = movement.Length >= 100 && duration >= 1000 && discarded == 0 && samples.All(s => !s.Absolute)
            ? (int)Math.Round(100 * (double)intervals.Count(x => x > 0 && x <= 20) / intervals.Length) : null;
        return new(samples.Count, movement.Length, clicks.Length, samples.Count(s => s.Wheel != 0), samples.Count(s=>s.Absolute), discarded,
            duration > 0 ? (movement.Length-1)*1000/duration : null, median, sorted.Length == 0 ? null : Percentile(sorted,.95), deviation,
            intervals.Count(x=>x>20), clickTimes.Length == 0 ? null : clickTimes.Average(), clickTimes.Count(x=>x<30), score,
            intervals.Where((_,i)=>i % Math.Max(1,(int)Math.Ceiling(intervals.Length/240.0)) == 0).Take(240).ToArray());
    }
    private static double Percentile(double[] sorted,double p) => sorted[(int)Math.Ceiling((sorted.Length-1)*p)];
}
public sealed record MouseRecommendation(string Title, string Reason, string Benefit, string Tradeoff, string Confidence);
public static class MouseRecommendations
{
    public static IReadOnlyList<MouseRecommendation> Create(string priority, MouseAnalysis? analysis, bool acceleration, int desiredPolling)
    {
        var result = new List<MouseRecommendation> {
            new("Medir antes e depois", "Compara sessões com movimento e duração semelhantes.", "Permite verificar a tua configuração.", "Uma sessão isolada não demonstra ganho num jogo.", "Alta"),
            new("DPI e sensor: software oficial", "O fornecedor genérico não lê nem escreve estas definições.", "Evita aplicar comandos incompatíveis.", "Requer configurar o rato no software do fabricante.", "Alta") };
        if (acceleration && priority != "Productivity") result.Add(new("Experimentar sem aceleração", "A precisão melhorada está ativa no Windows.", "Pode facilitar consistência do ponteiro.", "Jogos com Raw Input podem ignorar esta opção; pode ser menos confortável no ambiente de trabalho.", "Média"));
        if (desiredPolling > 1000) result.Add(new("Comparar polling elevado", "2000–8000 Hz aumenta o número de eventos.", "Mais amostras quando hardware e jogo suportam.", "Mais CPU e bateria; não garante menor latência ou mais FPS.", "Média"));
        if (analysis?.GapsOver20Ms > 0) result.Add(new("Repetir o teste em movimento contínuo", "Foram observados intervalos superiores a 20 ms.", "Ajuda a distinguir pausas de irregularidades persistentes.", "Não identifica sozinho a causa; verifica ligação, carga e superfície.", "Baixa"));
        var exercise=priority switch { "Competitive FPS"=>"Flick e Troca de alvos", "Precision"=>"Microajustes e calibração manual", "Tracking"=>"Tracking", "Productivity"=>"Conforto do ponteiro no ambiente de trabalho", _=>"Alternar Flick e Tracking" };
        result.Add(new(exercise,"Prioridade escolhida: "+priority,"Ajuda a comparar conforto e resultados da tua utilização.","Não determina uma sensibilidade ideal nem altera o rato por si só.","Baixa — depende do utilizador"));
        return result;
    }
}
