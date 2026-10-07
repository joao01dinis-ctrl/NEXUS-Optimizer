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
        if (Environment.GetCommandLineArgs().Contains("--smoke-test"))
        {
            try
            {
                var output = Environment.GetEnvironmentVariable("NEXUS_DATA_DIR") ?? throw new InvalidOperationException("Smoke test requires an isolated NEXUS_DATA_DIR.");
                await Task.Delay(1500);
                await model.TickAsync();
                var page = Environment.GetEnvironmentVariable("NEXUS_SCREENSHOT_PAGE");
                if (!string.IsNullOrEmpty(page)) { ShowPage(page); await Task.Delay(300); }
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
                await bitmap.RenderAsync(Root);
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
                    model.CpuName,
                    model.GpuName,
                    model.RamText,
                    model.CpuText,
                    model.MemoryText,
                    model.Disks,
                    model.Status,
                    Width = bitmap.PixelWidth,
                    Height = bitmap.PixelHeight
                }));
            }
            catch (Exception error) { Nexus.Diagnostics.AppLog.Error(error, "Smoke test falhou"); Environment.ExitCode = 1; }
            Close();
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
            PollSession();
            timer.Interval = TimeSpan.FromSeconds(model.SampleSeconds);
        }
        finally { busy = false; }
    }
}
