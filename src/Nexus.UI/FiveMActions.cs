using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private void InitializeFiveMHelp()
    {
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = "Verificar o FiveM", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "No FiveM, abre F8 e escreve cl_drawperf true para consultar FPS, ping e perda de pacotes. Para ocultar, usa cl_drawperf false. São comandos de diagnóstico documentados pelo FiveM; a NEXUS não os executa.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Compara o mesmo servidor, local e percurso, com as mesmas definições gráficas. Repete cada condição três vezes. Espera pelo carregamento inicial; não limpes caches entre os testes. Uma mudança na carga do servidor pode invalidar a comparação.", TextWrapping = TextWrapping.Wrap });
        var fps = new Button { Content = "Comparar capturas de FPS" };
        fps.Click += (_, _) => ShowPage("benchmark"); panel.Children.Add(fps);
        var cpu = new Button { Content = "Consultar CPU das aplicações" };
        cpu.Click += (_, _) => ShowPage("memory"); panel.Children.Add(cpu);
        var help = new Button { Content = "Abrir documentação oficial do FiveM" };
        help.Click += async (_, _) =>
        {
            try { if (!await Windows.System.Launcher.LaunchUriAsync(new Uri("https://docs.fivem.net/docs/client-manual/console-commands/"))) Notice("Não foi possível abrir a documentação.", true); }
            catch (Exception ex) { Notice(ex.Message, true); }
        };
        panel.Children.Add(help);
        panel.Children.Add(new TextBlock { Text = "Ainda não há um resultado de desempenho deste PC no FiveM. Não altera ficheiros do jogo, recursos do servidor, anti-cheat ou definições gráficas; não aplica um perfil automaticamente.", TextWrapping = TextWrapping.Wrap, Opacity = .8 });
        HelpPanel.Children.Insert(1, new Border { Background = Color("#18201C"), BorderBrush = Color("#2B3830"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(22), Child = panel });
    }
}
