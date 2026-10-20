using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private string currentPage = "overview";
    private readonly HashSet<string> favoritePages = new(StringComparer.Ordinal);
    private void ToggleFavoritePage(object sender, RoutedEventArgs e)
    {
        if (!favoritePages.Remove(currentPage)) favoritePages.Add(currentPage);
        UpdateFavoriteNavigation(); SaveUiPreferences();
    }
    private void UpdateFavoriteNavigation()
    {
        FavoritePageButton.Content = favoritePages.Contains(currentPage) ? "Retirar favorito" : "Guardar favorito";
        FavoriteNavigation.Children.Clear();
        foreach (var page in favoritePages.Order())
        {
            var target = SearchTargets.FirstOrDefault(p => p.Page == page);
            if (target is null) continue;
            var button = new Button { Content = new TextBlock { Text = "★ " + target.Title, TextWrapping = TextWrapping.Wrap, MaxLines = 2, FontSize = 11 },
                Tag = page, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += Navigate; FavoriteNavigation.Children.Add(button);
        }
        FavoriteNavigation.Visibility = FavoriteNavigation.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private async void ShowQuickGuide(object sender, RoutedEventArgs e)
    {
        if (closed) return;
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, Title = "Primeiros passos", CloseButtonText = "Fechar",
            Content = new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                "1. No Dashboard, escolhe Otimização rápida ou Analisar sistema. A análise não aplica alterações.\n\n" +
                "2. Revê as propostas e confirma apenas as que queres. Os valores anteriores ficam no Histórico.\n\n" +
                "3. Em Os meus jogos, atualiza a procura automática e guarda um perfil por jogo. Abre o jogo pelo launcher habitual.\n\n" +
                "4. Otimizar este jogo cria uma sessão temporária. Mantém a NEXUS aberta; ao terminar, as preferências são repostas.\n\n" +
                "5. Game Boost automático é opcional. Usa Histórico ou Repor sessão para recuperar configurações.\n\n" +
                "Nexos VPN integra o cliente oficial Cloudflare WARP instalado por ti. Ligar, desligar e testar exigem confirmação. A VPN própria WireGuard permanece indisponível. As medições pertencem ao PC onde a NEXUS está a correr." }
        };
        await dialog.ShowAsync();
    }
}
