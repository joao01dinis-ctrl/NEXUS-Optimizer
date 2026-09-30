using Nexus.Core;
namespace Nexus.Benchmark;

public static class BenchmarkModule
{
    public static ModuleStatus Status => new("Benchmark", "Benchmarks planeados; nenhum teste de carga automático.", false);
}
