using Microsoft.UI.Xaml;
using Nexus.Diagnostics;
namespace Nexus.UI;

public partial class App : Application
{
    private Window? window;
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => AppLog.Error(e.Exception, "Erro não tratado: " + e.Message);
        DebugSettings.XamlResourceReferenceFailed += (_, e) => AppLog.Info("Recurso XAML: " + e.Message);
        DebugSettings.BindingFailed += (_, e) => AppLog.Info("Binding: " + e.Message);
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var root = Environment.GetEnvironmentVariable("NEXUS_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusOptimizer");
        try
        {
            AppLog.Initialize(Path.Combine(root, "logs"));
            AppLog.Info("NEXUS iniciado");
            window = new MainWindow(root);
            window.Closed += (_, _) => AppLog.Close();
            window.Activate();
        }
        catch (Exception error)
        {
            AppLog.Error(error, "Não foi possível iniciar a NEXUS");
            if (Environment.GetCommandLineArgs().Contains("--smoke-test"))
            {
                try { Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "startup-error.txt"), error.ToString()); } catch (IOException) { }
                AppLog.Close(); Environment.Exit(1); return;
            }
            var policyBlock = error.HResult == unchecked((int)0x800711C7) || error.ToString().Contains("0x800711C7", StringComparison.OrdinalIgnoreCase);
            var message = policyBlock
                ? "O Controlo de Aplicações do Windows impediu o carregamento da NEXUS. Esta versão é experimental e sem assinatura de distribuição. É necessária uma versão aceite pela política deste PC. Não foram aplicados ajustes nem alteradas proteções."
                : "A NEXUS não conseguiu iniciar. Consulta os detalhes abaixo e os logs para identificar o problema.";
            var close = new Microsoft.UI.Xaml.Controls.Button { Content = "Fechar" };
            var content = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 16, Padding = new Thickness(24) };
            content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = "Não foi possível abrir a NEXUS", FontSize = 24, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = error.Message, TextWrapping = TextWrapping.Wrap, Opacity = .7 });
            content.Children.Add(close);
            window = new Window { Title = "NEXUS · problema no arranque", Content = new Microsoft.UI.Xaml.Controls.ScrollViewer { Content = content } };
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(720, 520));
            close.Click += (_, _) => { AppLog.Close(); Environment.Exit(1); };
            window.Closed += (_, _) => { AppLog.Close(); Environment.Exit(1); };
            window.Activate();
        }
    }
}
