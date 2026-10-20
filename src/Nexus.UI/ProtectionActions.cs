using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Diagnostics;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private readonly WindowsProtection protection = new();
    private ProtectionStatus? protectionStatus;
    private bool protectionReadBusy, protectionActionBusy;
    private int protectionActionCount;

    private async void RefreshProtection(object sender, RoutedEventArgs e) => await RefreshProtectionAsync();
    private async Task RefreshProtectionAsync()
    {
        if (protectionReadBusy || closed) return;
        protectionReadBusy = true; ProtectionRefreshButton.IsEnabled = false;
        try
        {
            protectionStatus = await protection.ReadAsync(backgroundCancellation.Token);
            if (closed) return;
            var status = protectionStatus; var defender = status.Defender;
            ProtectionState.Message = status.Summary;
            ProtectionState.Severity = status.WindowsHealth is AntivirusHealth.Poor or AntivirusHealth.Snoozed ||
                defender is { Active: true, RealTimeEnabled: false } or { Active: true, SignaturesOutOfDate: true }
                ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
            string Flag(bool? value) => value is true ? "Ativa" : value is false ? "Desligada" : "Indisponível";
            string Date(DateTimeOffset? value) => value?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Data não fornecida";
            ProtectionDetails.Text = "Modo do Defender: " + (defender?.Mode ?? "Indisponível") +
                "\nProteção em tempo real: " + Flag(defender?.RealTimeEnabled) +
                "\nProteção contra alterações: " + Flag(defender?.TamperProtected) +
                "\nDefinições: " + (defender?.SignatureVersion ?? "Indisponíveis") +
                "\nÚltima atualização: " + Date(defender?.SignaturesUpdated) +
                "\nDefinições desatualizadas: " + (defender?.SignaturesOutOfDate is true ? "Sim" : defender?.SignaturesOutOfDate is false ? "O Defender indica que não" : "Indisponível");
            ProtectionScanHistory.Text = "Última análise rápida terminada: " + Date(defender?.QuickScanEnded) +
                "\nÚltima análise completa terminada: " + Date(defender?.FullScanEnded) +
                "\nEstas datas não representam uma confirmação de ausência de ameaças. Consulta as deteções na Segurança do Windows.";
            ProtectionCheckedAt.Text = "Consulta local às " + status.CheckedAt.ToString("HH:mm:ss") + ". " + (status.ReadError ?? "A consulta não altera definições de segurança.");
            ProtectionPermissions.Text = status.CanRequestAction
                ? "O Defender está ativo e esta NEXUS tem direitos para pedir análises ou atualizações. Cada pedido exige confirmação."
                : !status.Administrator
                    ? "Para pedir análises nesta página, abre a NEXUS como administrador. Podes usar já a Segurança do Windows pelo botão abaixo."
                    : "O Defender não foi confirmado como antivírus ativo. Usa a Segurança do Windows para consultar e utilizar o fornecedor atual.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppLog.Error(ex, "Consultar proteção"); if (!closed) ProtectionCheckedAt.Text = "Não foi possível consultar a proteção. Usa a Segurança do Windows."; }
        finally { protectionReadBusy = false; if (!closed) { ProtectionRefreshButton.IsEnabled = !protectionActionBusy; UpdateProtectionButtons(); } }
    }
    private void UpdateProtectionButtons()
    {
        var allowed = protectionStatus?.CanRequestAction == true && !protectionActionBusy && !protectionReadBusy;
        ProtectionQuickButton.IsEnabled = ProtectionFullButton.IsEnabled = ProtectionUpdateButton.IsEnabled = allowed;
    }
    private async void RequestProtectionAction(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !Enum.TryParse<ProtectionAction>(tag, out var action) || !Enum.IsDefined(action)) return;
        await Run(async () =>
        {
            if (protectionActionBusy) return;
            if (sessions.Active is not null || gameAutomation.Enabled) throw new InvalidOperationException("Termina a sessão e pausa o Game Boost antes de pedir uma operação ao Defender pela NEXUS. A proteção do Windows continua independente.");
            var title = action switch { ProtectionAction.QuickScan => "Iniciar análise rápida?", ProtectionAction.FullScan => "Iniciar análise completa?", _ => "Procurar atualizações do Defender?" };
            var text = action == ProtectionAction.UpdateSignatures
                ? "O Microsoft Defender vai procurar definições através das fontes configuradas no Windows. Pode utilizar a Internet. A NEXUS não muda as fontes ou políticas de atualização."
                : "O Microsoft Defender vai analisar o PC e pode colocar ameaças em quarentena automaticamente, segundo as políticas atuais do Windows. Pode utilizar CPU e disco; uma análise completa pode demorar bastante.\n\nA NEXUS não elimina ou restaura ameaças e não apresenta uma percentagem inventada. Se fechares a NEXUS, uma análise já iniciada pode continuar no Windows.";
            var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, PrimaryButtonText = "Confirmar pedido", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || closed) return;
            protectionActionBusy = true; protectionActionCount++; UpdateProtectionButtons(); ProtectionRefreshButton.IsEnabled = false;
            ProtectionProgress.Visibility = Visibility.Visible;
            ProtectionRequestStatus.Text = "A aguardar o Microsoft Defender. Consulta o progresso na Segurança do Windows; a NEXUS não mede a percentagem da análise.";
            SaveActivity("Proteção", "Pedido confirmado: " + action + ". A execução e as deteções pertencem ao Microsoft Defender.");
            try
            {
                await protection.RequestAsync(action, confirmed: true, backgroundCancellation.Token);
                if (!closed) ProtectionRequestStatus.Text = "O pedido terminou sem erro comunicado. Consulta o histórico e as deteções na Segurança do Windows. Isto não confirma que o PC esteja livre de malware.";
                SaveActivity("Proteção", "Pedido ao Defender terminou sem erro comunicado: " + action + ".");
            }
            catch (OperationCanceledException) { if (!closed) ProtectionRequestStatus.Text = "A espera foi interrompida. Uma análise já iniciada pode continuar na Segurança do Windows."; }
            catch (Exception ex) { SaveActivity("Proteção", "O pedido ao Defender falhou ou deixou de ser acompanhado: " + action + "."); if (!closed) ProtectionRequestStatus.Text = ex.Message; throw; }
            finally
            {
                protectionActionBusy = false;
                if (!closed) { ProtectionProgress.Visibility = Visibility.Collapsed; await RefreshProtectionAsync(); UpdateProtectionButtons(); }
            }
        });
    }
    private async void OpenWindowsProtection(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:windowsdefender")))
                Notice("Não foi possível abrir a Segurança do Windows. Procura-a no menu Iniciar.", true);
        }
        catch (Exception ex) { AppLog.Error(ex, "Abrir Segurança do Windows"); Notice("Abre Segurança do Windows pelo menu Iniciar.", true); }
    }
}
