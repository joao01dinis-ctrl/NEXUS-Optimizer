using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Nexus.Optimization;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private readonly Grid dashboardMetrics = new() { ColumnSpacing = 10, RowSpacing = 10 };
    private readonly Grid dashboardModules = new() { ColumnSpacing = 12, RowSpacing = 12 };
    private readonly StackPanel dashboardGames = new() { Spacing = 8 };
    private readonly TextBlock dashboardGameCount = DashboardText("A procurar instalações locais…", 12, muted: true);
    private readonly TextBlock dashboardJournal = DashboardText("A consultar o histórico…", 12, muted: true);
    private readonly TextBlock dashboardSample = DashboardText("A recolher dados", 11, muted: true);
    private readonly List<(ToggleSwitch Toggle, string Key, string Desired, TextBlock State)> dashboardSettings = [];
    private bool dashboardReady, dashboardRefreshing;

    private static TextBlock DashboardText(string text, double size = 13, bool bold = false, bool muted = false) => new()
    {
        Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        Foreground = Color(muted ? "#9DA9AE" : "#F0F5F2")
    };
    private static TextBlock DashboardBound(string property, double size, bool bold = false)
    {
        var value = DashboardText("", size, bold);
        value.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(property), Mode = BindingMode.OneWay });
        return value;
    }
    private Button DashboardLink(string label, string destination, bool accent = false)
    {
        var button = new Button { Content = label, Padding = new Thickness(11, 7, 11, 7), FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(6) };
        if (accent) button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        button.Click += (_, _) => ShowPage(destination);
        return button;
    }
    private static StackPanel DashboardSection(string title, string description, string glyph)
    {
        var content = new StackPanel { Spacing = 10 };
        var heading = new Grid { ColumnSpacing = 10 };
        heading.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        heading.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        if (glyph == "\uE782")
        {
            var logo = new Grid { Width = 20, Height = 20, RowSpacing = 2, ColumnSpacing = 2 };
            for (var i = 0; i < 2; i++) { logo.RowDefinitions.Add(new()); logo.ColumnDefinitions.Add(new()); }
            for (var i = 0; i < 4; i++)
            {
                var pane = new Microsoft.UI.Xaml.Shapes.Rectangle { Fill = Color("#43DF79") };
                Grid.SetColumn(pane, i % 2); Grid.SetRow(pane, i / 2); logo.Children.Add(pane);
            }
            heading.Children.Add(logo);
        }
        else heading.Children.Add(new FontIcon { Glyph = glyph, FontSize = 21, Foreground = Color("#43DF79") });
        var labels = new StackPanel { Spacing = 3 };
        labels.Children.Add(DashboardText(title, 15, true));
        labels.Children.Add(DashboardText(description, 11, muted: true));
        Grid.SetColumn(labels, 1); heading.Children.Add(labels); content.Children.Add(heading);
        return content;
    }
    private static Border DashboardCard(UIElement content) => new()
    {
        Background = Color("#121819"), BorderBrush = Color("#283234"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Child = content
    };
    private void DashboardMove(FrameworkElement element, Panel destination)
    {
        // XAML-created children may not expose Parent until their tree is loaded.
        // Detach from the known owner before reusing the existing named control.
        DashboardLegacy.Children.Remove(element);
        if (element.Parent is Panel parent) parent.Children.Remove(element);
        destination.Children.Add(element);
    }
    private void InitializeDashboard()
    {
        var body = new StackPanel { Spacing = 12 };
        var top = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        top.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        top.RowDefinitions.Add(new() { Height = GridLength.Auto }); top.RowDefinitions.Add(new() { Height = GridLength.Auto });
        top.Children.Add(DashboardSection("Visão geral do sistema", "Medições deste computador · alterações só depois de revisão", "\uE7F4"));
        var actions = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, MaximumRowsOrColumns = 2 };
        HomeOptimizeButton.Padding = new Thickness(18, 10, 18, 10); HomeOptimizeButton.Margin = new Thickness(0, 0, 8, 6);
        HomeAnalyzeButton.Padding = new Thickness(18, 10, 18, 10); HomeAnalyzeButton.Margin = new Thickness(0, 0, 0, 6);
        HomeOptimizeButton.Style = null;
        HomeOptimizeButton.Background = Color("#43DF79"); HomeOptimizeButton.BorderBrush = Color("#43DF79"); HomeOptimizeButton.Foreground = Color("#0D1213");
        ToolTipService.SetToolTip(HomeOptimizeButton, "Analisa o PC e abre a seleção para revisão. Não aplica ajustes ao clicar.");
        DashboardMove(HomeOptimizeButton, actions); DashboardMove(HomeAnalyzeButton, actions);
        Grid.SetColumn(actions, 1); top.Children.Add(actions);
        Grid.SetRow(dashboardSample, 1); Grid.SetColumnSpan(dashboardSample, 2); top.Children.Add(dashboardSample);
        var hero = DashboardCard(top);
        hero.RequestedTheme = ElementTheme.Dark;
        hero.Background = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops = { new GradientStop { Color = Windows.UI.Color.FromArgb(255, 22, 35, 28), Offset = 0 },
                new GradientStop { Color = Windows.UI.Color.FromArgb(255, 15, 21, 22), Offset = 1 } }
        };
        body.Children.Add(hero);

        void Metric(string title, FrameworkElement reading, string detail, string? property = null, Canvas? chart = null)
        {
            var panel = new StackPanel { Spacing = 7 };
            panel.Children.Add(DashboardText(title, 12, true)); panel.Children.Add(reading);
            if (chart is not null) { chart.Height = 36; DashboardMove(chart, panel); }
            var explanation = property is null ? DashboardText(detail, 11, muted: true) : DashboardBound(property, 11);
            explanation.MaxLines = 2; explanation.TextTrimming = TextTrimming.CharacterEllipsis;
            ToolTipService.SetToolTip(explanation, property is null ? detail : "Identificação detetada neste PC; detalhes em Hardware.");
            panel.Children.Add(explanation); dashboardMetrics.Children.Add(DashboardCard(panel));
        }
        Metric("CPU", DashboardBound(nameof(model.CpuText), 27, true), "", nameof(model.CpuName), CpuGraph);
        Metric("RAM", DashboardBound(nameof(model.MemoryText), 21, true), "", nameof(model.RamText), MemoryGraph);
        Metric("GPU", DashboardText("Deteção local", 19, true), "", nameof(model.GpuName));
        Metric("Latência", DashboardText("Por medir", 19, true), "O teste é iniciado por ti em Rede. Não é o ping de todos os jogos.");
        Metric("Temperatura", DashboardText("Sem leitura", 19, true), "Este painel não tem uma fonte térmica validada. Nenhum valor estimado.");
        Metric("FPS / antes e depois", DashboardText("Por medir", 19, true), "Importa capturas em Testes e FPS. Não existe uma previsão de ganho.");
        dashboardMetrics.SizeChanged += (_, _) => DashboardLayout(dashboardMetrics, dashboardMetrics.ActualWidth >= 1040 ? 6 : dashboardMetrics.ActualWidth >= 600 ? 3 : 2);
        body.Children.Add(dashboardMetrics);

        var games = DashboardSection("Jogos detetados", "Biblioteca deste PC · perfis guardados por jogo", "\uE7FC");
        games.Children.Add(dashboardGameCount); games.Children.Add(dashboardGames);
        var gameActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var scan = new Button { Content = "Atualizar", FontSize = 12 };
        scan.Click += async (_, _) => { scan.IsEnabled = false; try { await ScanInstalledGamesAsync(); RefreshDashboardGames(); } finally { if (!closed) scan.IsEnabled = true; } };
        gameActions.Children.Add(scan); gameActions.Children.Add(DashboardLink("Gerir jogos e perfis", "library"));
        games.Children.Add(gameActions); dashboardModules.Children.Add(DashboardCard(games));

        var windows = DashboardSection("Otimização do Windows", "Preferências reais, com histórico e reposição", "\uE782");
        DashboardSetting(windows, "Animações dos menus", "menu-animation", "0");
        DashboardSetting(windows, "Animações ao minimizar", "minimize-animation", "0");
        DashboardSetting(windows, "Animações de conteúdo", "animations", "0");
        windows.Children.Add(DashboardLink("Energia, arranque e outros ajustes", "optimize"));
        dashboardModules.Children.Add(DashboardCard(windows));

        var gpu = DashboardSection("GPU e gráficos", "Compatibilidade antes de qualquer alteração", "\uE7F4");
        gpu.Children.Add(DashboardBound(nameof(model.GpuName), 13, true));
        gpu.Children.Add(DashboardText("Reflex, V-Sync, shader cache e opções do driver dependem do jogo e do fabricante. Não são ativados por botões simulados.", 12, muted: true));
        gpu.Children.Add(DashboardLink("Ver GPU, monitor e controladores", "hardware"));
        var graphics = new Button { Content = "Preferências gráficas do Windows", Tag = "ms-settings:display-advancedgraphics", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        graphics.Click += OpenSetting; gpu.Children.Add(graphics);
        dashboardModules.Children.Add(DashboardCard(gpu));

        var resources = DashboardSection("CPU / RAM", "Monitorização e sessões de jogo reversíveis", "\uE950");
        resources.Children.Add(DashboardBound(nameof(model.CpuName), 13));
        resources.Children.Add(DashboardBound(nameof(model.MemoryText), 20, true));
        resources.Children.Add(DashboardText("Revê processos e ajustes da sessão. Nenhuma prioridade, afinidade ou limpeza de memória é aplicada por este painel.", 12, muted: true));
        resources.Children.Add(DashboardLink("Ver consumo das aplicações", "memory"));
        resources.Children.Add(DashboardLink("Preparar Game Boost", "gaming")); dashboardModules.Children.Add(DashboardCard(resources));

        var network = DashboardSection("Rede & VPN", "Testes iniciados por ti · WARP opcional", "\uE774");
        network.Children.Add(DashboardText("Ligação e túnel por verificar", 14, true));
        network.Children.Add(DashboardText("Não há medições externas no arranque. A NEXUS não afirma que o tráfego está protegido sem confirmação. DNS e WARP exigem a tua revisão.", 12, muted: true));
        network.Children.Add(DashboardLink("Medir latência e estabilidade", "latency"));
        network.Children.Add(DashboardLink("Abrir Nexos VPN / WARP", "vpn"));
        network.Children.Add(DashboardLink("Consultar adaptadores e DNS", "network")); dashboardModules.Children.Add(DashboardCard(network));

        var mouse = DashboardSection("Mouse / Input", "Mouse Lab integrado · simples e avançado", "\uE962");
        mouse.Children.Add(DashboardText("Testar. Comparar. Repor.", 17, true));
        mouse.Children.Add(DashboardText("Polling observado, cliques, treino de mira, sensibilidade e perfis CS2 / FiveM. DPI e sensor só são alterados com suporte oficial.", 12, muted: true));
        mouse.Children.Add(DashboardLink("Abrir Mouse Lab", "mouselab", true));
        mouse.Children.Add(DashboardLink("Definições do ponteiro", "precision")); dashboardModules.Children.Add(DashboardCard(mouse));

        var fiveM = DashboardSection("FiveM & Mods", "Diagnóstico e perfis · sem alterar o jogo", "\uE7FC");
        fiveM.Children.Add(DashboardText("A NEXUS não instala citizens, packs ou mods nem modifica o anti-cheat. Consulta o guia para comparar FPS e pausas no mesmo servidor.", 12, muted: true));
        fiveM.Children.Add(DashboardLink("Abrir guia de testes FiveM", "fivem"));
        fiveM.Children.Add(DashboardLink("Comparar capturas reais de FPS", "benchmark")); dashboardModules.Children.Add(DashboardCard(fiveM));

        var cleanup = DashboardSection("Limpeza e manutenção", "Analisar antes de remover", "\uE74D");
        cleanup.Children.Add(DashboardText("O espaço recuperável só é apresentado depois da análise. Revê os ficheiros e usa a Lixeira quando suportado. Não há limpeza agressiva do registo.", 12, muted: true));
        cleanup.Children.Add(DashboardLink("Analisar ficheiros temporários", "autoclean"));
        cleanup.Children.Add(DashboardLink("Proteção com Microsoft Defender", "protection")); dashboardModules.Children.Add(DashboardCard(cleanup));

        var restore = DashboardSection("Restore", "Os teus dados e valores anteriores preservados", "\uE81C");
        restore.Children.Add(dashboardJournal);
        restore.Children.Add(DashboardLink("Ver alterações e repor", "history"));
        var backup = new Button { Content = "Exportar cópia dos dados", HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
        backup.Click += ExportBackup; restore.Children.Add(backup);
        dashboardModules.Children.Add(DashboardCard(restore));
        dashboardModules.SizeChanged += (_, _) => DashboardLayout(dashboardModules, dashboardModules.ActualWidth >= 1020 ? 3 : dashboardModules.ActualWidth >= 650 ? 2 : 1);
        body.Children.Add(dashboardModules);
        DashboardHost.Content = body; dashboardReady = true;
        RefreshDashboardGames(); RefreshDashboardState();
    }
    private static void DashboardLayout(Grid grid, int columns)
    {
        if (grid.Tag is int previous && previous == columns) return;
        grid.Tag = columns; grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < (grid.Children.Count + columns - 1) / columns; i++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (var i = 0; i < grid.Children.Count; i++)
            if (grid.Children[i] is FrameworkElement child) { Grid.SetRow(child, i / columns); Grid.SetColumn(child, i % columns); }
    }
    private void DashboardSetting(StackPanel content, string label, string key, string desired)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(DashboardText(label, 12, true));
        var state = DashboardText("A consultar…", 10, muted: true); labels.Children.Add(state); row.Children.Add(labels);
        var toggle = new ToggleSwitch { OnContent = "", OffContent = "", MinWidth = 44, HorizontalAlignment = HorizontalAlignment.Right };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, "Reduzir " + label.ToLowerInvariant());
        ToolTipService.SetToolTip(toggle, "Revê a alteração. O valor anterior é guardado para restauro.");
        Grid.SetColumn(toggle, 1); row.Children.Add(toggle); content.Children.Add(row);
        dashboardSettings.Add((toggle, key, desired, state));
        toggle.Toggled += async (_, _) =>
        {
            if (dashboardRefreshing || !dashboardReady) return;
            var enabled = toggle.IsOn; var observed = toggle.Tag as AdjustmentState;
            if (observed is null) return;
            await Run(async () =>
            {
                if (sessions.Active is not null) throw new InvalidOperationException("Termina e repõe a sessão de jogo antes de alterar preferências permanentes.");
                if (!await Confirm(enabled ? "Reduzir " + label.ToLowerInvariant() + "?" : "Repor " + label.ToLowerInvariant() + "?",
                    "Preferência visual do Windows, sem garantia de ganhos em jogos. O valor anterior fica no Histórico. Se outra aplicação tiver mudado esta opção, o restauro pode exigir revisão.")) return;
                AdjustmentStates.Set(optimizer, observed, enabled);
                Notice(enabled ? "Preferência aplicada e guardada no histórico." : "Valor anterior reposto.");
            });
            RefreshDashboardState();
        };
    }
    private void RefreshDashboardGames()
    {
        if (!dashboardReady) return;
        dashboardGames.Children.Clear();
        try
        {
            var games = gameCatalog.Read(); dashboardGameCount.Text = $"{games.Count} instalações encontradas neste PC";
            foreach (var game in games.Take(3))
            {
                var panel = new StackPanel { Spacing = 3 };
                panel.Children.Add(DashboardText(game.Name, 14, true));
                var preference = gameCatalog.Preference(game);
                panel.Children.Add(DashboardText(game.Launcher + " · " + preference.ProfileName, 11, muted: true));
                var button = new Button { Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 8, 10, 8) };
                button.Click += (_, _) => ShowPage("library"); dashboardGames.Children.Add(button);
            }
            if (games.Count == 0) dashboardGames.Children.Add(DashboardText("Ainda sem jogos identificados. A biblioteca e os perfis do outro PC serão detetados separadamente.", 12, muted: true));
        }
        catch (Exception ex) { dashboardGameCount.Text = "Biblioteca indisponível. Abre Jogos para tentar novamente."; Nexus.Diagnostics.AppLog.Error(ex, "Dashboard: jogos"); }
    }
    private void RefreshDashboardState()
    {
        if (!dashboardReady || closed) return;
        dashboardRefreshing = true;
        try
        {
            foreach (var (toggle, key, desired, state) in dashboardSettings)
            {
                try
                {
                    var observed = AdjustmentStates.Read(optimizer, key, desired);
                    toggle.Tag = observed; toggle.IsOn = observed.IsOn; toggle.IsEnabled = observed.Error is null && (!observed.IsOn || observed.CanRestore);
                    state.Text = observed.Label + (observed.IsOn && !observed.CanRestore ? " · sem valor anterior NEXUS" : "");
                }
                catch { toggle.Tag = null; toggle.IsEnabled = false; state.Text = "Estado indisponível"; }
            }
            var changes = optimizer.History().Count(x => x.State is "applied" or "pending");
            dashboardJournal.Text = changes == 0 ? "Sem alterações do sistema por repor. Perfis, histórico e treinos continuam guardados localmente." : $"{changes} alterações aplicadas ou por verificar. Consulta o Histórico antes de repor.";
            dashboardSample.Text = model.LastSampleAt is { } at ? $"●  Dados locais · atualizados às {at.ToLocalTime():HH:mm:ss} · {model.SampleSeconds}s entre amostras" : "A recolher a primeira amostra…";
        }
        catch (Exception ex) { dashboardJournal.Text = "Histórico por consultar"; Nexus.Diagnostics.AppLog.Error(ex, "Dashboard: estado"); }
        finally { dashboardRefreshing = false; }
    }
    private void ValidateDashboard(string output)
    {
        DashboardLayout(dashboardMetrics, dashboardMetrics.ActualWidth >= 1040 ? 6 : dashboardMetrics.ActualWidth >= 600 ? 3 : 2);
        DashboardLayout(dashboardModules, dashboardModules.ActualWidth >= 1020 ? 3 : dashboardModules.ActualWidth >= 650 ? 2 : 1);
        var cards = dashboardMetrics.Children.Concat(dashboardModules.Children).OfType<FrameworkElement>().ToArray();
        if (!dashboardReady || cards.Length != 15 || cards.Any(x => x.ActualWidth < 100 || !double.IsFinite(x.ActualHeight) || x.ActualHeight < 1))
            throw new InvalidOperationException("Dashboard card layout did not render correctly.");
        if (optimizer.History().Count != 0 || mouseLab?.Capturing == true || gameAutomation.Enabled || vpnControlRequests != 0 || vpnExternalSamples != 0)
            throw new InvalidOperationException("Read-only dashboard validation changed settings or started optional monitoring.");
        File.WriteAllText(Path.Combine(output, "dashboard-readonly.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Result = "PASS", Version = "1.0.1", Cards = cards.Length, MetricColumns = dashboardMetrics.ColumnDefinitions.Count,
            ModuleColumns = dashboardModules.ColumnDefinitions.Count, DashboardWidth = dashboardModules.ActualWidth,
            WindowsChanges = 0, VpnRequests = 0, VpnExternalSamples = 0, MouseCapture = false, AutomaticGameProfiles = false,
            UnmeasuredFps = true, UnmeasuredTemperatures = true
        }));
    }
}
