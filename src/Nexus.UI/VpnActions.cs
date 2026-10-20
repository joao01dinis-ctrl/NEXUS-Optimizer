using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Diagnostics;
using Nexus.Network;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private readonly InstalledWarpClient warpClient = new();
    private CloudflareWarpProvider? warpProvider;
    private readonly CloudflareVpnProbe vpnProbe = new();
    private VpnMeasurements? vpnMeasurements;
    private VpnStatus? vpnStatus;
    private bool vpnReadBusy, vpnControlBusy, vpnMeasureBusy;
    private CancellationTokenSource? vpnMeasurementCancellation;
    private DateTimeOffset nextVpnRead;
    private int vpnControlRequests, vpnExternalSamples;

    private void InitializeVpn()
    {
        warpProvider = new(warpClient); vpnMeasurements = new(vpnProbe);
        Closed += (_, _) => { vpnMeasurementCancellation?.Cancel(); vpnProbe.Dispose(); };
    }
    private async void RefreshVpnStatus(object sender, RoutedEventArgs e) => await RefreshVpnStatusAsync();
    private async Task RefreshVpnStatusAsync(CancellationToken cancellation = default)
    {
        if (closed || vpnReadBusy || warpProvider is null) return;
        vpnReadBusy = true; UpdateVpnButtons();
        try
        {
            vpnStatus = await warpProvider.ReadAsync(cancellation.CanBeCanceled ? cancellation : backgroundCancellation.Token);
            if (closed) return;
            RenderVpnStatus(); nextVpnRead = DateTimeOffset.UtcNow.AddSeconds(15);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            AppLog.Error(error, "Consulta local WARP falhou");
            if (!closed) { vpnStatus = null; VpnStateText.Text = "Estado por confirmar"; VpnAvailabilityText.Text = "Não foi possível consultar o WARP. Abre a aplicação oficial e tenta atualizar novamente."; RenderVpnVerification(); }
        }
        finally { vpnReadBusy = false; if (!closed) UpdateVpnButtons(); }
    }
    private void RenderVpnStatus()
    {
        if (vpnStatus is not { } status) return;
        VpnStateText.Text = !status.Installed ? "WARP não instalado" : !status.Trusted ? "Instalação por validar" : status.State switch
        {
            VpnConnectionState.Connected => status.ReportsTunnel ? "Ligado no WARP" : "Ligado · modo sem túnel confirmado",
            VpnConnectionState.Disconnected => "Desligado",
            VpnConnectionState.Connecting => "A ligar…",
            VpnConnectionState.Disconnecting => "A desligar…",
            VpnConnectionState.Error => "O WARP precisa de atenção",
            _ => "Estado por confirmar"
        };
        VpnAvailabilityText.Text = status.Summary;
        if (status.Mode == WarpTrafficMode.TrafficOnly) VpnAvailabilityText.Text += " O modo atual encaminha tráfego; o DNS continua a ser gerido pelo Windows.";
        VpnCheckedAtText.Text = "Consulta local: " + status.CheckedAt.ToLocalTime().ToString("HH:mm:ss") +
            (status.Version is null ? "" : " · versão WARP " + status.Version) + ". Nenhum teste externo é feito por esta consulta.";
        VpnSetupPanel.Visibility = !status.Installed || !status.CanOpenClient || status.State == VpnConnectionState.Unknown ? Visibility.Visible : Visibility.Collapsed;
        RenderVpnVerification();
    }
    private void RenderVpnVerification()
    {
        var sample = vpnMeasurements?.Samples.LastOrDefault();
        VpnVerificationText.Text = VpnMeasurements.ConfirmsTestTraffic(vpnStatus, sample, DateTimeOffset.UtcNow)
            ? "O pedido HTTPS de teste passou pelo WARP. Isto não confirma a cobertura de todo o tráfego."
            : "Passagem de tráfego pelo WARP: não confirmada por uma medição recente.";
    }
    private void UpdateVpnButtons()
    {
        var busy = vpnReadBusy || vpnControlBusy || vpnMeasureBusy;
        VpnConnectButton.IsEnabled = !busy && vpnStatus?.CanConnect == true;
        VpnDisconnectButton.IsEnabled = !busy && vpnStatus?.CanDisconnect == true;
        VpnOpenClientButton.IsEnabled = !vpnControlBusy && vpnStatus?.CanOpenClient == true;
        VpnRefreshButton.IsEnabled = !vpnReadBusy && !vpnControlBusy;
        VpnMeasureButton.IsEnabled = VpnMonitorButton.IsEnabled = !vpnMeasureBusy && !vpnControlBusy;
        VpnStopMeasureButton.IsEnabled = vpnMeasureBusy;
        VpnProgress.Visibility = vpnControlBusy || vpnMeasureBusy ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task PollVpnStatusAsync()
    {
        if (currentPage != "vpn" || closed) return;
        RenderVpnVerification();
        if (!vpnControlBusy && !vpnMeasureBusy && DateTimeOffset.UtcNow >= nextVpnRead) await RefreshVpnStatusAsync();
    }
    private async void RequestVpnConnection(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string action } || vpnControlBusy || vpnMeasureBusy || warpProvider is null) return;
        var connect = action == "connect";
        var explanation = connect
            ? "O cliente oficial WARP vai encaminhar tráfego e DNS segundo a configuração atual da Cloudflare. Isto pode interromper ligações abertas e aumentar a latência. Os dados abrangidos passam a ser tratados pela Cloudflare segundo os termos que aceitaste no cliente oficial. A NEXUS não muda o modo, o protocolo ou a conta."
            : "O cliente oficial WARP vai desligar a ligação atual e gerir a reposição da rede. Ligações abertas podem ser interrompidas. O tráfego deixa de passar pelo túnel WARP.";
        if (!await Confirm(connect ? "Ligar Cloudflare WARP?" : "Desligar Cloudflare WARP?", explanation) || closed) return;
        vpnControlBusy = true; UpdateVpnButtons(); VpnRequestText.Text = "Pedido enviado. A aguardar confirmação do cliente oficial…";
        vpnMeasurements?.Clear(); RenderVpnVerification();
        try
        {
            vpnControlRequests++;
            vpnStatus = await warpProvider.RequestConnectionAsync(connect, confirmed: true, backgroundCancellation.Token);
            if (!closed) { VpnRequestText.Text = "O cliente confirmou o estado pedido. A NEXUS não verificou ainda a passagem de tráfego."; RenderVpnStatus(); }
            SaveActivity("VPN", connect ? "Pedido confirmado de ligação WARP; estado comunicado pelo cliente." : "Pedido confirmado de desligar WARP; estado comunicado pelo cliente.");
        }
        catch (OperationCanceledException) { if (!closed) VpnRequestText.Text = "A espera foi interrompida. Consulta o estado no WARP; o pedido pode ter sido aplicado."; }
        catch (Exception error) { if (!closed) VpnRequestText.Text = error.Message; AppLog.Error(error, "Pedido confirmado ao WARP falhou"); }
        finally
        {
            vpnControlBusy = false;
            if (!closed) { await RefreshVpnStatusAsync(); UpdateVpnButtons(); }
        }
    }
    private async void AssistWarpSetup(object sender, RoutedEventArgs e)
    {
        if (!await Confirm("Abrir a instalação oficial?", "Vais abrir a página oficial Cloudflare para Windows. Descarrega e executa o instalador nessa página, revê as permissões do Windows e aceita os termos pessoalmente na aplicação oficial. Depois volta à NEXUS e atualiza o estado.\n\nA NEXUS não instala, aceita termos, cria uma conta ou liga o WARP automaticamente.")) return;
        await OpenVpnPageAsync(InstalledWarpClient.SetupPage);
    }
    private async Task OpenVpnPageAsync(Uri page)
    {
        try { if (!await Windows.System.Launcher.LaunchUriAsync(page)) Notice("Não foi possível abrir a página oficial no navegador.", true); }
        catch (Exception error) { AppLog.Error(error, "Abrir página oficial WARP"); if (!closed) Notice("Abre a página oficial Cloudflare WARP para Windows no navegador.", true); }
    }
    private async void OpenWarpClient(object sender, RoutedEventArgs e)
    {
        try { await warpClient.OpenOfficialClientAsync(backgroundCancellation.Token); }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!closed) Notice(error.Message, true); }
    }
    private async void MeasureVpnConnection(object sender, RoutedEventArgs e)
    {
        if (vpnMeasureBusy || vpnControlBusy || vpnMeasurements is null) return;
        var monitor = sender is Button { Tag: "monitor" };
        var count = monitor ? 12 : 5;
        if (!await Confirm(monitor ? "Acompanhar a ligação durante um minuto?" : "Verificar a ligação agora?",
            "O teste contacta www.cloudflare.com por HTTPS e 1.1.1.1 por ICMP. A Cloudflare recebe o IP de origem e os dados normais destes pedidos. A NEXUS descarta o IP da resposta e não envia dados para a IA.\n\n" +
            (monitor ? "Até 12 amostras, durante um minuto. Podes parar a qualquer momento." : "Até cinco amostras. Não muda definições nem liga ou desliga a VPN."))) return;
        if (closed || currentPage != "vpn") return;
        vpnMeasurementCancellation = CancellationTokenSource.CreateLinkedTokenSource(backgroundCancellation.Token);
        vpnMeasurementCancellation.CancelAfter(TimeSpan.FromMinutes(1));
        var cancellation = vpnMeasurementCancellation;
        vpnMeasureBusy = true; vpnMeasurements.Clear(); UpdateVpnButtons();
        try
        {
            for (var i = 0; i < count; i++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (currentPage != "vpn" || closed) break;
                VpnMeasurementProgressText.Text = $"A medir amostra {i + 1} de {count}…";
                await vpnMeasurements.SampleAsync(confirmed: true, cancellation.Token); vpnExternalSamples++;
                await RefreshVpnStatusAsync(cancellation.Token);
                if (closed) return;
                VpnMeasurementText.Text = vpnMeasurements.Summary;
                RenderVpnVerification();
                if (i + 1 < count) await Task.Delay(monitor ? 5000 : 1000, cancellation.Token);
            }
            if (!closed) VpnMeasurementProgressText.Text = "Medição terminada. Os resultados descrevem apenas as amostras recolhidas.";
        }
        catch (OperationCanceledException) { if (!closed) VpnMeasurementProgressText.Text = "A medição parou. A ligação WARP não foi alterada pela NEXUS."; }
        catch (Exception error) { if (!closed) VpnMeasurementProgressText.Text = "Não foi possível terminar a medição. Podes atualizar o estado ou abrir o WARP."; AppLog.Error(error, "Medição VPN falhou"); }
        finally
        {
            if (ReferenceEquals(vpnMeasurementCancellation, cancellation)) vpnMeasurementCancellation = null;
            cancellation.Dispose(); vpnMeasureBusy = false; if (!closed) UpdateVpnButtons();
        }
    }
    private void StopVpnMeasurement(object sender, RoutedEventArgs e) => vpnMeasurementCancellation?.Cancel();
}
