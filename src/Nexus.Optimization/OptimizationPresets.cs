namespace Nexus.Optimization;

public sealed record OptimizationPreset(string Name, string Description, bool ReduceContentAnimations, string[] PreferenceKeys)
{ public override string ToString() => Name; }

public static class OptimizationPresets
{
    public static IReadOnlyList<OptimizationPreset> All { get; } = [
        new("Balanceado", "Analisa pressão sustentada antes de recomendar preferências. Mantém prioridade, energia e segurança. Perfil predefinido.", false, []),
        new("Competitivo", "Ajustes de interface opcionais. Energia e prioridade precisam de escolha manual e comparação em jogo; não há ganho presumido.", true, ["menu-animation", "tooltip-animation", "combo-animation", "minimize-animation"]),
        new("Streaming", "Mantém prioridade para partilhar CPU com jogo, OBS, Discord e áudio. Não fecha programas; ajustes de interface só após revisão.", false, [])
    ];
}
