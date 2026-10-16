using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Hardware;
using Nexus.Optimization;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private async void ApplyMouseSpeed(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (sessions.Active is not null) throw new InvalidOperationException("Termina e repõe a sessão antes de alterar o rato.");
        var value = MouseSpeedValue.Value;
        if (!double.IsFinite(value) || value != Math.Truncate(value)) throw new ArgumentException("Escolhe uma velocidade inteira entre 1 e 20.");
        var desired = MousePrecision.ParseSpeed(value.ToString(CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
        var before = MousePrecision.ResolveSpeed().Read();
        if (!await Confirm("Alterar velocidade do ponteiro?", before + " → " + desired + " / 20.\nSó altera o ponteiro do Windows. O valor anterior fica no Histórico; jogos com Raw Input podem ignorar esta opção.")) return;
        optimizer.ApplyBatch([new(MousePrecision.SpeedKey, desired, before)]); RefreshMousePrecision(this, new());
        Notice("Velocidade aplicada e verificada. Podes repor o valor anterior.");
    });
    private async void RestoreMouseSpeed(object sender, RoutedEventArgs e) => await Run(() =>
    {
        var record = optimizer.History().FirstOrDefault(x => x.Key == MousePrecision.SpeedKey && x.State is "applied" or "pending");
        Notice(record is null ? "Não há velocidade anterior guardada para repor." : optimizer.Undo(record.Id));
        RefreshMousePrecision(this, new()); return Task.CompletedTask;
    });
    private bool extendedHardwareBusy;
    private async void RefreshExtendedHardware(object sender, RoutedEventArgs e)
    {
        if (extendedHardwareBusy) return;
        extendedHardwareBusy = true; ExtendedHardwareText.Text = "A consultar hardware e drivers deste PC…";
        try
        {
            var drivers = await Task.Run(HardwareEnvironment.GraphicsDrivers);
            if (closed) return;
            var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            ExtendedHardwareText.Text = model.CpuName + "\n" + model.GpuName + "\nRAM: " + model.RamText + "\n" +
                RuntimeInformation.OSDescription + "\n" + string.Join("\n", drivers) +
                $"\nMonitor desta janela: {display.OuterBounds.Width} × {display.OuterBounds.Height} píxeis.\n" +
                "Os valores pertencem a este computador. A torre e o portátil são detetados separadamente.";
        }
        catch (Exception ex) { if (!closed) ExtendedHardwareText.Text = "Parte da informação está indisponível: " + ex.Message; }
        finally { extendedHardwareBusy = false; }
    }
    private async void ShowVpnPlan(object sender, RoutedEventArgs e)
    {
        if (closed) return;
        var text = "Fase atual: integração independente do cliente oficial Cloudflare WARP. A NEXUS não redistribui o cliente nem aceita termos por ti. A ajuda da versão instalada tem de confirmar a CLI e cada pedido de rede exige confirmação. O modo apenas DNS não é apresentado como túnel.\n\n" +
            "A VPN própria Nexos/WireGuard continua indisponível. Não foram contratados servidores ou criadas subscrições.\n\n" +
            "Proposta mínima: um servidor Linux na Europa com WireGuard oficial e uma API autenticada para registar dispositivos e distribuir apenas configurações individuais. Os componentes Windows têm de ser assinados e instalados com autorização.\n\n" +
            "Orçamento inicial estimado: 7–10 €/mês para um único nó de testes com IPv4 e impostos. Alojamento da API, manutenção, tráfego excedente e expansão podem acrescentar custos. Não foi contratado qualquer serviço.\n\n" +
            "Antes de disponibilizar: ligação real, revogação de dispositivos, chaves protegidas, testes de DNS/IPv6, falhas e reposição da conectividade. Kill Switch e split tunneling ficam indisponíveis até validação específica.\n\n" +
            "Não prometemos anonimato absoluto, no logs, menor ping ou proteção verificada sem medições.";
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Nexos VPN — proposta", CloseButtonText = "Fechar", Content = new ScrollViewer { MaxHeight = 420, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } } };
        await dialog.ShowAsync();
    }
}
