using Nexus.Core;
namespace Nexus.Network;

public static class NetworkModule
{
    public static ModuleStatus Status => new("Network", "Diagnóstico de rede planeado; sem alterações DNS/TCP.", false);
}
