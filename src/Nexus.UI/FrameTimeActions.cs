using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Benchmark;
using Nexus.Storage;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private FrameTimeStore frameTimeStore = null!;
    private FrameTimeImport? frameTimeImport;
    private readonly ComboBox frameStreamChoice = new() { HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Escolhe o jogo e o fluxo de imagem" };
    private readonly TextBox frameContext = new() { MaxLength = 600, Header = "Condições do teste", PlaceholderText = "Mesmo PC, jogo/versão, cena, resolução, qualidade, driver, limite de FPS e VSync" };
    private readonly TextBlock frameSource = new() { Text = "Nenhum CSV importado.", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock frameDetails = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock frameMethod = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock frameComparison = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox frameBaseline = new() { Header = "Antes — referência", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox frameCurrent = new() { Header = "Depois — nova captura", HorizontalAlignment = HorizontalAlignment.Stretch };

    private void InitializeFrameTimes(string root)
    {
        frameTimeStore = new(Path.Combine(root, "nexus.db"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(frameStreamChoice, "Aplicação e fluxo de imagem da captura");
        var content = new StackPanel { Spacing = 14 };
        content.Children.Add(new TextBlock { Text = "Confere os FPS com dados de jogo", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = "Escolhe uma captura de jogo feita no PresentMon. Consulta FPS e pausas e compara dois testes feitos nas mesmas condições. A NEXUS ainda não faz a captura automaticamente.", TextWrapping = TextWrapping.Wrap });
        var import = new Button { Content = "Escolher CSV do PresentMon" }; import.Click += ImportFrameTimes; content.Children.Add(import);
        content.Children.Add(frameStreamChoice);
        frameStreamChoice.SelectionChanged += (_, _) =>
        {
            frameDetails.Text = frameStreamChoice.SelectedItem is FrameTimeSummary s ? Describe(s) : "";
            frameMethod.Text = frameStreamChoice.SelectedItem is FrameTimeSummary selected ? DescribeMethod(selected) : "";
        };
        content.Children.Add(frameDetails); content.Children.Add(frameContext);
        var save = new Button { Content = "Guardar análise e condições" }; save.Click += SaveFrameTimes; content.Children.Add(save);
        content.Children.Add(new TextBlock { Text = "Compara o mesmo percurso, com pelo menos 20 segundos e 300 intervalos. Repete cada condição pelo menos três vezes, sem downloads ou outras cargas variáveis. As condições são declaradas por ti; a NEXUS não as confirma a partir do CSV.", TextWrapping = TextWrapping.Wrap, Opacity = .8 });
        content.Children.Add(frameBaseline); content.Children.Add(frameCurrent);
        frameBaseline.SelectionChanged += (_, _) => frameComparison.Text = "";
        frameCurrent.SelectionChanged += (_, _) => frameComparison.Text = "";
        var compare = new Button { Content = "Comparar capturas guardadas" }; compare.Click += CompareFrameTimes; content.Children.Add(compare);
        content.Children.Add(frameComparison);
        var details = new StackPanel { Spacing = 10 };
        details.Children.Add(new TextBlock { Text = "Formato: CSV por fotograma com MsBetweenPresents (métricas v1). Cada aplicação/PID/fluxo é analisado separadamente; escolhe o fluxo principal do jogo.", TextWrapping = TextWrapping.Wrap });
        details.Children.Add(frameSource); details.Children.Add(frameMethod);
        content.Children.Add(new Expander { Header = "Origem dos dados e método de cálculo", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = details });
        content.Children.Add(new TextBlock { Text = "FPS apresentados pela aplicação, incluindo apresentações não mostradas: não é FPS efetivo no ecrã, fotogramas gerados ou latência de entrada. Uma diferença isolada não prova um ganho causado por um ajuste. Não são gerados números sem um CSV.", TextWrapping = TextWrapping.Wrap, Opacity = .8 });
        var help = new Button { Content = "Abrir documentação oficial do PresentMon" };
        help.Click += async (_, _) =>
        {
            try { if (!await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md"))) Notice("Não foi possível abrir a documentação.", true); }
            catch (Exception ex) { Notice(ex.Message, true); }
        };
        content.Children.Add(help);
        BenchmarkPanel.Children.Insert(0, new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 31, 50)), CornerRadius = new CornerRadius(14), Padding = new Thickness(22), Child = content });
        RefreshFrameTimeHistory();
    }
    private static string Describe(FrameTimeSummary s) =>
        $"{s.Application} · PID {s.ProcessId}\n" +
        $"FPS de apresentação: {s.AverageFps:F1} · 1% low: {s.OnePercentLowFps:F1}\n" +
        $"Intervalo mediano: {s.MedianMs:F2} ms · P99: {s.P99Ms:F2} ms\n" +
        $"{s.Frames} intervalos válidos · {s.Seconds:F2} segundos analisados · {s.Omitted} intervalos omitidos\n" +
        (s.Frames < 300 || s.Seconds < 20 || s.Omitted > 1 ? "Captura curta/incompleta: consulta apenas, sem comparação antes/depois." : "");
    private static string DescribeMethod(FrameTimeSummary s) =>
        $"Fluxo de imagem: {s.SwapChain}. A duração é a soma dos intervalos válidos.\n" +
        (s.Dropped is int dropped ? $"Dropped na origem: {dropped}; mantidos na análise dos Presents.\n" : "A origem não informa Dropped.\n") +
        "1% low = 1000 / média do 1% de intervalos mais lentos (arredondado para cima). P99/mediana usam nearest rank. Pausas válidas são mantidas; zero/NA são contados como omitidos.";
    private async void ImportFrameTimes(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id);
        picker.FileTypeFilter.Add(".csv");
        var file = await picker.PickSingleFileAsync();
        if (file is null || closed) return;
        frameTimeImport = null; frameStreamChoice.ItemsSource = null; frameDetails.Text = "";
        frameSource.Text = "A ler e validar o CSV…";
        try
        {
            var imported = await FrameTimes.ReadFileAsync(file.Path, backgroundCancellation.Token);
            if (closed) return;
            frameTimeImport = imported;
            frameSource.Text = $"Origem: {imported.FileName}\nSHA-256: {imported.Sha256}\n{imported.Streams.Count} fluxos analisáveis; escolhe um. Dados locais do ficheiro, sem confirmação de autenticidade ou upload.";
            frameStreamChoice.ItemsSource = imported.Streams; frameStreamChoice.SelectedIndex = 0;
        }
        catch
        {
            if (!closed) frameSource.Text = "Importação falhou. Nenhuma análise nova foi guardada.";
            throw;
        }
    });
    private async void SaveFrameTimes(object sender, RoutedEventArgs e) => await Run(() =>
    {
        if (frameTimeImport is null || frameStreamChoice.SelectedItem is not FrameTimeSummary selected)
            throw new InvalidOperationException("Importa e escolhe primeiro uma captura.");
        var capture = new FpsCapture(DateTimeOffset.Now, frameTimeImport.FileName, frameTimeImport.Sha256, frameContext.Text.Trim(), selected);
        if (!FrameTimes.Valid(capture)) throw new InvalidOperationException("Preenche as condições do teste numa linha, até 600 caracteres.");
        frameTimeStore.Save(capture); RefreshFrameTimeHistory(); Notice("Análise guardada localmente, com a origem e as condições declaradas.");
        return Task.CompletedTask;
    });
    private void RefreshFrameTimeHistory()
    {
        try
        {
            var captures = frameTimeStore.History(); frameBaseline.ItemsSource = frameCurrent.ItemsSource = captures;
            if (captures.Count > 0) frameCurrent.SelectedIndex = 0;
            if (captures.Count > 1) frameBaseline.SelectedIndex = 1;
        }
        catch (Exception ex) { frameComparison.Text = "Histórico de capturas indisponível: " + ex.Message; }
    }
    private void CompareFrameTimes(object sender, RoutedEventArgs e)
    {
        if (operationRunning) return;
        if (frameBaseline.SelectedItem is not FpsCapture before || frameCurrent.SelectedItem is not FpsCapture after)
        { frameComparison.Text = "Guarda duas capturas e escolhe a referência e o novo teste."; return; }
        var result = FrameTimes.Compare(after, before, out var reason);
        frameComparison.Text = result is null ? reason :
            $"FPS: {before.Summary.AverageFps:F1} → {after.Summary.AverageFps:F1} ({result.AveragePercent:+0.0;-0.0;0}%)\n" +
            $"1% low: {before.Summary.OnePercentLowFps:F1} → {after.Summary.OnePercentLowFps:F1} ({result.LowPercent:+0.0;-0.0;0}%)\n" +
            $"P99: {before.Summary.P99Ms:F2} → {after.Summary.P99Ms:F2} ms ({result.P99Percent:+0.0;-0.0;0}%; menor é melhor)\n" +
            "Comparação isolada, com condições declaradas iguais. Repete os testes; não demonstra que um ajuste causou esta diferença.";
    }
}
