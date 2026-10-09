namespace Nexus.Storage;

public sealed record CleanupLocation(string Name, string Path, string Description)
{
    public override string ToString() => Name;
}
public static class CleanupLocations
{
    public static IReadOnlyList<CleanupLocation> Read()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var all = new[] {
            new CleanupLocation("Temporários do utilizador", System.IO.Path.GetTempPath(), "Ficheiros sem escrita/acesso há 7 dias. Não remove pastas, ligações ou ficheiros recentes."),
            new CleanupLocation("Cache de shaders DirectX", System.IO.Path.Combine(local, "D3DSCache"), "Só ficheiros antigos. Os jogos podem recompilar shaders e apresentar pausas; não é um aumento de FPS."),
            new CleanupLocation("Cache web do Epic Launcher", System.IO.Path.Combine(local, "EpicGamesLauncher", "Saved", "webcache"), "Fecha o launcher. A cache pode voltar a ser criada e as sessões web podem ter de ser iniciadas novamente."),
            new CleanupLocation("Recent: atalhos e histórico", System.IO.Path.Combine(roaming, "Microsoft", "Windows", "Recent"), "Revê atalhos e registos antigos do histórico recente, incluindo listas de acesso. Não remove os documentos originais; pode retirar entradas do histórico.")
        };
        return all.Where(x => Directory.Exists(x.Path)).ToArray();
    }
}
