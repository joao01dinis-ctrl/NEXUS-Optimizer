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
        AppLog.Initialize(Path.Combine(root, "logs"));
        AppLog.Info("NEXUS iniciado");
        window = new MainWindow(root);
        window.Closed += (_, _) => AppLog.Close();
        window.Activate();
    }
}
