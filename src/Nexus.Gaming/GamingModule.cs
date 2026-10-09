using Nexus.Core;
namespace Nexus.Gaming;

public static class GamingModule
{
    public static ModuleStatus Status => new("Gaming", "Sessões com prioridade, energia e restauro; FPS/PresentMon não implementados.", true);
}
