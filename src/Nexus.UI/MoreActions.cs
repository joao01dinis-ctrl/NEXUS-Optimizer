using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Benchmark;
using Nexus.Diagnostics;
using Nexus.Network;
using Nexus.Optimization;
using Nexus.Storage;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private BenchmarkStore benchmarkStore = null!;
    private CancellationTokenSource? benchmarkCancellation;
    private sealed record SearchTarget(string Title, string Page);
    private static readonly SearchTarget[] SearchTargets = [
        new("Dashboard e hardware", "overview"), new("Analisar CPU e RAM", "overview"),
        new("Plano de energia", "optimize"), new("Animações do Windows", "optimize"), new("Perfis de otimização", "optimize"),
        new("Prioridade da aplicação", "gaming"), new("Sessão de jogo", "gaming"), new("Verificar FPS e ping no FiveM", "gaming"),
        new("Processos e memória RAM", "memory"), new("CPU de Discord e outras apps", "memory"), new("IP, ligação e gateway", "network"), new("DNS atual e comparação", "network"),
        new("Benchmark de CPU e memória", "benchmark"), new("Comparar testes", "benchmark"), new("FPS e capturas PresentMon", "benchmark"),
        new("Limpeza de temporários", "tools"), new("Ferramentas do Windows", "tools"),
        new("Desfazer e restauro", "history"), new("Exportar cópia de segurança", "history")
        , new("PC Boost analisar e aplicar", "optimize"), new("Game Mode automático", "gaming"), new("App Tuner programas", "tuner"), new("Tweaks interruptores e restauro", "tweaks"),
        new("Biblioteca e perfis de jogo", "gaming"), new("Privacidade do Windows e Edge", "preferences"),
        new("Remover aplicações nativas opcionais", "apps"), new("Latência, ping e perda de rede", "latency"),
        new("Catálogo de funções TL e estado NEXUS", "catalog"), new("Auto limpeza de temporários", "autoclean"), new("Ajuda FiveM", "help")
    ];
    private void InitializeMore(string root)
    {
        benchmarkStore = new(Path.Combine(root, "nexus.db"));
        InitializeFrameTimes(root);
        InitializeProcessCpu();
        InitializeFiveMHelp();
        OptimizationPresetList.ItemsSource = OptimizationPresets.All;
        OptimizationPresetList.SelectedIndex = 1;
        Closed += (_, _) => benchmarkCancellation?.Cancel();
        RefreshBenchmarkHistory();
    }
    private void SearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            sender.ItemsSource = SearchTargets.Where(x => x.Title.Contains(sender.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }
    private void SearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var target = args.ChosenSuggestion as SearchTarget ?? SearchTargets.FirstOrDefault(x => string.Equals(x.Title, args.QueryText, StringComparison.CurrentCultureIgnoreCase));
        if (target is null) { Notice("Escolhe um resultado da pesquisa.", true); return; }
        ShowPage(target.Page); sender.Text = "";
    }
    private void ChoosePreset(object sender, RoutedEventArgs e)
    {
        if (operationRunning || OptimizationPresetList.SelectedItem is not OptimizationPreset preset) return;
        PowerChoice.IsChecked = false; AnimationChoice.IsChecked = preset.ReduceContentAnimations;
        foreach (var row in PreferenceChoices.Children.OfType<StackPanel>())
            if (row.Children[0] is CheckBox { Tag: UserAdjustment item } choice) choice.IsChecked = preset.PreferenceKeys.Contains(item.Key);
        PresetDescription.Text = preset.Description;
        Notice("Seleção preparada para " + preset.Name + ". Revê os ajustes e usa Rever e aplicar selecionados.");
    }
    private async void RefreshNetwork(object sender, RoutedEventArgs e)
    {
        NetworkRefreshButton.IsEnabled = false;
        try
        {
            var adapters = await Task.Run(NetworkSnapshot.Read);
            if (!closed) NetworkAdaptersText.Text = adapters.Count == 0 ? "Não foi encontrado um adaptador acessível." : string.Join("\n\n", adapters.Select(x => x.Display));
        }
        catch (Exception ex) { if (!closed) Notice("Não foi possível consultar a ligação: " + ex.Message, true); }
        finally { if (!closed) NetworkRefreshButton.IsEnabled = true; }
    }
    private async void StartBenchmark(object sender, RoutedEventArgs e)
    {
        if (benchmarkCancellation is not null || operationRunning || autoCleanupBusy || temporaryCancellation is not null) return;
        using var cancellation = new CancellationTokenSource(); benchmarkCancellation = cancellation;
        BenchmarkButton.IsEnabled = false;
        try
        {
            if (sessions.Active is not null) { Notice("Termina e repõe a sessão antes de medir o desempenho.", true); return; }
            if (!await Confirm("Executar o teste curto?", "Usa uma tarefa de CPU e cerca de 17 MiB em buffers durante aproximadamente 4 segundos, mais preparação. Pode aumentar a temperatura e interferir com um jogo aberto. Fecha jogos e outras tarefas pesadas. O resultado não mede FPS ou latência e não aplica ajustes.")) return;
            if (closed) return;
            var previous = benchmarkStore.History().FirstOrDefault();
            CancelBenchmarkButton.IsEnabled = true; BenchmarkResultText.Text = "A testar CPU e depois cópia de memória…";
            var result = await new LocalBenchmark().RunAsync(model.CpuName, cancellation.Token);
            if (closed) return;
            benchmarkStore.Save(result);
            var comparison = previous is null ? null : LocalBenchmark.Compare(result, previous);
            BenchmarkResultText.Text = $"CPU · SHA-256: {result.CpuMegabytesPerSecond:F1} MiB/s\nCópia de memória: {result.CopyMegabytesPerSecond:F1} MiB/s\n" +
                (comparison is not null ? $"Variação face ao teste anterior: CPU {comparison.CpuPercent:+0.0;-0.0;0}% · cópia {comparison.CopyPercent:+0.0;-0.0;0}%.\nIsto é uma variação da medição, não um ganho atribuído às otimizações." : "Primeiro teste, ou ambiente/método diferente: sem comparação.");
            RefreshBenchmarkHistory();
        }
        catch (OperationCanceledException) { if (!closed) BenchmarkResultText.Text = "Teste cancelado. Nenhum resultado parcial foi guardado."; }
        catch (Exception ex) { AppLog.Error(ex, "Benchmark"); if (!closed) Notice("O teste não foi concluído: " + ex.Message, true); }
        finally { benchmarkCancellation = null; if (!closed) { BenchmarkButton.IsEnabled = true; CancelBenchmarkButton.IsEnabled = false; } }
    }
    private void CancelBenchmark(object sender, RoutedEventArgs e) => benchmarkCancellation?.Cancel();
    private void RefreshBenchmarkHistory()
    {
        try
        {
            var history = benchmarkStore.History();
            BenchmarkHistoryText.Text = history.Count == 0 ? "Ainda sem testes guardados." : string.Join("\n\n", history.Select(x => $"{x.At:dd/MM HH:mm} · CPU {x.CpuMegabytesPerSecond:F1} MiB/s · cópia {x.CopyMegabytesPerSecond:F1} MiB/s\n{x.Environment}"));
        }
        catch (Exception ex) { BenchmarkHistoryText.Text = "Histórico de testes indisponível."; AppLog.Error(ex, "Histórico de benchmark"); }
    }
    private async void ExportBackup(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(AppWindow.Id);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null || closed) return;
        var path = Path.Combine(folder.Path, "NEXUS-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".sqlite");
        await Task.Run(() => optimizer.ExportBackup(path));
        if (!closed) { BackupLocationText.Text = path; Notice("Cópia de segurança guardada. Não repõe ajustes nem substitui a base de dados atual."); }
    });
}
