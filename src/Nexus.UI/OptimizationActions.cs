using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Optimization;
using Nexus.Diagnostics;
using System.Diagnostics;
using Nexus.Hardware;
using Nexus.Network;
using Nexus.Gaming;
using Nexus.Storage;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private SystemOptimizer optimizer = null!;
    private bool operationRunning;
    private CancellationTokenSource? dnsCancellation;
    private GameSessions sessions = null!;
    private readonly TemporaryCleaner temporaryCleaner = new();
    private TemporaryScan? temporaryScan;
    private CancellationTokenSource? temporaryCancellation;
    private void InitializeOptimizer(string root)
    {
        optimizer = new(Path.Combine(root, "nexus.db"), WindowsSettings.Resolve);
        sessions = new(Path.Combine(root, "nexus.db"), optimizer);
        foreach (var item in UserPreferences.All)
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new CheckBox { Content = item.Name, Tag = item });
            row.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap, Opacity = .7 });
            PreferenceChoices.Children.Add(row);
        }
        Closed += (_, _) => { dnsCancellation?.Cancel(); temporaryCancellation?.Cancel(); };
        InitializeMore(root);
        InitializeExpanded(root);
        InitializeProduct();
        RefreshSystem();
    }
    private void RefreshSystem()
    {
        try
        {
            var plans = WindowsSettings.Plans(); PowerPlans.ItemsSource = plans;
            if (SessionPlans.ItemsSource is null) SessionPlans.ItemsSource = plans;
            var active = Guid.Parse(WindowsSettings.Resolve("power").Read());
            PowerPlans.SelectedItem = plans.FirstOrDefault(x => x.Id == active);
            CurrentPower.Text = "Atual: " + (plans.FirstOrDefault(x => x.Id == active)?.Name ?? active.ToString());
        }
        catch (Exception e) { CurrentPower.Text = "Plano indisponível: " + e.Message; }
        try
        {
            var items = optimizer.History();
            SystemHistory.Text = items.Count == 0 ? "Ainda não aplicaste alterações ao Windows." : string.Join("\n\n", items.Take(80).Select(x => $"{(DateTimeOffset.TryParse(x.At, out var at) ? at.ToString("dd/MM HH:mm") : x.At)} · {x.Name}\n{StateName(x.State)} · {x.Before} → {x.After}"));
        }
        catch (Exception ex) { SystemHistory.Text = "Histórico indisponível: " + ex.Message; AppLog.Error(ex, "Ler histórico"); }
        try { var events = activityStore.History(); ActivityHistoryText.Text = events.Count == 0 ? "Ainda não há eventos de memória, rede, aplicações ou limpeza." : string.Join("\n\n", events.Select(x => x.Display)); }
        catch (Exception ex) { ActivityHistoryText.Text = "Eventos indisponíveis: " + ex.Message; }
        RefreshSessions();
    }
    private static string StateName(string state) => state switch { "applied" => "Aplicado", "pending" => "Interrompido — verificar/restaurar", "reverted" => "Reposto", "failed" => "Falhou sem alteração", _ => state };
    private void Navigate(object sender, RoutedEventArgs e) => ShowPage((string)((Button)sender).Tag);
    private void ShowPage(string page)
    {
        OverviewPanel.Visibility = page == "overview" ? Visibility.Visible : Visibility.Collapsed;
        OptimizePanel.Visibility = page == "optimize" ? Visibility.Visible : Visibility.Collapsed;
        GamingPanel.Visibility = page == "gaming" ? Visibility.Visible : Visibility.Collapsed;
        MemoryPanel.Visibility = page == "memory" ? Visibility.Visible : Visibility.Collapsed;
        NetworkPanel.Visibility = page == "network" ? Visibility.Visible : Visibility.Collapsed;
        BenchmarkPanel.Visibility = page == "benchmark" ? Visibility.Visible : Visibility.Collapsed;
        ToolsPanel.Visibility = page is "tools" or "utilities" ? Visibility.Visible : Visibility.Collapsed;
        PreferencesPanel.Visibility = page == "preferences" ? Visibility.Visible : Visibility.Collapsed;
        AppsPanel.Visibility = page == "apps" ? Visibility.Visible : Visibility.Collapsed;
        LatencyPanel.Visibility = page == "latency" ? Visibility.Visible : Visibility.Collapsed;
        CatalogPanel.Visibility = page == "catalog" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPanel.Visibility = page == "history" ? Visibility.Visible : Visibility.Collapsed;
        TweaksPanel.Visibility = page == "tweaks" ? Visibility.Visible : Visibility.Collapsed;
        AutoCleanupPanel.Visibility = page == "autoclean" ? Visibility.Visible : Visibility.Collapsed;
        TunerPanel.Visibility = page == "tuner" ? Visibility.Visible : Visibility.Collapsed;
        HelpPanel.Visibility = page == "help" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page switch { "optimize" => "PC Boost", "gaming" => "Game Mode", "tuner" => "App Tuner", "memory" => "Memória", "network" => "Rede", "benchmark" => "Testes e FPS", "tools" or "utilities" => "Ferramentas do Windows", "history" => "Histórico e restauro", "preferences" or "apps" => "Windows", "latency" => "Latência", "tweaks" => "Tweaks", "autoclean" => "Auto Limpeza", "help" => "Ajuda", "catalog" => "Cobertura da referência", _ => "Início" };
        foreach (var button in NavigationButtons.Children.OfType<Button>())
            button.Background = Color((string)button.Tag == page ? "#294530" : "#121B16");
        if (page is "optimize" or "history") RefreshSystem();
        if (page == "gaming") RefreshGames(this, new());
        if (page == "memory") RefreshMemory(this, new());
        if (page == "benchmark") RefreshBenchmarkHistory();
        if (page == "tuner") RefreshTuner(this, new());
        if (page is "tweaks" or "preferences" or "overview" or "history") RefreshAdjustmentCards(this, new());
        PageScroll.ChangeView(null, 0, null, disableAnimation: true);
    }
    private void Notice(string message, bool error = false)
    {
        OperationInfo.Message = message; OperationInfo.Severity = error ? InfoBarSeverity.Warning : InfoBarSeverity.Success; OperationInfo.IsOpen = true;
        AppLog.Info(message);
    }
    private async Task<bool> Confirm(string title, string message)
    {
        if (closed) return false;
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = ElementTheme.Dark, Title = title, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, PrimaryButtonText = "Aplicar", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close };
        var result = await dialog.ShowAsync();
        return !closed && result == ContentDialogResult.Primary;
    }
    private async Task Run(Func<Task> action)
    {
        if (operationRunning || benchmarkCancellation is not null || autoCleanupBusy || temporaryCancellation is not null) { Notice("Espera pela operação em curso ou cancela-a.", true); return; }
        operationRunning = true; ApplySystemButton.IsEnabled = PriorityButton.IsEnabled = UndoSystemButton.IsEnabled = StartSessionButton.IsEnabled = EndSessionButton.IsEnabled = false;
        try { await action(); }
        catch (Exception e) { AppLog.Error(e, "Otimização do sistema"); if (!closed) Notice(e.Message, true); }
        finally { operationRunning = false; if (!closed) { ApplySystemButton.IsEnabled = PriorityButton.IsEnabled = UndoSystemButton.IsEnabled = StartSessionButton.IsEnabled = EndSessionButton.IsEnabled = true; RefreshSystem(); RefreshAdjustmentCards(this, new()); } }
    }
    private async void ApplySystem(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var changes = new List<(string Key, string Value, string Summary)>();
        if (PowerChoice.IsChecked == true)
        {
            if (PowerPlans.SelectedItem is not PowerPlan plan) throw new InvalidOperationException("Escolhe um plano de energia disponível.");
            changes.Add(("power", plan.Id.ToString(), "Plano de energia: " + plan.Name + ". Pode alterar consumo, temperatura e autonomia."));
        }
        if (AnimationChoice.IsChecked == true) changes.Add(("animations", "0", "Desativar animações de conteúdo do Windows."));
        foreach (var row in PreferenceChoices.Children.OfType<StackPanel>())
            if (row.Children[0] is CheckBox { IsChecked: true, Tag: UserAdjustment item })
                changes.Add((item.Key, item.Value, item.Name + ": " + item.Description));
        if (changes.Count == 0) { Notice("Seleciona pelo menos um ajuste.", true); return; }
        if (!await Confirm("Aplicar estes ajustes?", string.Join("\n\n", changes.Select(x => x.Summary)) + "\n\nPodes desfazer cada alteração no Histórico. Não são alterados serviços nem proteções do Windows.")) return;
        var results = new List<string>();
        foreach (var change in changes)
        {
            try { results.Add(optimizer.Apply(change.Key, change.Value) ? "Aplicado: " + change.Summary : "Já estava configurado: " + change.Summary); }
            catch (Exception ex) { results.Add("Falhou: " + ex.Message + " As alterações anteriores continuam guardadas no Histórico."); Notice(string.Join("\n", results), true); return; }
        }
        Notice(string.Join("\n", results));
    });
    private void RefreshGames(object sender, RoutedEventArgs e)
    {
        try
        {
            var current = (GameList.SelectedItem as GameProcess)?.Key;
            var games = WindowsSettings.Games(); GameList.ItemsSource = games;
            GameList.SelectedItem = games.FirstOrDefault(x => x.Key == current) ?? games.FirstOrDefault(x => x.Name.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) { Notice(ex.Message, true); }
    }
    private async void ApplyPriority(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (GameList.SelectedItem is not GameProcess game) { Notice("Abre e seleciona primeiro o jogo ou aplicação.", true); return; }
        if (!await Confirm("Priorizar " + game.Name + "?", "A prioridade de CPU fica acima do normal até a aplicação fechar. Outras apps podem ficar menos responsivas. Podes repor no Histórico. Não há garantia de aumento de FPS.")) return;
        if (WindowsSettings.Resolve(game.Key).Read() != "Normal") throw new InvalidOperationException("A aplicação terminou ou a prioridade mudou. Atualiza a lista.");
        optimizer.Apply(game.Key, "AboveNormal"); Notice("Prioridade aplicada a " + game.Name + "."); RefreshGames(this, new());
    });
    private async void UndoSystem(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (await Confirm("Repor a última alteração?", "Será restaurado o valor anterior guardado pelo NEXUS, se a definição não tiver sido alterada por outra aplicação.")) Notice(optimizer.Undo());
    });
    private async void OpenSetting(object sender, RoutedEventArgs e)
    {
        try { if (!await Windows.System.Launcher.LaunchUriAsync(new Uri((string)((Button)sender).Tag))) Notice("O Windows não conseguiu abrir estas definições.", true); }
        catch (Exception ex) { Notice(ex.Message, true); }
    }
    private void OpenTool(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, (string)((Button)sender).Tag)) { UseShellExecute = true }); }
        catch (Exception ex) { Notice(ex.Message, true); }
    }
    private async void Analyze(object sender, RoutedEventArgs e)
    {
        try
        {
        await model.TickAsync();
        if (closed) return;
        var notes = new List<string>();
        if (model.MemoryPercent > 80) notes.Add("RAM acima de 80% nesta amostra: revê aplicações abertas no Gestor de Tarefas antes de jogar.");
        if (model.CpuPercent > 80) notes.Add("CPU acima de 80% nesta amostra: identifica a aplicação responsável; uma leitura isolada não é um diagnóstico.");
        foreach (var drive in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed))
            try { if (drive.IsReady && drive.TotalSize > 0 && (double)drive.AvailableFreeSpace / drive.TotalSize < .15) notes.Add($"Pouco espaço em {drive.Name}: usa Armazenamento para rever ficheiros temporários."); } catch (IOException) { }
        if (notes.Count == 0) notes.Add("Sem pressão elevada de CPU/RAM ou pouco espaço nos volumes acessíveis nesta amostra.");
        notes.Add("Para jogos: revê o plano de energia e o Modo de Jogo. Não foram feitas alterações.");
        AnalysisText.Text = string.Join("\n\n", notes);
        }
        catch (Exception ex) { Notice("Não foi possível concluir a análise: " + ex.Message, true); }
    }
    private void RefreshMemory(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = MemoryProcesses.Read();
            MemoryAppChoice.ItemsSource = items;
            MemoryProcessesText.Text = items.Count == 0 ? "Não foram encontrados processos acessíveis." : string.Join("\n\n", items.Take(50).Select(x => x.Display));
        }
        catch (Exception ex) { Notice(ex.Message, true); }
    }
    private async void MeasureDns(object sender, RoutedEventArgs e)
    {
        if (dnsCancellation is not null) return;
        using var cancellation = new CancellationTokenSource(); dnsCancellation = cancellation;
        DnsButton.IsEnabled = false; CancelDnsButton.IsEnabled = true; DnsResultsText.Text = "A medir…";
        try
        {
            var results = await new DnsBenchmark().MeasureAsync(cancellation.Token);
            if (!closed) DnsResultsText.Text = string.Join("\n\n", results.Select(x => x.Display));
        }
        catch (OperationCanceledException) { if (!closed) DnsResultsText.Text = "Medição cancelada."; }
        catch (Exception ex) { if (!closed) Notice("Medição DNS falhou: " + ex.Message, true); }
        finally { dnsCancellation = null; if (!closed) { DnsButton.IsEnabled = true; CancelDnsButton.IsEnabled = false; } }
    }
    private void CancelDns(object sender, RoutedEventArgs e) => dnsCancellation?.Cancel();
    private void RefreshSessions()
    {
        try
        {
            var active = sessions.Active;
            var recovery = sessions.RecoveryRequired;
            SessionStatusText.Text = active is null ? "Sem sessão ativa." : active.Name + " · " + (!recovery && active.Status == "active" ? "A acompanhar" : "Reposição necessária") + "\n" + (active.Error ?? (recovery ? "Usa Terminar e repor para recuperar as definições desta sessão." : ""));
            var entries = sessions.History();
            SessionHistoryText.Text = entries.Count == 0 ? "Ainda não há sessões." : string.Join("\n\n", entries.Take(30).Select(x => $"{x.Name} · {(x.Status == "ended" ? "Terminada e reposta" : x.Status == "active" ? "Ativa" : "Requer reposição")}\n{(DateTimeOffset.TryParse(x.Started, out var at) ? at.ToString("dd/MM HH:mm") : x.Started)} {x.Error}"));
            if (!operationRunning) { StartSessionButton.IsEnabled = active is null; EndSessionButton.IsEnabled = active is not null; }
        }
        catch (Exception ex) { SessionStatusText.Text = "Sessões indisponíveis: " + ex.Message; AppLog.Error(ex, "Ler sessões"); }
    }
    private async void StartSession(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (GameList.SelectedItem is not GameProcess game) { Notice("Seleciona a aplicação aberta acima.", true); return; }
        var priority = SessionPriorityChoice.IsChecked == true;
        PowerPlan? plan = null;
        if (SessionPowerChoice.IsChecked == true) plan = SessionPlans.SelectedItem as PowerPlan ?? throw new InvalidOperationException("Escolhe o plano de energia da sessão.");
        var animations = SessionAnimationsChoice.IsChecked == true;
        if (!priority && plan is null && !animations) { Notice("Seleciona pelo menos um ajuste para a sessão.", true); return; }
        var summary = "Aplicação: " + game.Name + ".\n" + (priority ? "Prioridade acima do normal. Pode reduzir a fluidez de outras apps.\n" : "") + (animations ? "Reduz animações durante a sessão.\n" : "") + (plan is not null ? "Plano: " + plan.Name + ". Pode aumentar consumo e temperatura.\n" : "") + "\nMantém o NEXUS aberto para repor automaticamente quando a aplicação terminar. Se fechares o NEXUS, usa Terminar e repor ao voltar a abrir.";
        if (!await Confirm("Iniciar esta sessão?", summary) || closed) return;
        var session = sessions.Start(game.Key, game.Name, plan?.Id.ToString(), priority, animations);
        Notice(session.Status == "active" ? "Sessão iniciada: " + game.Name + "." : session.Error ?? "Reposição necessária.", session.Status != "active");
    });
    private async void EndSession(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (!await Confirm("Terminar e repor a sessão?", "Repõe os ajustes guardados para esta sessão. O jogo continua aberto. As alterações feitas por outras apps são verificadas antes de repor.") || closed) return;
        var result = sessions.End();
        Notice(result is null ? "Não há sessão por terminar." : result.Status == "ended" ? "Sessão terminada e ajustes repostos." : result.Error ?? "Há ajustes por repor.", result?.Status == "recovery");
    });
    private void PollSession()
    {
        if (operationRunning || closed) return;
        try
        {
            var before = sessions.Active;
            if (before is null || before.Status != "active" || sessions.RecoveryRequired) return;
            var result = sessions.Poll();
            if (result is not null && result.Status != "active")
            {
                Notice(result.Status == "ended" ? "O jogo terminou. Os ajustes da sessão foram repostos." : result.Error ?? "Há ajustes da sessão por repor.", result.Status != "ended");
                AutomationStatus.Text = result.Status != "ended" ? "Recuperação necessária · " + result.Error : gameAutomation.Enabled ? "Automático ativo · a aguardar próximo jogo." : "Sessão terminada e reposta.";
                RefreshSystem();
                RefreshAdjustmentCards(this, new());
            }
        }
        catch (Exception ex) { AppLog.Error(ex, "Acompanhar sessão"); SessionStatusText.Text = "Não foi possível acompanhar a sessão. Usa Terminar e repor."; }
    }
    private async void ScanTemporary(object sender, RoutedEventArgs e)
    {
        if (temporaryCancellation is not null || autoCleanupBusy || operationRunning) return;
        if (CleanupLocationChoice.SelectedItem is not CleanupLocation location) { Notice("Escolhe uma categoria disponível.", true); return; }
        using var cancellation = new CancellationTokenSource(); temporaryCancellation = cancellation;
        ScanTemporaryButton.IsEnabled = RecycleTemporaryButton.IsEnabled = false;
        CleanupLocationChoice.IsEnabled = false;
        TemporarySummaryText.Text = "A analisar " + location.Name + "…";
        try
        {
            var scan = await Task.Run(() => temporaryCleaner.Scan(location.Path, cancellation.Token));
            if (closed) return;
            temporaryScan = scan; TemporaryFiles.ItemsSource = scan.Files;
            TemporarySummaryText.Text = $"{scan.Files.Count} ficheiros candidatos · {scan.CandidateBytes / 1048576d:F1} MB · {scan.ExaminedFiles} examinados" + (scan.Limited ? "\nA análise atingiu o limite; não inclui toda a pasta." : "") + (scan.Issues.Count > 0 ? $"\n{scan.Issues.Count} locais não puderam ser analisados." : "");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closed) Notice("Análise de temporários falhou: " + ex.Message, true); }
        finally { temporaryCancellation = null; if (!closed) { CleanupLocationChoice.IsEnabled = true; ScanTemporaryButton.IsEnabled = true; RecycleTemporaryButton.IsEnabled = temporaryScan?.Files.Count > 0; } }
    }
    private async void RecycleTemporary(object sender, RoutedEventArgs e)
    {
        if (temporaryCancellation is not null || temporaryScan is null || autoCleanupBusy || operationRunning) return;
        var selected = TemporaryFiles.SelectedItems.OfType<TemporaryFileCandidate>().ToArray();
        if (selected.Length == 0) { Notice("Seleciona os ficheiros que queres enviar à Lixeira.", true); return; }
        var location = CleanupLocationChoice.SelectedItem as CleanupLocation;
        if (sessions.Active is not null || GameLibrary.Detect(library.Read()).Count != 0) { Notice("Termina a sessão e fecha aplicações da biblioteca antes de reciclar ficheiros.", true); return; }
        using var cancellation = new CancellationTokenSource(); temporaryCancellation = cancellation;
        ScanTemporaryButton.IsEnabled = RecycleTemporaryButton.IsEnabled = false;
        try
        {
            if (!await Confirm("Enviar à Lixeira?", $"{selected.Length} ficheiros selecionados · {selected.Sum(x => x.Bytes) / 1048576d:F1} MB.\n{location?.Name}\n{location?.Description}\n\nFecha jogos/launchers antes de limpar caches. Serão revalidados antes da ação. Podes recuperá-los na Lixeira do Windows. O espaço continua ocupado até esvaziares a Lixeira por tua decisão.") || closed) return;
            var result = await temporaryCleaner.RecycleAsync(temporaryScan, selected, cancellation.Token);
            if (closed) return;
            Notice($"Enviados à Lixeira: {result.Recycled} · {result.RecycledBytes / 1048576d:F1} MB. Ignorados: {result.Skipped}. Erros: {result.Errors.Count}." + (result.Cancelled ? " Ação cancelada." : "") + (result.Limited ? " A ação atingiu o limite de tempo." : "") + (result.Errors.Count > 0 ? "\n" + string.Join("\n", result.Errors.Take(3).Select(x => x.Message)) : ""), result.Errors.Count > 0);
            SaveActivity("Limpeza", $"{location?.Name} · {result.Recycled} ficheiros enviados à Lixeira · {result.RecycledBytes} bytes · {result.Errors.Count} erros; Lixeira não esvaziada.");
            temporaryScan = null; TemporaryFiles.ItemsSource = null; TemporarySummaryText.Text = "Volta a analisar para atualizar os candidatos.";
        }
        catch (Exception ex) { if (!closed) Notice("Reciclagem falhou: " + ex.Message, true); }
        finally { temporaryCancellation = null; if (!closed) { ScanTemporaryButton.IsEnabled = true; RecycleTemporaryButton.IsEnabled = temporaryScan?.Files.Count > 0; } }
    }
}
