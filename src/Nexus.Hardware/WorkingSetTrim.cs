using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nexus.Hardware;

public static class WorkingSetTrim
{
    // Not a standby purge: only a reviewed, accessible background app in the user's session.
    public static long Trim(MemoryProcess chosen, IReadOnlySet<int> protectedProcesses)
    {
        if (chosen.Started == 0 || protectedProcesses.Contains(chosen.Id)) throw new InvalidOperationException("Esta aplicação está protegida ou a seleção não é válida.");
        using var self = Process.GetCurrentProcess(); using var p = Process.GetProcessById(chosen.Id);
        _ = p.Handle; // Pin this process instance before validating its start time.
        if (p.Id == self.Id || p.SessionId != self.SessionId || p.StartTime.ToUniversalTime().Ticks != chosen.Started || p.HasExited)
            throw new InvalidOperationException("A aplicação terminou ou a seleção mudou. Atualiza a lista.");
        GetWindowThreadProcessId(GetForegroundWindow(), out var foreground);
        if (p.Id == foreground || new[] { "explorer", "dwm", "csrss", "winlogon", "sihost", "Nexus.UI", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost" }.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("A aplicação em primeiro plano e componentes do Windows não são elegíveis.");
        var before = p.WorkingSet64;
        if (!EmptyWorkingSet(p.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
        p.Refresh(); return before - p.WorkingSet64;
    }
    [DllImport("kernel32.dll", EntryPoint = "K32EmptyWorkingSet", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EmptyWorkingSet(nint process);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
}
