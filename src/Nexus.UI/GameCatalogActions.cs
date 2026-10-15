using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Gaming;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private GameDiscoveryStore gameCatalog = null!;
    private bool installedScanBusy;
    private DateTimeOffset nextInstalledScan = DateTimeOffset.MinValue, nextProcessScan = DateTimeOffset.MinValue;
    private string? catalogSessionState;
    private void RefreshCatalogSessionState()
    {
        var state = sessions.Active is { } s ? s.Id + ":" + s.Status : "";
        if (state == catalogSessionState) return;
        catalogSessionState = state;
        if (InstalledGamesPanel.Visibility == Visibility.Visible) RenderInstalledGames();
    }
    private void InitializeGameCatalog(string root)
    {
        gameCatalog = new(Path.Combine(root, "nexus.db"));
        CustomProfileTemplate.ItemsSource = GameSessionOptions.BuiltIn;
        CustomProfileTemplate.SelectedIndex = 0;
        RenderInstalledGames();
    }
    private async void ScanInstalledGames(object sender, RoutedEventArgs e) => await ScanInstalledGamesAsync();
    private void SeedGameCatalogSmokeFixtures(string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new InvalidOperationException("Artificial game installations are only for isolated hosted UI validation.");
        var root = Path.Combine(output, "fixture-game-library");
        var steam = Path.Combine(root, "steam");
        var game = Path.Combine(steam, "steamapps", "common", "Counter-Strike Global Offensive", "game", "bin", "win64");
        Directory.CreateDirectory(game); File.WriteAllBytes(Path.Combine(game, "cs2.exe"), []);
        File.WriteAllText(Path.Combine(steam, "steamapps", "appmanifest_730.acf"),
            "\"AppState\" { \"appid\" \"730\" \"name\" \"Counter-Strike 2 — TESTE DA INTERFACE\" \"installdir\" \"Counter-Strike Global Offensive\" }");
        var local = Path.Combine(root, "local"); Directory.CreateDirectory(Path.Combine(local, "FiveM"));
        File.WriteAllBytes(Path.Combine(local, "FiveM", "FiveM.exe"), []);
        var result = GameDiscovery.Scan(new([steam], [], [], [], [], [], local, Path.Combine(root, "roaming")));
        if (result.Games.Count != 2 || result.Games.Any(g => !g.CanTrack)) throw new InvalidOperationException("Game discovery fixture failed.");
        gameCatalog.Synchronize(result); RefreshLibrary(); RenderInstalledGames();
        InstalledGamesStatus.Text = "TESTE DA INTERFACE — duas instalações artificiais. Não são jogos reais deste PC e nenhum jogo foi iniciado.";
    }
    private async Task ScanInstalledGamesAsync()
    {
        if (installedScanBusy || closed || sessions.Active is not null || operationRunning || autoCleanupBusy) return;
        installedScanBusy = true; InstalledScanButton.IsEnabled = false;
        InstalledGamesStatus.Text = "A ler bibliotecas e manifestos dos launchers. Não é feita uma pesquisa de todo o disco…";
        try
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(backgroundCancellation.Token);
            cancel.CancelAfter(TimeSpan.FromSeconds(20));
            var result = await Task.Run(() =>
            {
                var warnings = new List<string>();
                var locations = GameDiscoverySources.Windows(warnings);
                var scan = GameDiscovery.Scan(locations, cancel.Token);
                return scan with { Warnings = scan.Warnings.Concat(warnings).ToArray() };
            }, cancel.Token);
            if (closed) return;
            gameCatalog.Synchronize(result); RefreshLibrary(); RenderInstalledGames();
            InstalledGamesStatus.Text = $"{gameCatalog.Read().Count} instalações encontradas · última procura {result.At:HH:mm}.\n" +
                (result.Warnings.Count == 0 ? "Foram consultadas as fontes acessíveis. Algumas instalações personalizadas ou protegidas podem não ser identificadas." :
                "Deteção parcial:\n" + string.Join("\n", result.Warnings.Distinct().Take(6)));
        }
        catch (OperationCanceledException) { if (!closed) InstalledGamesStatus.Text = "Procura cancelada ou demasiado demorada. A biblioteca anterior foi preservada."; }
        catch (Exception ex) { Nexus.Diagnostics.AppLog.Error(ex, "Descobrir jogos instalados"); if (!closed) InstalledGamesStatus.Text = "Não foi possível atualizar todos os jogos. A biblioteca anterior foi preservada."; }
        finally
        {
            nextInstalledScan = DateTimeOffset.Now.AddMinutes(5); installedScanBusy = false;
            if (!closed) { InstalledScanButton.IsEnabled = true; if (assistantInitialized) ApplyAppearance(); }
        }
    }
    private async Task PollInstalledGamesAsync()
    {
        if (InstalledGamesPanel.Visibility == Visibility.Visible && DateTimeOffset.Now >= nextInstalledScan &&
            benchmarkCancellation is null && temporaryCancellation is null) await ScanInstalledGamesAsync();
    }
    private void RenderInstalledGames()
    {
        InstalledGameCards.Children.Clear();
        var games = gameCatalog.Read(); var profiles = gameCatalog.Profiles();
        var activeSession = sessions.Active;
        var activePath = activeSession is null ? null : GameLibrary.Detect(library.Read()).FirstOrDefault(a => a.ProcessKey == activeSession.ProcessKey)?.App.Path;
        if (games.Count == 0)
        {
            InstalledGameCards.Children.Add(new TextBlock { Text = "Ainda não há jogos identificados. A procura automática usa as instalações deste PC; a torre e o portátil têm bibliotecas independentes.", TextWrapping = TextWrapping.Wrap });
            return;
        }
        foreach (var game in games)
        {
            var preference = gameCatalog.Preference(game);
            var content = new StackPanel { Spacing = 10 };
            var title = new Grid { ColumnSpacing = 12 };
            title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            title.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            title.Children.Add(new FontIcon { Glyph = "\uE7FC", FontSize = 24 });
            var name = new TextBlock { Text = game.Name, FontSize = 21, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(name, 1); title.Children.Add(name); content.Children.Add(title);
            content.Children.Add(new TextBlock { Text = game.Launcher + " · " + game.InstallationState + "\nRecomendado: " + game.RecommendedProfile, TextWrapping = TextWrapping.Wrap });
            var profile = new ComboBox { ItemsSource = profiles, SelectedItem = profiles.FirstOrDefault(p => p.Name == preference.ProfileName) ?? profiles[0], HorizontalAlignment = HorizontalAlignment.Stretch };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(profile, "Perfil de " + game.Name);
            var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Color("#C3CBE0") };
            var automatic = new CheckBox { Content = "Usar este perfil no Game Boost automático", IsChecked = preference.Automatic, IsEnabled = game.CanTrack };
            void Save()
            {
                if (profile.SelectedItem is not GameSessionOptions selected) return;
                summary.Text = selected.Summary;
                try { gameCatalog.SavePreference(new(game.Id, selected.Name, automatic.IsChecked == true)); }
                catch (Exception ex) { Notice("Não foi possível guardar a escolha: " + ex.Message, true); }
                if (gameAutomation.Enabled) AutomationStatus.Text = "Automático ativo com a seleção anterior. Pausa e ativa novamente para usar as escolhas alteradas.";
            }
            profile.SelectionChanged += (_, _) => Save(); automatic.Checked += (_, _) => Save(); automatic.Unchecked += (_, _) => Save();
            summary.Text = ((GameSessionOptions)profile.SelectedItem).Summary;
            content.Children.Add(profile); content.Children.Add(summary); content.Children.Add(automatic);
            var state = new TextBlock { Text = "Não aplicado. Abre o jogo para iniciar uma sessão com revisão.", TextWrapping = TextWrapping.Wrap };
            if (activeSession is not null && activePath is not null && game.Executables.Contains(activePath, StringComparer.OrdinalIgnoreCase))
                state.Text = activeSession.Status == "active" ? "Aplicado nesta sessão; os valores anteriores estão guardados." : "Reposição pendente. Abre Game Boost.";
            else if (activeSession is { Status: not "active" } recovery && recovery.Name == game.Name)
                state.Text = "Uma sessão deste jogo precisa de recuperação. Abre Game Boost ou Histórico.";
            var apply = new Button { Content = "Otimizar este jogo", IsEnabled = game.CanTrack };
            apply.Click += async (_, _) => await Run(async () =>
            {
                var candidates = library.Read().Where(a => game.Executables.Contains(a.Path, StringComparer.OrdinalIgnoreCase)).ToArray();
                var running = await Task.Run(() => GameLibrary.Detect(candidates));
                var target = running.OrderByDescending(a => a.ActualPath?.Contains("_GTAProcess", StringComparison.OrdinalIgnoreCase) == true).FirstOrDefault();
                if (target is null) { Notice("Abre " + game.Name + " pelo launcher habitual e volta a carregar em Otimizar. A NEXUS não modifica os ficheiros nem inicia o jogo."); return; }
                var selected = ((GameSessionOptions)profile.SelectedItem).Freeze();
                if (!await Confirm("Preparar " + game.Name + "?", selected.Name + "\n" + selected.Summary + "\n\nAs preferências são temporárias. Mantém a NEXUS aberta até terminar o jogo.")) return;
                var session = sessions.Start(target.ProcessKey, game.Name, priority: false, visualKeys: selected.VisualKeys);
                state.Text = session.Status == "active" ? "Aplicado nesta sessão. Usa Repor sessão quando quiseres." : "Reposição necessária: " + session.Error;
                RenderInstalledGames();
            });
            content.Children.Add(state); content.Children.Add(apply);
            var restore = new Button { Content = "Repor sessão", IsEnabled = sessions.Active is not null && sessions.Active.Name == game.Name };
            restore.Click += (_, _) => EndSession(this, new());
            content.Children.Add(restore);
            content.Children.Add(new TextBlock { Text = game.Directory, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Color("#95A1BF") });
            InstalledGameCards.Children.Add(new Border { Child = content, Padding = new Thickness(20), CornerRadius = new CornerRadius(14), Background = Color("#171D2F"), BorderBrush = Color("#2A3350"), BorderThickness = new Thickness(1) });
        }
    }
    private async void TogglePerGameAutomation(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (gameAutomation.Enabled) { gameAutomation.Disable(); AutomationStatus.Text = "Automático pausado. A sessão ativa continua até terminar ou ser reposta."; return; }
        var profiles = gameCatalog.Profiles(); var apps = new List<LibraryApp>();
        var perApp = new Dictionary<string, GameSessionOptions>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in gameCatalog.Read())
        {
            var preference = gameCatalog.Preference(game);
            if (!preference.Automatic || !game.CanTrack) continue;
            var profile = profiles.FirstOrDefault(p => p.Name == preference.ProfileName);
            if (profile is null) continue;
            foreach (var app in library.Read().Where(a => game.Executables.Contains(a.Path, StringComparer.OrdinalIgnoreCase)))
            { apps.Add(app); perApp[app.Path] = profile.Freeze(); }
        }
        if (apps.Count == 0) { Notice("Escolhe pelo menos um jogo com automático ativado e executável identificado.", true); return; }
        if (!await Confirm("Ativar Game Boost por jogo?", string.Join("\n\n", apps.DistinctBy(a => a.Name).Select(a => a.Name + " · " + perApp[a.Path].Name + "\n" + perApp[a.Path].Summary)) +
            "\n\nSó enquanto a NEXUS está aberta. Novos jogos não recebem autorização automaticamente. Não fecha aplicações, não altera energia ou prioridade e nunca liga a VPN. Ao terminar o jogo, repõe os valores anteriores.")) return;
        gameAutomation.Enable(new(apps, "Por jogo", false, false, null, perApp));
        AutomationStatus.Text = "Game Boost por jogo ativo nesta abertura. Para novas escolhas, pausa e volta a ativar.";
        Notice("Game Boost por jogo ativado. Abre os jogos normalmente.");
    });
    private void SaveCustomGameProfile(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CustomProfileTemplate.SelectedItem is not GameSessionOptions template) return;
            gameCatalog.SaveProfile(new(CustomProfileName.Text.Trim(), template.VisualKeys));
            CustomProfileName.Text = ""; RenderInstalledGames(); Notice("Perfil personalizado guardado. Escolhe-o no cartão de cada jogo.");
        }
        catch (Exception ex) { Notice(ex.Message, true); }
    }
    private async void ExportGameProfile(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var profiles = gameCatalog.Profiles();
        var selected = profiles.LastOrDefault(p => !GameSessionOptions.BuiltIn.Any(b => b.Name == p.Name)) ??
            CustomProfileTemplate.SelectedItem as GameSessionOptions ?? profiles[0];
        var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(AppWindow.Id);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null || closed) return;
        var path = Path.Combine(folder.Path, "NEXUS-perfil-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        await File.WriteAllTextAsync(path, GameDiscoveryStore.ExportProfile(selected));
        Notice("Perfil exportado sem caminhos dos jogos ou credenciais: " + path);
    });
    private async void ImportGameProfile(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id); picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync(); if (file is null || closed) return;
        if (new FileInfo(file.Path).Length > 16384) throw new ArgumentException("O ficheiro de perfil excede o limite.");
        var profile = GameDiscoveryStore.ImportProfile(await File.ReadAllTextAsync(file.Path));
        if (GameSessionOptions.BuiltIn.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
            profile = profile with { Name = "Importado — " + profile.Name };
        if (!await Confirm("Importar perfil?", profile.Name + "\n" + profile.Summary + "\nSó guarda o perfil. Não aplica alterações nem ativa o automático.")) return;
        gameCatalog.SaveProfile(profile); RenderInstalledGames(); Notice("Perfil importado. Nenhum ajuste foi aplicado.");
    });
}
