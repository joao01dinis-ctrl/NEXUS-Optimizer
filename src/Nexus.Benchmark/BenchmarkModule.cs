using Nexus.Core;
namespace Nexus.Benchmark;

public static class BenchmarkModule
{
    public static ModuleStatus Status => new("Benchmark", "Teste curto de SHA-256 e cópia de memória, escolhido pelo utilizador; não mede FPS.", true);
}
