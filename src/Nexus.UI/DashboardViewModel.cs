using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Core;
using Nexus.Hardware;
using Nexus.Storage;
using Nexus.Optimization;
using Nexus.Restore;
using Nexus.Service;
using Nexus.Diagnostics;
namespace Nexus.UI;

public partial class DashboardViewModel : ObservableObject
{
    private readonly WindowsHardwareService hardware = new();
    private readonly ProfileStore store;
    private readonly ProfileManager profiles;
    private readonly UndoManager undo;
    private readonly MonitoringService monitor;
    public string CpuName { get; set => SetProperty(ref field, value); } = "A detetar…";
    public string GpuName { get; set => SetProperty(ref field, value); } = "A detetar…";
    public string RamText { get; set => SetProperty(ref field, value); } = "—";
    public string CpuText { get; set => SetProperty(ref field, value); } = "A recolher…";
    public string MemoryText { get; set => SetProperty(ref field, value); } = "—";
    public double CpuPercent { get; set => SetProperty(ref field, value); }
    public double MemoryPercent { get; set => SetProperty(ref field, value); }
    public string Disks { get; set => SetProperty(ref field, value); } = "A detetar…";
    public string ActiveProfile { get; set => SetProperty(ref field, value); } = "Normal";
    public string SelectedProfile { get; set => SetProperty(ref field, value); } = "Normal";
    public string History { get; set => SetProperty(ref field, value); } = "Ainda sem alterações.";
    public string Status { get; set => SetProperty(ref field, value); } = "A iniciar monitorização…";
    public IRelayCommand ApplyProfileCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public string[] ProfileNames { get; } = Profiles.All.Select(p => p.Name).ToArray();
    public int SampleSeconds => profiles.Current.SampleSeconds;
    public DashboardViewModel(string root)
    {
        ApplyProfileCommand = new RelayCommand(ApplyProfile);
        UndoCommand = new RelayCommand(Undo);
        store = new(Path.Combine(root, "nexus.db"));
        profiles = new(store);
        undo = new(store);
        monitor = new(hardware);
        Refresh();
        SelectedProfile = ActiveProfile;
    }
    public async Task InitializeAsync()
    {
        try
        {
            var h = await Task.Run(hardware.Detect);
            CpuName = h.Cpu;
            GpuName = h.Gpu;
            RamText = $"{h.RamGb:F1} GB instalados";
            Disks = string.Join("\n", h.Disks);
            foreach (var w in h.Warnings)
                AppLog.Info(w);
        }
        catch (Exception e) { AppLog.Error(e, "Deteção de hardware"); Status = "Algum hardware não pôde ser identificado."; }
    }
    public async Task TickAsync()
    {
        try
        {
            var s = await monitor.SampleAsync();
            CpuPercent = s.CpuPercent ?? 0;
            CpuText = s.CpuPercent is double p ? $"{p:F0}%" : "A recolher…";
            MemoryPercent = s.MemoryPercent;
            MemoryText = $"{s.UsedGb:F1} / {s.TotalGb:F1} GB";
            Status = $"Atualizado às {s.At:HH:mm:ss} · intervalo {SampleSeconds}s";
        }
        catch (Exception e) { Status = "Monitorização indisponível; consultar logs."; AppLog.Error(e, "Monitorização"); }
    }
    private void ApplyProfile()
    {
        try
        {
            profiles.Apply(SelectedProfile);
            Refresh();
            Status = $"Perfil {ActiveProfile} guardado. Apenas preferências da aplicação.";
            AppLog.Info($"Perfil: {ActiveProfile}");
        }
        catch (Exception e) { Status = "Não foi possível guardar o perfil."; AppLog.Error(e, "Aplicar perfil"); }
    }
    private void Undo()
    {
        try
        {
            var changed = undo.UndoLast();
            Refresh();
            SelectedProfile = ActiveProfile;
            Status = changed ? "Última alteração desfeita." : "Não há alterações para desfazer.";
            AppLog.Info(Status);
        }
        catch (Exception e) { Status = "Não foi possível desfazer."; AppLog.Error(e, "Undo"); }
    }
    private void Refresh()
    {
        ActiveProfile = store.Current;
        var entries = store.History();
        History = entries.Count == 0 ? "Ainda sem alterações." : string.Join("\n", entries.Select(h => $"{h.At.ToLocalTime():dd/MM HH:mm}   {h.Before} → {h.After}{(h.Undone ? " · desfeito" : "")}"));
    }
}
