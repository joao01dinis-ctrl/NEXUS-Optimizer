namespace Nexus.Core;

public sealed record Profile(string Name, int SampleSeconds, string Description);
public static class Profiles
{
    public static readonly Profile[] All = [new("Normal", 2, "Monitorização equilibrada"), new("Gaming", 5, "Menos atualizações do monitor durante jogos"), new("Trabalho", 3, "Monitorização para utilização diária")];
    public static Profile Get(string name) => All.SingleOrDefault(p => p.Name == name) ?? throw new ArgumentException("Perfil desconhecido", nameof(name));
}
public sealed record HistoryEntry(long Id, DateTimeOffset At, string Before, string After, bool Undone);
public sealed record HardwareInfo(string Cpu, string Gpu, double RamGb, IReadOnlyList<string> Disks, IReadOnlyList<string> Warnings);
public sealed record Sample(double? CpuPercent, double MemoryPercent, double UsedGb, double TotalGb, DateTimeOffset At);
public interface IHardwareService
{
    HardwareInfo Detect(); Sample ReadSample();
}
public interface IProfileStore
{
    string Current
    {
        get;
    }
    IReadOnlyList<HistoryEntry> History();
    void Apply(string profile);
    bool Undo();
}
public sealed record ModuleStatus(string Name, string Description, bool Available);
