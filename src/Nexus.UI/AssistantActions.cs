using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Optimization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private bool assistantBusy, assistantInitialized;
    private string uiPreferencesPath = "";
    private DateTimeOffset nextSafeAnalysis = DateTimeOffset.MaxValue;
    private bool resourceSaving;
    private sealed record UiPreferences(bool LightTheme = false, bool ResourceSaving = false);

    private void InitializeAssistant(string root)
    {
        uiPreferencesPath = Path.Combine(root, "appearance.json");
        UiPreferences prefs = new();
        try { if (File.Exists(uiPreferencesPath)) prefs = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(uiPreferencesPath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        LightThemeToggle.IsOn = prefs.LightTheme;
        ResourceSavingToggle.IsOn = resourceSaving = prefs.ResourceSaving;
        model.SamplingOverrideSeconds = resourceSaving ? 10 : null;
        var configured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        AiConnectionStatus.Text = configured ? "Chave disponível nesta sessão. A IA só é usada depois de ativares a opção abaixo." : "Sem chave disponível. A análise e a otimização locais funcionam sem a OpenAI.";
        AiUseToggle.IsEnabled = configured;
        assistantInitialized = true;
        ApplyAppearance();
        Root.Loaded += (_, _) => ApplyAppearance();
        Closed += (_, _) => { AutoSafeToggle.IsOn = false; nextSafeAnalysis = DateTimeOffset.MaxValue; };
    }
    private async void SmartAnalyze(object sender, RoutedEventArgs e) => await RunIntelligentAsync(false);
    private async void SmartOptimize(object sender, RoutedEventArgs e) => await RunIntelligentAsync(true);
    private async Task RunIntelligentAsync(bool apply, bool periodic = false)
    {
        if (assistantBusy || operationRunning || closed) return;
        if (sessions.Active is not null || gameAutomation.Enabled)
        {
            if (!periodic) Notice("Termina e repõe a sessão de jogo e pausa o Game Mode automático antes de analisar.", true);
            return;
        }
        assistantBusy = true;
        OperationInfo.IsOpen = false;
        AssistantProgress.Visibility = Visibility.Visible;
        HomeOptimizeButton.IsEnabled = HomeAnalyzeButton.IsEnabled = QuickOptimizeButton.IsEnabled = false;
        AssistantSummary.Text = "A observar CPU e RAM. Ainda não foi alterada nenhuma definição…";
        try
        {
            await Run(async () =>
            {
                await AnalyzeSmartBoostCore();
                if (boostReport is null || closed) { AssistantSummary.Text = "A análise não foi concluída. Nenhuma otimização automática aplicada."; return; }
                var report = boostReport;
                AssistantEvidence.Text = $"5 amostras · CPU sustentada: {(report.SustainedCpuPressure ? "elevada" : "sem pressão confirmada")} · RAM sustentada: {(report.SustainedMemoryPressure ? "elevada" : "sem pressão confirmada")}";
                string? aiSummary = null;
                if (!periodic && AiUseToggle.IsOn)
                {
                    var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        AssistantSummary.Text = "A pedir à OpenAI uma explicação dos dados resumidos…";
                        try
                        {
                            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
                            var advice = await new OpenAiAdvisor(client, key, AiModelBox.Text.Trim()).AnalyzeAsync(report, backgroundCancellation.Token);
                            aiSummary = "OpenAI · " + advice.Summary + "\n" + string.Join("\n", advice.Observations.Select(s => "• " + s));
                            AiConnectionStatus.Text = "Última análise OpenAI concluída. Nenhuma permissão de execução foi dada ao modelo.";
                        }
                        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException or KeyNotFoundException)
                        {
                            if (closed) return;
                            AiConnectionStatus.Text = "IA indisponível nesta análise. A análise local mantém-se disponível.";
                            aiSummary = "Não foi possível obter uma resposta OpenAI válida. Foram usadas apenas as regras locais.";
                        }
                    }
                }
                if (closed) return;
                var plan = AutomaticBoost.Prepare(report, DateTimeOffset.Now);
                if (periodic)
                {
                    if (!AutoSafeToggle.IsOn) { AssistantSummary.Text = "Automático pausado antes de aplicar."; return; }
                    plan = AutomaticBoost.RespectUserChanges(plan, optimizer.History(), optimizer.FindDrift());
                }
                var applied = apply ? plan.Apply(optimizer, DateTimeOffset.Now) : 0;
                var result = apply
                    ? applied == 0 ? "Análise concluída. Não foi necessário aplicar novos ajustes automáticos." : $"Concluído · {applied} preferências visuais ajustadas e verificadas. Podes desfazer no Histórico."
                    : $"Análise concluída · {plan.Changes.Count} preferências visuais elegíveis. O PC não foi alterado.";
                AssistantSummary.Text = result + (aiSummary is null ? "" : "\n\n" + aiSummary);
                BoostStatus.Text = result;
                if (apply && applied > 0)
                {
                    foreach (var selected in boostSelection) selected.Choice.IsEnabled = false;
                    boostReport = null; ApplyBoostButton.IsEnabled = false;
                }
                SaveActivity(periodic ? "Automático local" : "NEXUS Intelligence", result);
            });
        }
        finally
        {
            assistantBusy = false;
            if (!closed)
            {
                AssistantProgress.Visibility = Visibility.Collapsed;
                HomeOptimizeButton.IsEnabled = HomeAnalyzeButton.IsEnabled = QuickOptimizeButton.IsEnabled = true;
                if (OperationInfo.IsOpen && OperationInfo.Severity == InfoBarSeverity.Warning)
                    AssistantSummary.Text = "A operação não foi concluída. Consulta a mensagem abaixo e o Histórico antes de tentar de novo.";
                if (periodic && OperationInfo.IsOpen && OperationInfo.Severity == InfoBarSeverity.Warning)
                { AutoSafeToggle.IsOn = false; AutoSafeStatus.Text = "Automático pausado após um erro. Revê o Histórico antes de o voltares a ativar."; }
                if (LightThemeToggle.IsOn) ApplyAppearance();
            }
        }
    }
    private void AutoSafeChanged(object sender, RoutedEventArgs e)
    {
        if (!assistantInitialized || closed) return;
        nextSafeAnalysis = AutoSafeToggle.IsOn ? DateTimeOffset.Now.AddMinutes(15) : DateTimeOffset.MaxValue;
        AutoSafeStatus.Text = AutoSafeToggle.IsOn ? "Ativo nesta sessão · próxima análise em 15 minutos. Sem chamadas OpenAI, sem limpeza de ficheiros e sem reaplicar ajustes desfeitos ou alterados fora da NEXUS." : "Desligado. As alterações anteriores continuam disponíveis para restauro no Histórico.";
    }
    private async Task PollSafeAnalysisAsync()
    {
        if (!AutoSafeToggle.IsOn || DateTimeOffset.Now < nextSafeAnalysis || assistantBusy || operationRunning ||
            sessions.Active is not null || gameAutomation.Enabled || autoCleanupBusy || benchmarkCancellation is not null || temporaryCancellation is not null) return;
        nextSafeAnalysis = DateTimeOffset.Now.AddMinutes(15);
        await RunIntelligentAsync(true, periodic: true);
    }
    private void RefreshMousePrecision(object sender, RoutedEventArgs e)
    {
        try
        {
            var state = MousePrecision.Resolve().Read();
            MouseStateText.Text = "Aceleração do Windows: " + (MousePrecision.ParseState(state)[2] == 0 ? "desligada" : "ligada") + ". O valor anterior é guardado antes de qualquer alteração.";
        }
        catch (Exception ex) { MouseStateText.Text = "Não foi possível consultar o rato: " + ex.Message; }
    }
    private async void DisableMouseAcceleration(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (sessions.Active is not null) throw new InvalidOperationException("Termina e repõe a sessão antes de alterar o rato.");
        var before = MousePrecision.Resolve().Read();
        if (MousePrecision.ParseState(before)[2] == 0) { Notice("A aceleração já está desligada."); return; }
        if (!await Confirm("Desligar a aceleração do ponteiro?", "Muda a sensação do ponteiro em aplicações que usam a entrada do Windows. Não muda a sensibilidade de jogos com Raw Input. O valor anterior fica guardado para restauro.")) return;
        optimizer.ApplyBatch([new(MousePrecision.AccelerationKey, "0,0,0", before)]);
        RefreshMousePrecision(this, new()); Notice("Aceleração desligada. Podes repor exatamente os valores anteriores.");
    });
    private async void RestoreMouseAcceleration(object sender, RoutedEventArgs e) => await Run(() =>
    {
        var record = optimizer.History().FirstOrDefault(x => x.Key == MousePrecision.AccelerationKey && x.State is "applied" or "pending");
        Notice(record is null ? "Não há uma alteração do rato por repor." : optimizer.Undo(record.Id));
        RefreshMousePrecision(this, new()); return Task.CompletedTask;
    });
    private void CalculateSensitivity(object sender, RoutedEventArgs e)
    {
        try { SensitivityResult.Text = $"Sensibilidade equivalente: {MousePrecision.EquivalentSensitivity(OriginalDpiBox.Value, OriginalSensitivityBox.Value, NewDpiBox.Value):0.####} · mesma escala linear, sem alterar o jogo."; }
        catch (ArgumentException ex) { SensitivityResult.Text = ex.Message; }
    }
    private void ResourceModeChanged(object sender, RoutedEventArgs e)
    {
        if (!assistantInitialized) return;
        resourceSaving = ResourceSavingToggle.IsOn;
        model.SamplingOverrideSeconds = resourceSaving ? 10 : null;
        timer.Interval = TimeSpan.FromSeconds(resourceSaving ? 10 : model.SampleSeconds);
        SaveUiPreferences();
    }
    private void ThemeChanged(object sender, RoutedEventArgs e)
    {
        if (!assistantInitialized) return;
        ApplyAppearance(); SaveUiPreferences();
    }
    private void SaveUiPreferences()
    {
        try { File.WriteAllText(uiPreferencesPath, JsonSerializer.Serialize(new UiPreferences(LightThemeToggle.IsOn, resourceSaving))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notice("A preferência não pôde ser guardada.", true); }
    }
    private static readonly Dictionary<string, string> LightPalette = new()
    {
        ["#0C0F1A"] = "#F4F5FA", ["#111525"] = "#EAEDF6", ["#171D2F"] = "#FFFFFF", ["#2A3350"] = "#D5DCEA",
        ["#302C50"] = "#E6DFFF", ["#95A1BF"] = "#53617F", ["#8390AE"] = "#53617F", ["#C3CBE0"] = "#43516D", ["#B5A4FF"] = "#6851AF"
    };
    private readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, object>> originalAppearance = new();
    private void ApplyAppearance()
    {
        Root.RequestedTheme = LightThemeToggle.IsOn ? ElementTheme.Light : ElementTheme.Dark;
        Recolor(Root, LightThemeToggle.IsOn);
    }
    private void AdaptAppearanceProperty(DependencyObject element, DependencyProperty property, bool light)
    {
        var originals = originalAppearance.GetOrCreateValue(element);
        if (!light)
        {
            if (originals.Remove(property, out var original))
            {
                if (ReferenceEquals(original, DependencyProperty.UnsetValue)) element.ClearValue(property);
                else element.SetValue(property, original);
            }
            return;
        }
        if (originals.ContainsKey(property) || element.GetValue(property) is not SolidColorBrush solid || solid.Color.A != 255) return;
        var value = $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}";
        if (!LightPalette.TryGetValue(value, out var target)) return;
        // Preserve local values/absence instead of freezing theme-provided text
        // colours. Dark appearance restores the original XAML/style inheritance.
        originals.Add(property, element.ReadLocalValue(property));
        element.SetValue(property, Color(target));
    }
    private void Recolor(DependencyObject element, bool light)
    {
        // The hero deliberately keeps a dark surface in both app appearances.
        if (element is FrameworkElement fixedTheme && !ReferenceEquals(element, Root) && fixedTheme.RequestedTheme == ElementTheme.Dark) return;
        if (element is Panel) AdaptAppearanceProperty(element, Panel.BackgroundProperty, light);
        if (element is Border) { AdaptAppearanceProperty(element, Border.BackgroundProperty, light); AdaptAppearanceProperty(element, Border.BorderBrushProperty, light); }
        if (element is TextBlock) AdaptAppearanceProperty(element, TextBlock.ForegroundProperty, light);
        if (element is Control) { AdaptAppearanceProperty(element, Control.BackgroundProperty, light); AdaptAppearanceProperty(element, Control.ForegroundProperty, light); AdaptAppearanceProperty(element, Control.BorderBrushProperty, light); }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) Recolor(VisualTreeHelper.GetChild(element, i), light);
    }
}
