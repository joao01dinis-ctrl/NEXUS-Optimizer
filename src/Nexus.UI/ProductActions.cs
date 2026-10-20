using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Gaming;
using Nexus.Optimization;
using System.Diagnostics;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private sealed record DirectAdjustment(string Key, string Name, string Description, string Desired, string? Unavailable = null);
    private sealed record AdjustmentCard(DirectAdjustment Item, ToggleSwitch Switch, TextBlock Status);
    private readonly List<AdjustmentCard> adjustmentCards = [];
    private bool refreshingAdjustments;
    private readonly SessionAutomation gameAutomation = new();
    private readonly List<(CheckBox Choice, DirectAdjustment Item, AdjustmentState State)> boostSelection = [];

    private void InitializeProduct()
    {
        foreach (var item in UserPreferences.All)
            AddAdjustmentCard(TweakCards, new(item.Key, item.Name, item.Description, item.Value));
        AddAdjustmentCard(TweakCards, new("animations", "Reduzir animações de conteúdo", "Mostra conteúdo com menos transições em aplicações compatíveis.", "0"));
        foreach (var policy in UserPolicies.All)
            AddAdjustmentCard(WindowsCards, new(policy.Key, policy.Name, policy.Description, policy.DesiredState, UserPolicies.Unsupported(policy)));
        AddAdjustmentCard(WindowsCards, new(UserAccessibility.StickyKeysKey, "Desativar Teclas de Aderência", "Desliga a função e o atalho de cinco toques no Shift. Se precisas desta acessibilidade, mantém a opção do Windows.", ""));
        TweakCards.SizeChanged += (_, _) => LayoutAdjustmentCards(TweakCards);
        WindowsCards.SizeChanged += (_, _) => LayoutAdjustmentCards(WindowsCards);
        LibraryProfiles.SelectionChanged += (_, _) =>
        {
            if (LibraryProfiles.SelectedItem is not GameProfile profile || profile.Name == "Personalizado") return;
            SessionPriorityChoice.IsChecked = profile.Priority;
            SessionAnimationsChoice.IsChecked = profile.Animations;
        };
        LibraryProfiles.SelectedIndex = 0;
        // Expanded initialization already selected item zero, so SelectionChanged
        // may not fire here. Explicitly initialise the controls from that profile.
        SessionPriorityChoice.IsChecked = GameLibrary.Profiles[0].Priority;
        SessionAnimationsChoice.IsChecked = GameLibrary.Profiles[0].Animations;
        SessionPowerChoice.IsChecked = false;
        AppWindow.Closing += (_, args) =>
        {
            if (operationRunning || autoCleanupBusy || benchmarkCancellation is not null || temporaryCancellation is not null)
            { args.Cancel = true; Notice("Termina ou cancela a operação antes de fechar.", true); return; }
            gameAutomation.Disable();
            try
            {
                if (sessions.RecoveryRequired)
                { args.Cancel = true; Notice("Há uma sessão anterior por recuperar. Usa Terminar e repor antes de fechar.", true); ShowPage("gaming"); return; }
                if (sessions.End() is { Status: not "ended" } remaining)
                { args.Cancel = true; Notice(remaining.Error ?? "Há ajustes de sessão por repor.", true); ShowPage("gaming"); }
            }
            catch (Exception error) { args.Cancel = true; Notice("A sessão não foi reposta: " + error.Message, true); }
        };
        RefreshAdjustmentCards(this, new());
        ShowPage("overview");
    }
    private static SolidColorBrush Color(string hex) => new(Windows.UI.Color.FromArgb(255,
        Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16)));
    private void AddAdjustmentCard(Grid parent, DirectAdjustment item)
    {
        var content = new StackPanel { Spacing = 10 };
        var heading = new Grid { ColumnSpacing = 12 };
        heading.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var label = new TextBlock { Text = item.Name, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        var toggle = new ToggleSwitch { OnContent = "", OffContent = "", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, item.Name);
        Grid.SetColumn(toggle, 1); heading.Children.Add(label); heading.Children.Add(toggle); content.Children.Add(heading);
        content.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Color("#C1CBCD") });
        var status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Color("#43DF79") }; content.Children.Add(status);
        var card = new AdjustmentCard(item, toggle, status); adjustmentCards.Add(card);
        toggle.Toggled += async (_, _) =>
        {
            if (refreshingAdjustments) return;
            var requested = toggle.IsOn;
            var observed = toggle.Tag as AdjustmentState;
            if (observed is null) return;
            await Run(() =>
            {
                if (sessions.Active is not null) throw new InvalidOperationException("Termina e repõe a sessão antes de alterar preferências permanentes.");
                AdjustmentStates.Set(optimizer, observed, requested);
                Notice(requested ? "Aplicado: " + item.Name : "Reposto o valor anterior: " + item.Name);
                return Task.CompletedTask;
            });
            RefreshAdjustmentCards(this, new());
        };
        parent.Children.Add(new Border { Child = content, Background = Color("#121819"), BorderBrush = Color("#283234"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(18) });
    }
    private static void LayoutAdjustmentCards(Grid grid)
    {
        var columns = grid.ActualWidth >= 640 ? 2 : 1;
        if (grid.Tag is int previous && previous == columns) return;
        grid.Tag = columns;
        grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < (grid.Children.Count + columns - 1) / columns; i++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (var i = 0; i < grid.Children.Count; i++)
            if (grid.Children[i] is FrameworkElement child) { Grid.SetColumn(child, i % columns); Grid.SetRow(child, i / columns); }
    }
    private void RefreshAdjustmentCards(object sender, RoutedEventArgs e)
    {
        refreshingAdjustments = true;
        try
        {
            foreach (var card in adjustmentCards)
            {
                var desired = card.Item.Desired;
                if (card.Item.Key == UserAccessibility.StickyKeysKey)
                {
                    try { desired = UserAccessibility.DisabledState(UserAccessibility.Resolve().Read()); }
                    catch (Exception error) { card.Status.Text = "Indisponível · " + error.Message; card.Switch.IsEnabled = false; continue; }
                }
                var state = AdjustmentStates.Read(optimizer, card.Item.Key, desired, unavailable: card.Item.Unavailable);
                card.Switch.Tag = state; card.Switch.IsOn = state.IsOn;
                card.Switch.IsEnabled = state.Error is null && (!state.IsOn || state.CanRestore) && sessions.Active is null;
                card.Status.Text = state.Label + (state.Error is not null ? " · " + state.Error : state.IsOn && !state.CanRestore ? " · sem alteração anterior da NEXUS" : "");
                ToolTipService.SetToolTip(card.Switch, state.Error ?? (state.CanRestore ? "Desligar repõe exatamente o valor anterior." : "Aplicar guarda o valor anterior para restauro."));
            }
            LayoutAdjustmentCards(TweakCards); LayoutAdjustmentCards(WindowsCards);
            var count = optimizer.History().Count(x => x.State is "applied" or "pending");
            GlobalChangeStatus.Text = count == 0 ? "Nenhuma alteração por repor" : $"{count} alterações por repor";
        }
        catch (Exception error) { Notice("Estados indisponíveis: " + error.Message, true); }
        finally { refreshingAdjustments = false; }
    }
    private void BoostPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        boostReport = null;
        boostSelection.Clear(); BoostChoices.Children.Clear();
        if (ApplyBoostButton is not null) ApplyBoostButton.IsEnabled = false;
        if (BoostStatus is not null) BoostStatus.Text = "Objetivo alterado. Volta a analisar para preparar a seleção.";
    }
    private async void AnalyzeBoost(object sender, RoutedEventArgs e) => await AnalyzeSmartBoost();
    private async void ApplyBoost(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (sessions.Active is not null) throw new InvalidOperationException("Termina e repõe a sessão antes de aplicar PC Boost.");
        if (!SmartBoost.IsFresh(boostReport, DateTimeOffset.Now)) throw new InvalidOperationException("A análise tem mais de cinco minutos ou foi invalidada. Volta a analisar antes de aplicar.");
        var selected = boostSelection.Where(x => x.Choice.IsChecked == true && x.Choice.IsEnabled).ToArray();
        if (selected.Length == 0) { Notice("Analisa o PC e escolhe pelo menos um ajuste disponível.", true); return; }
        if (!await Confirm("Aplicar PC Boost?", string.Join("\n\n", selected.Select(x => x.Item.Name + ": " + x.Item.Description)) + "\n\nPreferências de interface, sem ganho de FPS confirmado. Os valores anteriores ficam guardados. Restaurar alterações repõe o que a NEXUS mudou.")) return;
        if (!SmartBoost.IsFresh(boostReport, DateTimeOffset.Now)) throw new InvalidOperationException("A análise expirou durante a confirmação. Volta a analisar.");
        var applied = optimizer.ApplyBatch(selected.Select(x => new PreparedAdjustment(x.Item.Key, x.Item.Desired, x.State.Current!)).ToArray());
        BoostStatus.Text = $"PC Boost aplicado · {applied} ajustes. Os valores anteriores estão no Histórico. Ganhos de FPS ainda por medir.";
        foreach (var x in selected) x.Choice.IsEnabled = false;
        Notice("PC Boost aplicado. Podes repor com Restaurar alterações.");
    });
    private async void RestoreEverything(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (!await Confirm("Restaurar as alterações da NEXUS?", "Repõe os valores anteriores guardados, da alteração mais recente para a mais antiga. Mantém mudanças externas em conflito. Ficheiros enviados à Lixeira e apps desinstaladas não são repostos por este botão.")) return;
        AutoSafeToggle.IsOn = false;
        gameAutomation.Disable(); AutomationStatus.Text = "Automático pausado para restaurar.";
        var ended = sessions.End();
        var result = optimizer.RestoreAll(x => x.Owner is null);
        var errors = result.Errors.ToList();
        if (ended is { Status: not "ended" }) errors.Add(ended.Error ?? "Há ajustes da sessão por repor.");
        Notice($"{result.Restored} alterações repostas." + (errors.Count == 0 ? "" : "\n" + string.Join("\n", errors)), errors.Count > 0);
        boostReport = null; boostSelection.Clear(); BoostChoices.Children.Clear(); BoostStatus.Text = "Volta a analisar para preparar uma nova seleção.";
    });
    private async void ToggleGameAutomation(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (gameAutomation.Enabled) { gameAutomation.Disable(); AutomationStatus.Text = "Pausado. As sessões já iniciadas continuam até terminar e repor."; return; }
        if (sessions.Active is not null) throw new InvalidOperationException("Termina e repõe a sessão anterior antes de ativar o automático.");
        var apps = library.Read(); if (apps.Count == 0) throw new InvalidOperationException("Adiciona os executáveis dos jogos à biblioteca primeiro.");
        if (LibraryProfiles.SelectedItem is not GameProfile profile) return;
        var plan = SessionPowerChoice.IsChecked == true ? SessionPlans.SelectedItem as PowerPlan ?? throw new InvalidOperationException("Escolhe um plano disponível.") : null;
        RequireSessionPower(plan);
        var configuration = new AutomationConfiguration(apps, profile.Name, SessionPriorityChoice.IsChecked == true, SessionAnimationsChoice.IsChecked == true, plan?.Id.ToString());
        if (!await Confirm("Ativar Game Mode automático?", $"Perfil: {profile.Name}\nPrioridade acima do normal: {configuration.Priority}\nReduzir animações: {configuration.Animations}\nPlano: {plan?.Name ?? "mantido"}\n\nAplicações:\n" + string.Join("\n", apps.Select(x => x.Name)) + "\n\nSó enquanto a NEXUS estiver aberta. Esta seleção fica fixa até pausares e ativares novamente. Ao fechar o jogo, repõe os valores anteriores. Cada processo é iniciado uma única vez; falhas pedem recuperação no Histórico.")) return;
        gameAutomation.Enable(configuration);
        DiscoveryChoice.IsChecked = true;
        AutomationStatus.Text = "Ativo · " + profile.Name + $" · {apps.Count} aplicações autorizadas. A aguardar jogo.";
    });
    private void RefreshTuner(object sender, RoutedEventArgs e)
    {
        try { TunerProcessChoice.ItemsSource = WindowsSettings.Games(); TunerStatus.Text = "Escolhe um programa com prioridade normal."; }
        catch (Exception error) { Notice(error.Message, true); }
    }
    private void AddRunningLibraryApp(object sender, RoutedEventArgs e)
    {
        try
        {
            if (GameList.SelectedItem is not GameProcess game) throw new InvalidOperationException("Abre o jogo, atualiza e seleciona a aplicação acima.");
            using var process = Process.GetProcessById(game.Id);
            if (process.StartTime.ToUniversalTime().Ticks != game.Started) throw new InvalidOperationException("A aplicação terminou ou mudou. Atualiza a lista.");
            var executable = process.MainModule?.FileName ?? throw new InvalidOperationException("O caminho deste processo não está acessível.");
            var app = library.Add(executable); RefreshLibrary();
            Notice("Adicionado à biblioteca: " + app.Name + ". O automático só usa a nova aplicação após pausares e ativares novamente.");
        }
        catch (Exception error) { Notice(error.Message, true); }
    }
    private async void ApplyTuner(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (sessions.Active is not null) throw new InvalidOperationException("Termina a sessão antes de usar o App Tuner.");
        if (TunerProcessChoice.SelectedItem is not GameProcess selected) { Notice("Deteta e seleciona um programa aberto.", true); return; }
        if (!await Confirm("Otimizar " + selected.Name + "?", "Aumenta a prioridade de CPU para acima do normal até o programa fechar. Pode afetar outras aplicações. O valor anterior fica no histórico; não modifica ficheiros do programa.")) return;
        optimizer.Apply(selected.Key, "AboveNormal"); TunerStatus.Text = selected.Name + " · prioridade aplicada. Repor ajustes de programas restaura o valor anterior.";
    });
    private async void RestoreTuner(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (!await Confirm("Repor prioridades de programas?", "Repõe alterações do App Tuner e prioridades manuais guardadas pela NEXUS. Não termina programas nem interfere com uma sessão de jogo ativa.")) return;
        var result = optimizer.RestoreAll(x => x.Owner is null && x.Key.StartsWith("priority:", StringComparison.Ordinal));
        TunerStatus.Text = $"{result.Restored} prioridades repostas." + (result.Complete ? "" : "\n" + string.Join("\n", result.Errors));
    });
    private async void ExecuteAutoCleanup(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (!await Confirm("Executar a limpeza agora?", "Analisa apenas os temporários do utilizador com 30 dias, limitados a 100 ficheiros e 256 MiB. Envia à Lixeira. Suspende se um jogo da biblioteca ou uma sessão estiver ativo.")) return;
        var wasOff = nextAutoCleanup == DateTimeOffset.MaxValue;
        nextAutoCleanup = DateTimeOffset.Now;
        await PollAutoCleanupAsync(force: true);
        if (wasOff) nextAutoCleanup = DateTimeOffset.MaxValue;
    });
}
