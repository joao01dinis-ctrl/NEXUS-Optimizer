namespace Nexus.Optimization;

public sealed record OptimizationPreset(string Name, string Description, bool ReduceContentAnimations, string[] PreferenceKeys)
{ public override string ToString() => Name; }

public static class OptimizationPresets
{
    public static IReadOnlyList<OptimizationPreset> All { get; } = [
        new("Normal", "Limpa a seleção para escolheres os teus ajustes. Não repõe alterações já aplicadas: usa o Histórico para isso.", false, []),
        new("Gaming", "Seleciona animações de conteúdo, menus, dicas, listas de escolha e minimizar. Não altera energia ou o jogo; revê a seleção antes de aplicar.", true, ["menu-animation", "tooltip-animation", "combo-animation", "minimize-animation"]),
        new("Trabalho", "Seleciona animações de menus e dicas. Mantém o conteúdo ao arrastar janelas e a repetição do teclado. Revê a seleção antes de aplicar.", false, ["menu-animation", "tooltip-animation"])
    ];
}
