using Microsoft.UI.Xaml;
namespace Nexus.UI;

public sealed partial class MainWindow : Window
{
    private readonly DashboardViewModel model;
    private readonly DispatcherTimer timer = new();
    private bool busy, closed;
    public MainWindow(string root)
    {
        InitializeComponent();
        model = new(root);
        InitializeOptimizer(root);
        InitializeAssistant(root);
        InitializeVpn();
        InitializeMouseLab(root);
        Root.DataContext = model;
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(1180, Math.Max(1, area.Width - 40)), Math.Min(900, Math.Max(1, area.Height - 40))));
        timer.Interval = TimeSpan.FromSeconds(2);
        timer.Tick += Tick;
        Root.Loaded += Loaded;
        Closed += (_, _) => { closed = true; timer.Stop(); };
    }
    private async void Loaded(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= Loaded;
        await model.InitializeAsync();
        if (closed)
            return;
        await model.TickAsync();
        if (!closed)
            timer.Start();
        await ScanInstalledGamesAsync();
        if (closed) return;
        if (Environment.GetCommandLineArgs().Contains("--smoke-test"))
        {
            try
            {
                var output = Environment.GetEnvironmentVariable("NEXUS_DATA_DIR") ?? throw new InvalidOperationException("Smoke test requires an isolated NEXUS_DATA_DIR.");
                if (Environment.GetEnvironmentVariable("NEXUS_TEST_PACKAGED_UNDO") == "1")
                    ValidatePackagedUndo(output);
                await Task.Delay(1500);
                if (Environment.GetEnvironmentVariable("NEXUS_STORE_SCREENSHOT") == "1")
                {
                    // Resize after activation; Windows constrains an initial window to the runner's desktop.
                    AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1400, 850));
                    await Task.Delay(500);
                }
                await model.TickAsync();
                var page = Environment.GetEnvironmentVariable("NEXUS_SCREENSHOT_PAGE");
                if (Environment.GetEnvironmentVariable("NEXUS_SMOKE_GAME_FIXTURES") == "1")
                    SeedGameCatalogSmokeFixtures(output);
                if (!string.IsNullOrEmpty(page)) { ShowPage(page); await Task.Delay(300); }
                if (page == "mouselab")
                {
                    await mouseLab!.ActivateAsync();
                    if(int.TryParse(Environment.GetEnvironmentVariable("NEXUS_MOUSE_LAB_SECTION"),out var section))mouseLab.SelectSection(section);
                    if(mouseLab.Capturing || mouseLab.AutomationEnabled || mouseLab.HasActiveSession || optimizer.History().Count!=0)
                        throw new InvalidOperationException("Mouse Lab read-only validation changed Windows or started monitoring.");
                    File.WriteAllText(Path.Combine(output,"mouselab-readonly.json"),System.Text.Json.JsonSerializer.Serialize(new
                        {Result="PASS",Devices=mouseLab.DeviceCount,Section=section,WindowsChanges=0,Capturing=false,Automation=false}));
                }
                if (page == "hardware")
                    for (var i = 0; extendedHardwareBusy && i < 100; i++) await Task.Delay(100);
                if (page == "protection")
                {
                    for (var i = 0; protectionReadBusy && i < 230; i++) await Task.Delay(100);
                    if (protectionReadBusy || protectionActionCount != 0 || optimizer.History().Count != 0 || AiUseToggle.IsOn)
                        throw new InvalidOperationException("Protection read-only validation failed.");
                    File.WriteAllText(Path.Combine(output, "protection-readonly.json"), System.Text.Json.JsonSerializer.Serialize(new
                    { Result = "PASS", StatusQueried = protectionStatus is not null, WindowsHealth = protectionStatus?.WindowsHealth.ToString(),
                        DefenderAvailable = protectionStatus?.Defender is not null, OperationsRequested = protectionActionCount, WindowsChanges = 0, CloudEnabled = false }));
                }
                if (page is "library" or "vpn")
                {
                    if (page == "vpn")
                    {
                        for (var i = 0; vpnReadBusy && i < 300; i++) await Task.Delay(100);
                        if (vpnReadBusy || vpnControlRequests != 0 || vpnExternalSamples != 0 || vpnMeasureBusy)
                            throw new InvalidOperationException("VPN read-only validation requested a connection or external probe.");
                    }
                    if (optimizer.History().Count != 0 || sessions.Active is not null || gameAutomation.Enabled || AiUseToggle.IsOn)
                        throw new InvalidOperationException("Library/VPN read-only validation changed settings or enabled automation.");
                    File.WriteAllText(Path.Combine(output, "library-vpn-readonly.json"), System.Text.Json.JsonSerializer.Serialize(new
                    { Result = "PASS", Games = gameCatalog.Read().Count, FixtureData = Environment.GetEnvironmentVariable("NEXUS_SMOKE_GAME_FIXTURES") == "1",
                        WindowsChanges = 0, CloudEnabled = false, VpnCanConnect = Nexus.Network.VpnReadiness.Evaluate(0, false, false).CanConnect,
                        WarpState = page == "vpn" ? vpnStatus?.State.ToString() : null, WarpInstalled = page == "vpn" && vpnStatus?.Installed == true,
                        VpnControlRequests = vpnControlRequests, VpnExternalSamples = vpnExternalSamples }));
                }
                if (Environment.GetEnvironmentVariable("NEXUS_SMOKE_LIGHT") == "1")
                { LightThemeToggle.IsOn = true; await Task.Delay(300); ApplyAppearance(); }
                if (Environment.GetEnvironmentVariable("NEXUS_SMOKE_ASSISTANT_READONLY") == "1")
                {
                    await RunIntelligentAsync(false);
                    if (boostReport is null || optimizer.History().Count != 0 || sessions.Active is not null || AiUseToggle.IsOn || AutoSafeToggle.IsOn)
                        throw new InvalidOperationException("Assistant read-only validation failed or changed settings.");
                    File.WriteAllText(Path.Combine(output, "assistant-analysis.json"), System.Text.Json.JsonSerializer.Serialize(new
                    { Result = "PASS", SampleCount = boostReport.Input.Samples.Count, Changes = optimizer.History().Count, CloudEnabled = AiUseToggle.IsOn, Summary = AssistantSummary.Text }));
                }
                if (page == "precision")
                {
                    CalculateSensitivity(this, new());
                    if (optimizer.History().Count != 0 || Nexus.Optimization.MousePrecision.EquivalentSensitivity(OriginalDpiBox.Value, OriginalSensitivityBox.Value, NewDpiBox.Value) != .5)
                        throw new InvalidOperationException("Precision page validation failed.");
                    File.WriteAllText(Path.Combine(output, "precision-readonly.json"), System.Text.Json.JsonSerializer.Serialize(new
                    { Result = "PASS", ActualMouse = Nexus.Optimization.MousePrecision.Resolve().Read(), EquivalentSensitivity = .5, Changes = optimizer.History().Count }));
                }
                if (Environment.GetEnvironmentVariable("NEXUS_SMOKE_ANALYZE_BOOST") == "1")
                {
                    if (page != "optimize") throw new InvalidOperationException("Smart Boost smoke test requires the optimize page.");
                    await AnalyzeSmartBoost();
                    if (boostReport is null) throw new InvalidOperationException("Smart Boost did not produce a report.");
                    if (optimizer.History().Count != 0 || sessions.Active is not null) throw new InvalidOperationException("Read-only smoke test changed the adjustment journal or started a session.");
                    File.WriteAllText(Path.Combine(output, "smart-boost-report.json"), System.Text.Json.JsonSerializer.Serialize(boostReport));
                }
                if (Environment.GetEnvironmentVariable("NEXUS_SMOKE_FRAME_CSV") is string csv)
                {
                    if (page != "benchmark") throw new InvalidOperationException("Frame smoke test requires the benchmark page.");
                    frameTimeImport = await Nexus.Benchmark.FrameTimes.ReadFileAsync(csv);
                    frameSource.Text = "TESTE DA INTERFACE — CSV artificial, não é uma sessão de jogo nem prova de ganho: " + frameTimeImport.FileName;
                    frameStreamChoice.ItemsSource = frameTimeImport.Streams; frameStreamChoice.SelectedIndex = 0;
                }
                if (SessionPriorityChoice.IsChecked == true || SessionAnimationsChoice.IsChecked == true || SessionPowerChoice.IsChecked == true)
                    throw new InvalidOperationException("Balanced defaults must not preselect session adjustments.");
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
                if (Environment.GetEnvironmentVariable("NEXUS_STORE_SCREENSHOT") == "1")
                {
                    // Capture the rendered window at its native pixel density, not DIP dimensions.
                    var scale = Root.XamlRoot.RasterizationScale;
                    await bitmap.RenderAsync(Root, (int)Math.Round(Root.ActualWidth * scale), (int)Math.Round(Root.ActualHeight * scale));
                }
                else await bitmap.RenderAsync(Root);
                var pixels = await bitmap.GetPixelsAsync();
                var path = Path.Combine(output, "dashboard.png");
                File.WriteAllBytes(path, []);
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
                using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                {
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                    using var reader = Windows.Storage.Streams.DataReader.FromBuffer(pixels);
                    var bytes = new byte[pixels.Length];
                    reader.ReadBytes(bytes);
                    encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, bytes);
                    await encoder.FlushAsync();
                }
                File.WriteAllText(Path.Combine(output, "smoke-test.json"), System.Text.Json.JsonSerializer.Serialize(new
                {
                    PackageIdentity = CurrentPackageIdentity(),
                    DataDirectory = output,
                    LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    TemporaryDirectory = Path.GetTempPath(),
                    model.CpuName,
                    model.GpuName,
                    model.RamText,
                    model.CpuText,
                    model.MemoryText,
                    model.Disks,
                    model.Status,
                    BoostGoal = OptimizationPresetList.SelectedItem?.ToString(),
                    SessionProfile = LibraryProfiles.SelectedItem?.ToString(),
                    PrioritySelected = SessionPriorityChoice.IsChecked,
                    AnimationsSelected = SessionAnimationsChoice.IsChecked,
                    PowerSelected = SessionPowerChoice.IsChecked,
                    SmartBoostRules = boostReport?.Rules.Count,
                    SelectedBoostChanges = boostSelection.Count(x => x.Choice.IsChecked == true),
                    Width = bitmap.PixelWidth,
                    Height = bitmap.PixelHeight,
                    RasterizationScale = Root.XamlRoot.RasterizationScale
                }));
            }
            catch (Exception error) { Nexus.Diagnostics.AppLog.Error(error, "Smoke test falhou"); Environment.ExitCode = 1; }
            Close();
        }
    }
    private static string? CurrentPackageIdentity()
    {
        try { return Windows.ApplicationModel.Package.Current.Id.FullName; }
        catch (InvalidOperationException) { return null; }
    }
    private static void ValidatePackagedUndo(string output)
    {
        // This opt-in integration check is confined to the disposable installed test identity.
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" ||
            CurrentPackageIdentity()?.StartsWith("NexusOptimizer.StoreValidation.07_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Packaged Undo validation requires the disposable hosted runner and test package.");
        const string key = "menu-animation";
        var original = Nexus.Optimization.WindowsSettings.Resolve(key).Read();
        if (original is not ("0" or "1")) throw new InvalidOperationException("Could not capture the actual Windows preference.");
        var target = original == "0" ? "1" : "0";
        var testOptimizer = new Nexus.Optimization.SystemOptimizer(Path.Combine(output, "native-validation", "nexus.db"), Nexus.Optimization.WindowsSettings.Resolve);
        try
        {
            Nexus.Optimization.AdjustmentStates.Set(testOptimizer, Nexus.Optimization.AdjustmentStates.Read(testOptimizer, key, target), true);
            var applied = Nexus.Optimization.WindowsSettings.Resolve(key).Read();
            if (applied != target) throw new InvalidOperationException("The packaged application did not change the actual Windows preference.");
            Nexus.Optimization.AdjustmentStates.Set(testOptimizer, Nexus.Optimization.AdjustmentStates.Read(testOptimizer, key, target), false);
            var restored = Nexus.Optimization.WindowsSettings.Resolve(key).Read();
            var history = testOptimizer.History();
            if (restored != original || history.Count == 0 || history.Any(x => x.State != "reverted"))
                throw new InvalidOperationException("Packaged Undo did not restore the captured original value and journal.");
            File.WriteAllText(Path.Combine(output, "packaged-undo.json"), System.Text.Json.JsonSerializer.Serialize(new
            { Result = "PASS", Key = key, Original = original, Applied = applied, Restored = restored, JournalState = history[0].State, PackageIdentity = CurrentPackageIdentity() }));
        }
        finally
        {
            var recovery = testOptimizer.RestoreAll();
            if (!recovery.Complete || Nexus.Optimization.WindowsSettings.Resolve(key).Read() != original)
                throw new InvalidOperationException("Packaged preference test could not restore the original Windows preference.");
        }
    }
    private async void Tick(object? sender, object e)
    {
        if (busy || closed)
            return;
        busy = true;
        try
        {
            await model.TickAsync();
            UpdateGraphs();
            PollSession();
            RefreshCatalogSessionState();
            await PollDiscoveryAsync();
            if (!closed) await PollInstalledGamesAsync();
            if (!closed) await PollAutoCleanupAsync();
            if (!closed) await PollSafeAnalysisAsync();
            if (!closed) await PollVpnStatusAsync();
            timer.Interval = TimeSpan.FromSeconds(resourceSaving ? 10 : model.SampleSeconds);
        }
        catch (Exception ex) { Nexus.Diagnostics.AppLog.Error(ex, "Atualizar monitor e tarefas opcionais"); }
        finally { busy = false; }
    }
}
