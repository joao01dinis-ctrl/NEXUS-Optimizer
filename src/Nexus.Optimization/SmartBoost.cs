using Nexus.Core;

namespace Nexus.Optimization;

public enum BoostGoal { Balanced, Competitive, Streaming }
public enum BoostDecision { Apply, Ignore, ManualTest }
public sealed record BoostVolume(string Name, long TotalBytes, long FreeBytes);
public sealed record BoostProcess(string Name, double CpuPercent);
public sealed record BoostSetting(string Key, string Name, string Desired, string? Current, string? Error = null);
public sealed record BoostInput(BoostGoal Goal, IReadOnlyList<Sample> Samples, IReadOnlyList<BoostVolume> Volumes,
    IReadOnlyList<BoostProcess> Processes, bool? OnAcPower, bool? BatterySaver, string Cpu, string Gpu,
    string Windows, IReadOnlyList<string> Drivers, IReadOnlyList<BoostSetting> Settings);
public sealed record BoostRule(string Id, BoostDecision Decision, string Title, string Reason,
    string Measurement, string Revert, string? SettingKey = null);
public sealed record BoostReport(string RuleVersion, DateTimeOffset At, BoostInput Input,
    bool SustainedCpuPressure, bool SustainedMemoryPressure, IReadOnlyList<BoostRule> Rules);

// Pure, versioned decisions. This engine never writes Windows settings or processes.
public static class SmartBoost
{
    public const string RuleVersion = "1.0.0";
    public static IReadOnlyList<string> VisualKeys { get; } = Array.AsReadOnly(new[] { "animations", "menu-animation", "tooltip-animation", "combo-animation", "minimize-animation" });
    public static bool IsFresh(BoostReport? report, DateTimeOffset now) => report is not null && now >= report.At && now - report.At <= TimeSpan.FromMinutes(5);
    public static string DecisionName(BoostDecision decision) => decision switch
    { BoostDecision.Apply => "Recomendado", BoostDecision.Ignore => "Ignorar", _ => "Testar manualmente" };

    public static BoostReport Evaluate(BoostInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.Goal)) throw new ArgumentException("Objetivo desconhecido.");
        // Five spaced, valid observations are required. Unknown/invalid data never
        // becomes a zero usage measurement or a reason to modify Windows.
        var samples = input.Samples;
        var valid = samples.Count is >= 5 and <= 30 && samples.All(s =>
            s.CpuPercent is null || double.IsFinite(s.CpuPercent.Value) && s.CpuPercent is >= 0 and <= 100) &&
            samples.All(s => double.IsFinite(s.MemoryPercent) && s.MemoryPercent is >= 0 and <= 100 &&
                double.IsFinite(s.TotalGb) && s.TotalGb > 0 && double.IsFinite(s.UsedGb) && s.UsedGb >= 0 && s.UsedGb <= s.TotalGb) &&
            samples.Zip(samples.Skip(1), (a, b) => (b.At - a.At).TotalSeconds).All(t => t is >= 1 and <= 10) &&
            (samples[^1].At - samples[0].At).TotalSeconds >= 8;
        var cpuKnown = valid && samples.All(s => s.CpuPercent is not null);
        var cpu = cpuKnown && samples.Count(s => s.CpuPercent >= 80) >= Math.Ceiling(samples.Count * .8);
        var ram = valid && samples.Count(s => s.MemoryPercent >= 85) >= Math.Ceiling(samples.Count * .8);
        var rules = new List<BoostRule>();
        foreach (var key in VisualKeys)
        {
            var setting = input.Settings.SingleOrDefault(s => s.Key == key);
            var decision = BoostDecision.Ignore;
            var reason = "Não há pressão sustentada que justifique recomendar esta preferência.";
            if (setting is null || setting.Error is not null || setting.Current is null) reason = "O valor atual não está acessível. Não será alterado.";
            else if (setting.Current == setting.Desired) reason = "O valor pretendido já está configurado. Nenhuma alteração necessária.";
            else if (!valid) reason = "Amostras incompletas ou inválidas. Repete a análise antes de escolher ajustes.";
            else if (cpu || ram) { decision = BoostDecision.Apply; reason = "Pressão sustentada observada: podes reduzir transições da interface. O efeito nos FPS precisa de medição."; }
            else if (input.Goal == BoostGoal.Competitive) { decision = BoostDecision.ManualTest; reason = "Preferência opcional de interface; não foi detetada pressão sustentada. Testa antes/depois se a escolheres."; }
            rules.Add(new("nexus.visual." + key, decision, setting?.Name ?? key, reason,
                "Comparar fluidez da interface e capturas equivalentes no FPS Lab.", "Repor o valor anterior no Histórico.", key));
        }
        rules.Add(new("nexus.cpu.contention", cpu ? BoostDecision.ManualTest : BoostDecision.Ignore, "Competição por CPU",
            !cpuKnown ? "CPU indisponível ou amostras insuficientes: a carga não foi avaliada." : cpu ? "CPU acima de 80% em pelo menos 80% das amostras. Revê as aplicações consumidoras; isto não identifica um limite CPU/GPU do jogo." : "Não foi observada carga de CPU sustentada acima do limiar.",
            "Medir CPU das aplicações e comparar frametimes no mesmo cenário.", "Consulta apenas; não fecha ou suspende processos."));
        rules.Add(new("nexus.memory.pressure", ram ? BoostDecision.ManualTest : BoostDecision.Ignore, "Pressão de memória",
            !valid ? "Não há amostras suficientes para avaliar a memória." : ram ? "RAM acima de 85% em pelo menos 80% das amostras. Revê consumidores e paginação no Windows antes de iniciar o jogo." : "Não foi observada utilização de RAM sustentada acima do limiar.",
            "Consultar Memória/Gestor de Tarefas; uso elevado não prova paginação ou ganho após limpeza.", "Não limpa standby memory nem desativa o ficheiro de paginação."));
        var low = input.Volumes.Where(v => v.TotalBytes > 0 && v.FreeBytes >= 0 && v.FreeBytes <= v.TotalBytes && (double)v.FreeBytes / v.TotalBytes < .15).ToArray();
        rules.Add(new("nexus.storage.space", low.Length > 0 ? BoostDecision.ManualTest : BoostDecision.Ignore, "Espaço disponível",
            low.Length > 0 ? "Menos de 15% livres: " + string.Join(", ", low.Select(v => v.Name)) + ". Revê os ficheiros antes da limpeza." : input.Volumes.Count == 0 ? "Volumes não disponíveis. Espaço livre não foi avaliado." : "Sem pouco espaço nos volumes acessíveis.",
            "Rever Armazenamento e candidatos à Lixeira; não esvaziar caches durante comparações.", "Recuperar ficheiros na Lixeira, se ainda existirem."));
        rules.Add(new("nexus.power.session", input.OnAcPower == true && input.BatterySaver == false ? BoostDecision.ManualTest : BoostDecision.Ignore, "Energia durante o jogo",
            input.OnAcPower == false ? "PC em bateria: mantém energia e autonomia. Não recomenda um plano de maior consumo." : input.OnAcPower is null || input.BatterySaver is null ? "Estado da alimentação incompleto: não recomenda mudar o plano." : input.BatterySaver == true ? "Poupança de bateria ativa: não recomenda aumentar consumo." : "Ligado à corrente. Um plano existente pode ser testado por sessão; não há plano universalmente melhor.",
            "Comparar consumo, temperatura e frametimes com as mesmas condições.", "A sessão guarda e repõe o plano anterior; nada muda nesta análise."));
        rules.Add(new("nexus.game.priority", input.Goal == BoostGoal.Streaming ? BoostDecision.Ignore : BoostDecision.ManualTest, "Prioridade do jogo",
            input.Goal == BoostGoal.Streaming ? "Mantém a prioridade para partilhar recursos com OBS, Discord e áudio." : "Mantém a prioridade original por defeito. Acima do normal só deve ser escolhido e medido por ti.",
            "Comparar frametimes e responsividade das outras aplicações.", "A sessão repõe a prioridade anterior; nunca usa Real Time."));
        rules.Add(new("nexus.startup.review", BoostDecision.ManualTest, "Aplicações de arranque",
            "Revê aplicações opcionais nas Definições do Windows. A análise não decide quais são dispensáveis.", "Comparar tempo de arranque e carga após iniciar sessão.", "Reativar a mesma aplicação nas Definições do Windows."));
        rules.Add(new("nexus.gpu.driver", BoostDecision.ManualTest, "GPU e driver",
            input.Drivers.Count == 0 ? "Driver não disponível. Não se deduz uma versão desatualizada." : "Versão instalada identificada. Não foi consultada nem instalada uma atualização.",
            "Validar versão no fabricante; GPU Busy, VRAM, temperaturas e clocks ainda não são medidos.", "Consulta apenas; não modifica drivers ou configurações da GPU."));
        return new(RuleVersion, samples.Count == 0 ? DateTimeOffset.Now : samples[^1].At, input, cpu, ram, rules.AsReadOnly());
    }
}
