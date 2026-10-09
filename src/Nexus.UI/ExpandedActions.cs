using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Gaming;
using Nexus.Hardware;
using Nexus.Network;
using Nexus.Optimization;
using Nexus.Storage;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private GameLibrary library = null!;
    private ActivityStore activityStore = null!;
    private CancellationTokenSource? latencyCancellation;
    private DateTimeOffset nextAutoCleanup = DateTimeOffset.MaxValue;
    private bool autoCleanupBusy, discoveryBusy;
    private readonly CancellationTokenSource backgroundCancellation = new();
    private IReadOnlyList<Nexus.Core.FeatureItem> featureCatalog = [];
    private readonly Queue<(double? Cpu, double Ram)> chartSamples = new();
    private DateTimeOffset? chartAt;
    private void UpdateGraphs()
    {
        if (model.LastSampleAt is not null && chartAt != model.LastSampleAt)
        {
            chartAt = model.LastSampleAt; chartSamples.Enqueue((model.LastCpuSample, model.MemoryPercent));
            while (chartSamples.Count > 60) chartSamples.Dequeue();
        }
        var cpu = new Microsoft.UI.Xaml.Media.PointCollection(); var ram = new Microsoft.UI.Xaml.Media.PointCollection(); var i = 0;
        foreach (var point in chartSamples)
        {
            var x = i++ / 59d;
            if (point.Cpu is double value) cpu.Add(new Windows.Foundation.Point(x * CpuGraph.ActualWidth, 60 - Math.Clamp(value, 0, 100) * .6));
            ram.Add(new Windows.Foundation.Point(x * MemoryGraph.ActualWidth, 60 - Math.Clamp(point.Ram, 0, 100) * .6));
        }
        CpuLine.Points = cpu; MemoryLine.Points = ram;
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        UptimeText.Text = $"Desde o arranque: {uptime.Days} dias, {uptime.Hours} h, {uptime.Minutes} min · gráficos: até 60 amostras locais, sem previsão de desempenho.";
    }
    private string? lastDetectedKey;
    private void InitializeExpanded(string root)
    {
        library = new(Path.Combine(root, "nexus.db"));
        activityStore = new(Path.Combine(root, "nexus.db"));
        LibraryProfiles.ItemsSource = GameLibrary.Profiles; LibraryProfiles.SelectedIndex = 0;
        CleanupLocationChoice.ItemsSource = CleanupLocations.Read(); CleanupLocationChoice.SelectedIndex = 0;
        DnsAdapterChoice.ItemsSource = DnsSettings.Adapters();
        DnsProviderChoice.ItemsSource = DnsBenchmark.Resolvers.Select(x => new DnsProvider(x.Name, x.Address)).ToArray(); DnsProviderChoice.SelectedIndex = 0;
        foreach (var policy in UserPolicies.All)
        {
            var reason = UserPolicies.Unsupported(policy);
            var row = new StackPanel { Spacing = 5 };
            row.Children.Add(new CheckBox { Content = policy.Name, Tag = policy, IsEnabled = reason is null });
            row.Children.Add(new TextBlock { Text = policy.Description + (reason is null ? "" : "\n" + reason), TextWrapping = TextWrapping.Wrap, Opacity = .75 });
            PolicyChoices.Children.Add(row);
        }
        Closed += (_, _) => { latencyCancellation?.Cancel(); backgroundCancellation.Cancel(); };
        RefreshLibrary(); LoadFeatureCatalog();
    }
    private void SaveActivity(string area, string message)
    {
        try { activityStore.Save(area, message); }
        catch (Exception ex) { Nexus.Diagnostics.AppLog.Error(ex, "Guardar evento"); if (!closed) Notice("A ação foi executada, mas o evento não foi guardado no histórico. " + ex.Message, true); }
    }
    private void LoadFeatureCatalog() { featureCatalog = Nexus.Core.FeatureCatalog.Read(); FeatureList.ItemsSource = featureCatalog; }
    private void FilterFeatureCatalog(object sender, TextChangedEventArgs e)
    { FeatureList.ItemsSource = featureCatalog.Where(x => x.Display.Contains(FeatureSearch.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray(); }
    private void OpenFeatureArea(object sender, RoutedEventArgs e)
    {
        if (FeatureList.SelectedItem is not Nexus.Core.FeatureItem item) return;
        if (item.Page is null) { Notice(item.State + ": " + item.Note, true); return; }
        ShowPage(item.Page);
    }
    private async void CheckDrift(object sender, RoutedEventArgs e)
    {
        if (operationRunning) return;
        try
        {
            var drift = await Task.Run(optimizer.FindDrift);
            if (!closed) { DriftList.ItemsSource = drift; DriftSummary.Text = drift.Count == 0 ? "Os últimos valores elegíveis continuam iguais aos aplicados. Esta consulta não confirma ganhos nem políticas efetivas." : $"{drift.Count} valores diferentes ou indisponíveis. Nada foi reaplicado."; }
        }
        catch (Exception ex) { if (!closed) Notice(ex.Message, true); }
    }
    private async void ReapplyDrift(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (DriftList.SelectedItem is not SettingDrift { Error: null } drift) { Notice("Escolhe um ajuste que tenha valor atual acessível.", true); return; }
        if (sessions.Active is not null) { Notice("Termina e repõe a sessão antes de reaplicar.", true); return; }
        if (!await Confirm("Reaplicar " + drift.Record.Name + "?", "O ajuste mudou fora da NEXUS. Reaplica o valor escolhido anteriormente e guarda o valor atual como nova alteração para undo. A aplicação não assume que a mudança externa era um erro.")) return;
        if (WindowsSettings.Resolve(drift.Record.Key).Read() != drift.Current) throw new InvalidOperationException("O valor mudou novamente. Volta a verificar.");
        optimizer.Apply(drift.Record.Key, drift.Record.After); Notice("Ajuste reaplicado e novo valor anterior guardado."); DriftList.ItemsSource = null; DriftSummary.Text = "Volta a verificar para atualizar os valores.";
    });
    private sealed record DnsProvider(string Name, string Address) { public override string ToString() => Name + " · " + Address; }
    private void RefreshLibrary()
    {
        LibraryApps.ItemsSource = library.Read();
    }
    private async void AddLibraryApp(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id);
        picker.FileTypeFilter.Add(".exe");
        var chosen = await picker.PickSingleFileAsync();
        if (chosen is null || closed) return;
        var app = library.Add(chosen.Path); RefreshLibrary(); Notice("Adicionado: " + app.Name + ". Não foi iniciado nem alterado.");
    });
    private void RemoveLibraryApp(object sender, RoutedEventArgs e)
    {
        if (LibraryApps.SelectedItem is not LibraryApp app) return;
        library.Remove(app.Path); RefreshLibrary(); Notice("Removido da biblioteca. O programa continua instalado.");
    }
    private async void DetectLibraryApps(object sender, RoutedEventArgs e)
    {
        if (discoveryBusy) return; discoveryBusy = true;
        try
        {
            var apps = library.Read(); var found = await Task.Run(() => GameLibrary.Detect(apps));
            if (!closed) { LibraryDetected.ItemsSource = found; LibraryDetectionText.Text = found.Count == 0 ? "Nenhuma aplicação da biblioteca está acessível nesta sessão." : $"{found.Count} aplicações identificadas pelo caminho completo."; }
        }
        catch (Exception ex) { if (!closed) Notice("Não foi possível detetar aplicações: " + ex.Message, true); }
        finally { discoveryBusy = false; }
    }
    private async void StartLibrarySession(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (LibraryDetected.SelectedItem is not DetectedApp app || LibraryProfiles.SelectedItem is not GameProfile profile) { Notice("Deteta e seleciona uma aplicação aberta e o perfil.", true); return; }
        var priority = profile.Name == "Personalizado" ? SessionPriorityChoice.IsChecked == true : profile.Priority;
        var animations = profile.Name == "Personalizado" ? SessionAnimationsChoice.IsChecked == true : profile.Animations;
        var plan = SessionPowerChoice.IsChecked == true ? SessionPlans.SelectedItem as PowerPlan ?? throw new InvalidOperationException("Escolhe um plano disponível.") : null;
        var summary = profile.Description + "\n\n" + app.Display + "\nPrioridade: " + (priority ? "acima do normal" : "mantida") + "\nAnimações: " + (animations ? "reduzidas" : "mantidas") + "\nPlano: " + (plan?.Name ?? "mantido") + "\n\nMantém o NEXUS aberto para repor ao terminar. Não há promessa de mais FPS. Extremo mantém os mesmos limites seguros do Competitivo.";
        if (!await Confirm("Iniciar " + profile.Name + "?", summary)) return;
        var result = sessions.Start(app.ProcessKey, app.App.Name, plan?.Id.ToString(), priority, animations);
        Notice(result.Status == "active" ? "Sessão iniciada. Usa Terminar e repor para recuperar os valores anteriores." : result.Error ?? "Reposição necessária.", result.Status != "active");
    });
    // Discovery reports a running library app. It never applies a profile without review.
    private async Task PollDiscoveryAsync()
    {
        if ((DiscoveryChoice.IsChecked != true && !gameAutomation.Enabled) || discoveryBusy || operationRunning || benchmarkCancellation is not null || temporaryCancellation is not null || autoCleanupBusy) return;
        discoveryBusy = true;
        try
        {
            var apps = library.Read(); var found = await Task.Run(() => GameLibrary.Detect(apps));
            if (closed) return;
            LibraryDetected.ItemsSource = found;
            var target = gameAutomation.Next(found, library.Read(), sessions.Active is not null);
            if (target is not null && gameAutomation.Configuration is { } configuration)
            {
                gameAutomation.MarkAttempt(target);
                var result = sessions.Start(target.ProcessKey, target.App.Name, configuration.PowerPlan, configuration.Priority, configuration.Animations);
                AutomationStatus.Text = result.Status == "active" ? "A acompanhar · " + target.App.Name + " · " + configuration.ProfileName : "Recuperação necessária · " + result.Error;
                RefreshSystem(); RefreshAdjustmentCards(this, new());
            }
            var first = found.FirstOrDefault();
            if (first is not null && first.ProcessKey != lastDetectedKey)
            { lastDetectedKey = first.ProcessKey; Notice("Detetado: " + first.App.Name + ". Abre Jogos para rever o perfil e iniciar a sessão."); }
            if (first is null) lastDetectedKey = null;
        }
        catch (Exception ex)
        {
            Nexus.Diagnostics.AppLog.Error(ex, "Deteção de biblioteca");
            if (!closed && gameAutomation.Enabled) AutomationStatus.Text = "Não foi possível iniciar/acompanhar: " + ex.Message;
        }
        finally { discoveryBusy = false; }
    }
    private async void ApplyPolicies(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var selected = PolicyChoices.Children.OfType<StackPanel>().Select(r => r.Children[0]).OfType<CheckBox>()
            .Where(c => c.IsChecked == true && c.IsEnabled).Select(c => (UserPolicy)c.Tag).ToArray();
        if (selected.Length == 0) { Notice("Escolhe opções compatíveis com a tua edição do Windows.", true); return; }
        if (!await Confirm("Aplicar estas preferências?", string.Join("\n\n", selected.Select(p => p.Name + ": " + p.Description)) + "\n\nGuarda valores anteriores no histórico. São políticas do utilizador; a app não substitui políticas do computador. O Windows/Edge pode pedir reinício de sessão ou ignorar uma política não aplicável à conta.")) return;
        var messages = new List<string>();
        foreach (var p in selected)
        {
            try { messages.Add(optimizer.Apply(p.Key, p.DesiredState) ? "Valor guardado: " + p.Name : "Já estava configurado: " + p.Name); }
            catch (Exception ex) { messages.Add("Parou: " + ex.Message); Notice(string.Join("\n", messages), true); return; }
        }
        Notice(string.Join("\n", messages) + "\nConfere o efeito nas Definições. Não são ganhos de desempenho medidos.");
    });
    private async void ApplyDns(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (DnsAdapterChoice.SelectedItem is not DnsAdapter adapter || DnsProviderChoice.SelectedItem is not DnsProvider provider) { Notice("Escolhe adaptador e fornecedor.", true); return; }
        DnsSettings.RequireAdministrator();
        if (!await Confirm("Alterar DNS de " + adapter.Name + "?", "Servidor IPv4: " + provider.Address + " (" + provider.Name + ").\nO fornecedor recebe consultas DNS. Pode afetar acesso a nomes internos, filtros e serviços da tua rede. Não configura DNS cifrado ou melhora o ping de jogos. O valor estático/automático anterior fica guardado para undo. Não renova IP automaticamente.")) return;
        optimizer.Apply(adapter.Key, DnsSettings.State(false, provider.Address)); Notice("DNS configurado. O valor anterior está no Histórico.");
        if (FlushAfterDns.IsChecked == true) { try { await DnsSettings.FlushAsync(); } catch (Exception ex) { Notice("DNS configurado; limpeza de cache falhou: " + ex.Message, true); } }
        RefreshNetwork(this, new());
    });
    private async void AutomaticDns(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (DnsAdapterChoice.SelectedItem is not DnsAdapter adapter) { Notice("Escolhe o adaptador.", true); return; }
        DnsSettings.RequireAdministrator();
        if (!await Confirm("Usar DNS automático?", "Adaptador: " + adapter.Name + ". Usa os servidores fornecidos pela rede. O estado anterior fica no histórico; isto pode ser diferente de repor a última alteração.")) return;
        optimizer.Apply(adapter.Key, DnsSettings.State(true)); Notice("DNS automático configurado."); RefreshNetwork(this, new());
    });
    private async void FlushDns(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (!await Confirm("Limpar cache DNS?", "Remove respostas guardadas; as próximas consultas podem demorar mais. Não altera servidores, não termina ligações e não tem undo.")) return;
        await DnsSettings.FlushAsync(); Notice("A cache DNS foi limpa pela ferramenta do Windows."); SaveActivity("Rede", "Cache DNS limpa; sem undo.");
    });
    private async void RenewIp(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (DnsAdapterChoice.SelectedItem is not DnsAdapter adapter) { Notice("Escolhe o adaptador.", true); return; }
        DnsSettings.RequireAdministrator();
        if (!await Confirm("Renovar concessão IPv4?", "Adaptador: " + adapter.Name + ". Só está disponível para DHCP. Pode interromper a ligação e mudar o endereço IP. O NEXUS não liberta a concessão antes de renovar; o endereço é decidido pela rede, sem undo.")) return;
        await Task.Run(() => DnsSettings.Renew(adapter.Id)); Notice("O Windows confirmou a renovação DHCP."); SaveActivity("Rede", "Concessão DHCP renovada em " + adapter.Name + "; sem undo."); RefreshNetwork(this, new());
    });
    private async void MeasureLatency(object sender, RoutedEventArgs e)
    {
        if (latencyCancellation is not null) return;
        using var cancel = new CancellationTokenSource(); latencyCancellation = cancel; LatencyButton.IsEnabled = false;
        try { var result = await LatencyProbe.MeasureAsync(LatencyAddress.Text.Trim(), cancel.Token); if (!closed) { LatencyResultText.Text = result.Display; SaveActivity("Latência de rede", result.Display); } }
        catch (OperationCanceledException) { if (!closed) LatencyResultText.Text = "Teste cancelado."; }
        catch (Exception ex) { if (!closed) Notice("Teste ICMP falhou: " + ex.Message, true); }
        finally { latencyCancellation = null; if (!closed) LatencyButton.IsEnabled = true; }
    }
    private void CancelLatency(object sender, RoutedEventArgs e) => latencyCancellation?.Cancel();
    private async void TrimMemory(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (MemoryAppChoice.SelectedItem is not MemoryProcess selected) { Notice("Atualiza e escolhe uma aplicação em segundo plano.", true); return; }
        if (sessions.Active is not null) { Notice("Termina a sessão de jogo antes de reduzir working sets.", true); return; }
        if (!await Confirm("Reduzir RAM residente de " + selected.Name + "?", "Retira páginas da RAM residente desta aplicação; não liberta a memória que ela reservou. Pode provocar leituras do disco e tornar a próxima utilização mais lenta. O Windows volta a carregar páginas conforme precisa. Não há undo nem ganho de FPS garantido. Apps da biblioteca abertas e componentes do Windows são excluídos.")) return;
        var protectedIds = GameLibrary.Detect(library.Read()).Select(x => x.Id).ToHashSet();
        var bytes = await Task.Run(() => WorkingSetTrim.Trim(selected, protectedIds));
        Notice($"Variação da RAM residente: {bytes / 1048576d:F1} MiB. É uma leitura momentânea, não memória privada libertada."); RefreshMemory(this, new());
        SaveActivity("Memória", $"{selected.Name} · working set reduzido; variação observada {bytes / 1048576d:F1} MiB, sem undo ou ganho de FPS atribuído.");
    });
    private void CleanupLocationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (temporaryCancellation is not null || CleanupLocationChoice.SelectedItem is not CleanupLocation location) return;
        temporaryScan = null; TemporaryFiles.ItemsSource = null; RecycleTemporaryButton.IsEnabled = false;
        CleanupLocationText.Text = location.Description + "\n" + location.Path;
    }
    private async void EnableAutoCleanup(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (nextAutoCleanup != DateTimeOffset.MaxValue) { nextAutoCleanup = DateTimeOffset.MaxValue; AutoCleanupStatus.Text = "Desligada."; return; }
        if (AutoCleanupInterval.SelectedItem is not ComboBoxItem interval) return;
        if (!Directory.Exists(AutomaticCleanupPolicy.UserRoot)) { Notice("A pasta temporária local do utilizador não está disponível.", true); return; }
        if (!await Confirm("Ativar limpeza automática de temporários?", "Só enquanto o NEXUS estiver aberto. Pasta: " + AutomaticCleanupPolicy.UserRoot + ".\nA cada " + interval.Tag + " minutos revê esta pasta e envia à Lixeira até 100 ficheiros sem escrita/acesso há 30 dias, com máximo de 10 MiB por ficheiro e 256 MiB por execução. Não esvazia a Lixeira. Fica suspensa durante sessões e aplicações da biblioteca detetadas. Não há garantia de ausência de impacto e não inclui Prefetch, shaders ou ficheiros do sistema.")) return;
        nextAutoCleanup = DateTimeOffset.Now.AddMinutes(int.Parse((string)interval.Tag)); AutoCleanupStatus.Text = "Ativa nesta abertura da app. Próxima análise: " + nextAutoCleanup.ToString("HH:mm");
    });
    private async Task PollAutoCleanupAsync(bool force = false)
    {
        if (autoCleanupBusy || (!force && operationRunning) || temporaryCancellation is not null || benchmarkCancellation is not null || DateTimeOffset.Now < nextAutoCleanup) return;
        autoCleanupBusy = true;
        try
        {
            if (sessions.Active is not null || GameLibrary.Detect(library.Read()).Count != 0)
            { AutoCleanupStatus.Text = "Suspensa durante a sessão/aplicação da biblioteca."; return; }
            var cleaner = new TemporaryCleaner(maximumFiles: 1000);
            var scan = await Task.Run(() => cleaner.Scan(AutomaticCleanupPolicy.UserRoot, backgroundCancellation.Token));
            if (sessions.Active is not null || GameLibrary.Detect(library.Read()).Count != 0) { if (!closed) AutoCleanupStatus.Text = "Suspensa: uma sessão/aplicação abriu durante a análise."; return; }
            var selected = AutomaticCleanupPolicy.Select(scan.Files, DateTime.UtcNow);
            if (closed || nextAutoCleanup == DateTimeOffset.MaxValue) return;
            using var cleanupCancellation = CancellationTokenSource.CreateLinkedTokenSource(backgroundCancellation.Token);
            var recycling = cleaner.RecycleAsync(scan, selected, cleanupCancellation.Token);
            try
            {
                while (!recycling.IsCompleted)
                {
                    await Task.WhenAny(recycling, Task.Delay(1000, backgroundCancellation.Token));
                    if (recycling.IsCompleted) break;
                    if (closed || sessions.Active is not null || GameLibrary.Detect(library.Read()).Count != 0)
                    { cleanupCancellation.Cancel(); break; }
                }
            }
            catch { cleanupCancellation.Cancel(); await recycling; throw; }
            var result = await recycling;
            if (!closed) AutoCleanupStatus.Text = $"{DateTime.Now:HH:mm} · {result.Recycled} temporários enviados à Lixeira · {result.Errors.Count} erros.";
            SaveActivity("Limpeza automática", $"{result.Recycled} ficheiros enviados à Lixeira · {result.RecycledBytes} bytes · {result.Errors.Count} erros; Lixeira não esvaziada.");
        }
        catch (Exception ex) { if (!closed) AutoCleanupStatus.Text = "A limpeza não foi concluída: " + ex.Message; }
        finally
        {
            autoCleanupBusy = false;
            if (nextAutoCleanup != DateTimeOffset.MaxValue && AutoCleanupInterval.SelectedItem is ComboBoxItem interval)
                nextAutoCleanup = DateTimeOffset.Now.AddMinutes(int.Parse((string)interval.Tag));
        }
    }
    private async void RefreshOptionalApps(object sender, RoutedEventArgs e)
    {
        try { var apps = await Task.Run(OptionalApps.Read); if (!closed) OptionalAppList.ItemsSource = apps; }
        catch (Exception ex) { if (!closed) Notice("Não foi possível consultar pacotes: " + ex.Message, true); }
    }
    private async void RemoveOptionalApp(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (OptionalAppList.SelectedItem is not OptionalApp app || app.FullName is null) { Notice("Seleciona uma aplicação instalada.", true); return; }
        if (!await Confirm("Desinstalar " + app.Name + "?", "Só para o utilizador atual. A app fecha e os dados locais/configurações podem ser eliminados. Guarda primeiro o que precisas. Não tem undo; a reinstalação depende de a app continuar disponível na Microsoft Store. Não remove todas as apps de uma vez.")) return;
        await OptionalApps.RemoveAsync(app); Notice("Removida para o utilizador atual: " + app.Name + "."); RefreshOptionalApps(this, new());
        SaveActivity("Aplicações", "Removida para o utilizador atual: " + app.Name + ". Dados locais podem ter sido eliminados; sem undo.");
    });
    private async void RestoreOptionalApp(object sender, RoutedEventArgs e)
    {
        try
        {
            if (OptionalAppList.SelectedItem is not OptionalApp app) return;
            if (!await Windows.System.Launcher.LaunchUriAsync(OptionalApps.StoreSearch(app))) Notice("Não foi possível abrir a Microsoft Store.", true);
        }
        catch (Exception ex) { if (!closed) Notice("Não foi possível abrir a Store: " + ex.Message, true); }
    }
}
