using Nexus.Core;
namespace Nexus.Network;

public static class NetworkModule
{
    public static ModuleStatus Status => new("Network", "Comparação de resposta DNS; sem alterações DNS/TCP.", true);
}
