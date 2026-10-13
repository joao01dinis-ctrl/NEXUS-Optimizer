using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Hardware;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private readonly TextBlock processCpuResult = new() { Text = "Ainda não foram medidas as aplicações.", TextWrapping = TextWrapping.Wrap };
    private void InitializeProcessCpu()
    {
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = "Qual aplicação está a usar CPU?", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Consulta o uso de CPU de Discord, navegador, gravação e outras aplicações. A medição demora cerca de dois segundos e não altera os programas.", TextWrapping = TextWrapping.Wrap });
        var measure = new Button { Content = "Medir CPU das aplicações" }; measure.Click += MeasureProcessCpu; panel.Children.Add(measure);
        panel.Children.Add(processCpuResult);
        panel.Children.Add(new TextBlock { Text = "Percentagens sobre toda a capacidade de CPU, médias desta amostra. Processos protegidos, terminados ou fora da tua sessão podem não aparecer. Não mede GPU, rede ou impacto nos FPS. Usa capturas equivalentes para verificar uma diferença real.", TextWrapping = TextWrapping.Wrap, Opacity = .8 });
        MemoryPanel.Children.Insert(0, new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 31, 50)), CornerRadius = new CornerRadius(14), Padding = new Thickness(22), Child = panel });
    }
    private async void MeasureProcessCpu(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        processCpuResult.Text = "A medir…";
        try
        {
            var usages = await ProcessCpuProbe.MeasureAsync(backgroundCancellation.Token);
            if (!closed) processCpuResult.Text = $"Amostra terminada às {DateTime.Now:HH:mm:ss}\n\n" +
                (usages.Count == 0 ? "Sem processos acessíveis com duas amostras válidas." : string.Join("\n", usages.Select(x => x.Display)));
        }
        catch
        {
            if (!closed) processCpuResult.Text = "A medição não foi concluída. Não são apresentados valores parciais.";
            throw;
        }
    });
}
