using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Core;
using Nexus.Hardware;
using Nexus.Gaming;
using Nexus.Optimization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private CancellationTokenSource? boostCancellation;
    private BoostReport? boostReport;
    private Task AnalyzeSmartBoost() => Run(AnalyzeSmartBoostCore);
    private async Task AnalyzeSmartBoostCore()
    {
        if (sessions.Active is not null) throw new InvalidOperationException("Termina e repõe a sessão antes de analisar PC Boost.");
        if (OptimizationPresetList.SelectedItem is not OptimizationPreset preset) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(backgroundCancellation.Token);
        boostCancellation = cancellation;
        boostReport = null; boostSelection.Clear(); BoostChoices.Children.Clear(); BoostAdvice.Children.Clear();
        OptimizationPresetList.IsEnabled = false; ApplyBoostButton.IsEnabled = false;
        CancelBoostButton.IsEnabled = true; AnalyzeBoostButton.IsEnabled = false;
        BoostStatus.Text = "A observar CPU e RAM durante cerca de 10 segundos… Podes cancelar.";
        try
        {
            var token = cancellation.Token;
            var hardware = new WindowsHardwareService();
            await Task.Run(hardware.ReadSample, token); // Prime GetSystemTimes; never count the initial null CPU as idle.
            var before = await Task.Run(() => ProcessCpuProbe.Read(token), token);
            var samples = new List<Sample>();
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                samples.Add(await Task.Run(hardware.ReadSample, token));
                if (!closed) BoostStatus.Text = $"A analisar · {i + 1}/5 amostras · ainda sem alterações.";
            }
            var after = await Task.Run(() => ProcessCpuProbe.Read(token), token);
            var previous = before.ToDictionary(x => (x.Id, x.Started));
            var processes = after.Where(p => previous.ContainsKey((p.Id, p.Started)))
                .Select(p => ProcessCpuProbe.Compare(previous[(p.Id, p.Started)], p, Environment.ProcessorCount, Stopwatch.Frequency))
                .OfType<ProcessCpuUsage>().OrderByDescending(p => p.Percent).Take(10).Select(p => new BoostProcess(p.Name, p.Percent)).ToArray();
            var volumes = await Task.Run(() =>
            {
                var result = new List<BoostVolume>();
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
                    try { if (drive.IsReady) result.Add(new(drive.Name, drive.TotalSize, drive.AvailableFreeSpace)); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                return result.ToArray();
            }, token);
            IReadOnlyList<string> drivers;
            try { drivers = await Task.Run(HardwareEnvironment.GraphicsDrivers, token); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Nexus.Diagnostics.AppLog.Error(ex, "Consultar driver gráfico"); drivers = []; }
            token.ThrowIfCancellationRequested();
            if (closed) return;
            var power = HardwareEnvironment.ReadPower();
            var items = UserPreferences.All.Where(p => SmartBoost.VisualKeys.Contains(p.Key))
                .Select(p => new DirectAdjustment(p.Key, p.Name, p.Description, p.Value)).ToList();
            items.Insert(0, new("animations", "Reduzir animações de conteúdo", "Menos transições na interface, sem ganho de FPS presumido.", "0"));
            var states = items.ToDictionary(p => p.Key, p => AdjustmentStates.Read(optimizer, p.Key, p.Desired));
            var goal = preset.Name switch { "Competitivo" or "Máximo" => BoostGoal.Competitive, "Streaming" => BoostGoal.Streaming, _ => BoostGoal.Balanced };
            var input = new BoostInput(goal, samples.AsReadOnly(), volumes, processes, power.OnAcPower, power.BatterySaver,
                model.CpuName, model.GpuName, RuntimeInformation.OSDescription, drivers,
                items.Select(p => new BoostSetting(p.Key, p.Name, p.Desired, states[p.Key].Current, states[p.Key].Error)).ToArray());
            boostReport = SmartBoost.Evaluate(input);
            foreach (var rule in boostReport.Rules.Where(r => r.SettingKey is not null))
            {
                var item = items.Single(p => p.Key == rule.SettingKey); var state = states[item.Key];
                var eligible = rule.Decision != BoostDecision.Ignore && state.Error is null && !state.IsOn;
                var choice = new CheckBox { Content = item.Name, IsEnabled = eligible, IsChecked = eligible && rule.Decision == BoostDecision.Apply };
                boostSelection.Add((choice, item, state));
                var line = new StackPanel { Spacing = 6 }; line.Children.Add(choice);
                line.Children.Add(new TextBlock { Text = SmartBoost.DecisionName(rule.Decision) + " · " + rule.Reason, TextWrapping = TextWrapping.Wrap, Foreground = Color("#C3CBE0") });
                BoostChoices.Children.Add(new Border { Child = line, Padding = new Thickness(18), CornerRadius = new CornerRadius(12), Background = Color("#171D2F"), BorderBrush = Color("#2A3350"), BorderThickness = new Thickness(1) });
            }
            foreach (var rule in boostReport.Rules.Where(r => r.SettingKey is null))
            {
                var advice = new StackPanel { Spacing = 8 };
                advice.Children.Add(new TextBlock { Text = rule.Title + " · " + SmartBoost.DecisionName(rule.Decision) + "\n" + rule.Reason + "\n" + rule.Measurement + "\n" + rule.Revert, TextWrapping = TextWrapping.Wrap });
                if (rule.Decision == BoostDecision.ManualTest)
                {
                    var target = rule.Id switch { "nexus.cpu.contention" or "nexus.memory.pressure" => "memory", "nexus.storage.space" => "tools", "nexus.power.session" or "nexus.game.priority" => "gaming", _ => null };
                    var button = new Button { Content = "Rever " + rule.Title };
                    if (target is not null) button.Click += (_, _) => ShowPage(target);
                    else { button.Tag = rule.Id == "nexus.startup.review" ? "ms-settings:startupapps" : "ms-settings:display-advancedgraphics"; button.Click += OpenSetting; }
                    advice.Children.Add(button);
                }
                BoostAdvice.Children.Add(advice);
            }
            var latest = samples[^1];
            var recommended = boostReport.Rules.Count(r => r.SettingKey is not null && r.Decision == BoostDecision.Apply);
            BoostStatus.Text = $"{preset.Name} · {recommended} preferências recomendadas · 5 amostras em {(samples[^1].At - samples[0].At).TotalSeconds:F1}s.\n" +
                (boostReport.SustainedCpuPressure || boostReport.SustainedMemoryPressure ? "Foi observada pressão sustentada. Revê as recomendações e os consumidores abaixo." : "Os dados disponíveis não justificam recomendar ajustes de interface. Ajustes opcionais ficam por selecionar.") +
                "\nAnalisar não altera o PC; os ajustes de interface não demonstram aumento de FPS.";
            BoostHardwareText.Text = $"{model.CpuName}\n{model.GpuName}\nRAM: {latest.TotalGb:F1} GB · {latest.TotalGb - latest.UsedGb:F1} GB disponíveis na última amostra\n" +
                string.Join("\n", drivers) + "\n" + (power.OnAcPower is null ? "Alimentação desconhecida" : power.OnAcPower == true ? "Ligado à corrente" : "A usar bateria") +
                "\n" + (processes.Length == 0 ? "Sem processos acessíveis com duas amostras." : "CPU das aplicações na observação:\n" + string.Join("\n", processes.Take(5).Select(p => $"{p.Name}: {p.CpuPercent:F1}% da capacidade total"))) +
                "\nTemperaturas, GPU Busy e clocks não medidos. Não é diagnóstico de limitação CPU/GPU.";
            SaveActivity("Smart Boost", $"Regras {SmartBoost.RuleVersion} · {preset.Name}\n" + string.Join("\n", boostReport.Rules.Select(r => r.Title + ": " + SmartBoost.DecisionName(r.Decision) + " · " + r.Reason)));
        }
        catch (OperationCanceledException) { if (!closed) BoostStatus.Text = "Análise cancelada. Nenhum ajuste foi aplicado; volta a analisar."; }
        finally
        {
            boostCancellation = null;
            if (!closed) { OptimizationPresetList.IsEnabled = true; CancelBoostButton.IsEnabled = false; AnalyzeBoostButton.IsEnabled = true; ApplyBoostButton.IsEnabled = boostReport is not null; }
        }
    }
    private void CancelBoost(object sender, RoutedEventArgs e) => boostCancellation?.Cancel();
    private static void RequireSessionPower(PowerPlan? plan)
    {
        if (plan is null) return;
        var power = HardwareEnvironment.ReadPower();
        SessionPowerPolicy.RequireAc(power.OnAcPower, power.BatterySaver);
    }
    private async void ExportBoostReport(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (boostReport is null) throw new InvalidOperationException("Analisa primeiro o PC para preparar o relatório.");
        var report = boostReport;
        var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(AppWindow.Id);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null || closed) return;
        var path = Path.Combine(folder.Path, "NEXUS-diagnostico-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }), backgroundCancellation.Token);
        Notice("Relatório guardado localmente: " + path + ". Inclui hardware e nomes de aplicações; não foi enviado.");
    });
}
