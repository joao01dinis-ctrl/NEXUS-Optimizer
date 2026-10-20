using Nexus.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Nexus.Hardware;

public sealed class WindowsMouseProvider : IMouseProvider
{
    public string Name=>"Windows Raw Input / HID (leitura)";
    public IReadOnlyList<MouseDevice> Detect()
    {
        if(!OperatingSystem.IsWindows())return [];
        uint count=0;var size=(uint)Marshal.SizeOf<RawDevice>();
        if(GetRawInputDeviceList(null,ref count,size)==uint.MaxValue)throw new Win32Exception();
        if(count>1024)throw new InvalidOperationException("Número de dispositivos inesperado.");
        // Devices can arrive between the size query and enumeration. Retry a bounded number of times.
        for(var attempt=0;attempt<3;attempt++)
        {
            var devices=new RawDevice[count];var actual=count;
            var read=GetRawInputDeviceList(devices,ref actual,size);
            if(read==uint.MaxValue){count=actual;if(count>1024)break;continue;}
            var result=new List<MouseDevice>();
            foreach(var d in devices.Take((int)read).Where(d=>d.Type==0))
            {
                uint chars=0;
                if(GetRawInputDeviceName(d.Handle,0x20000007,null,ref chars)==uint.MaxValue || chars is 0 or >8192)continue;
                var text=new StringBuilder((int)chars);
                if(GetRawInputDeviceName(d.Handle,0x20000007,text,ref chars)==uint.MaxValue)continue;
                var path=text.ToString();
                var vendor=Regex.Match(path,@"VID_([0-9A-F]{4})",RegexOptions.IgnoreCase);
                var product=Regex.Match(path,@"PID_([0-9A-F]{4})",RegexOptions.IgnoreCase);
                var name="Rato / apontador HID";
                // Zero access, shared handle, strings only. Never sends HID feature/output reports.
                using(var handle=CreateFile(path,0,3,nint.Zero,3,0,nint.Zero))
                {
                    if(!handle.IsInvalid)
                    {
                        var buffer=new byte[512];
                        if(HidD_GetProductString(handle,buffer,buffer.Length)) { var candidate=Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();if(candidate.Length>0)name=candidate; }
                        if(HidD_GetManufacturerString(handle,buffer=new byte[512],buffer.Length)) { var maker=Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();if(maker.Length>0 && !name.Contains(maker,StringComparison.OrdinalIgnoreCase))name=maker+" "+name; }
                    }
                }
                var connection=path.Contains("BTH",StringComparison.OrdinalIgnoreCase)?"Bluetooth indicado pelo caminho PnP":"HID; cabo/recetor não distinguível";
                result.Add(new(path,name,vendor.Success?vendor.Groups[1].Value.ToUpperInvariant():null,product.Success?product.Groups[1].Value.ToUpperInvariant():null,connection,Name));
            }
            return result;
        }
        throw new InvalidOperationException("Os dispositivos mudaram durante a leitura. Atualiza a lista.");
    }
    public IReadOnlyList<MouseCapability> Capabilities(MouseDevice device)=>new[] {
        new MouseCapability("Identidade HID e eventos",MouseAccess.ReadOnly,"Informação do Windows; nome/modelo podem ser genéricos ou representar um recetor."),
        new MouseCapability("DPI / polling configurado",MouseAccess.OfficialSoftware,"Configurar no software oficial. O teste mede eventos entregues, não lê o polling configurado."),
        new MouseCapability("Motion Sync, Angle Snapping, Ripple, LOD, Debounce, energia",MouseAccess.OfficialSoftware,"Só no fabricante, se o modelo e o modo de ligação suportarem."),
        new MouseCapability("Bateria / firmware / sensor",MouseAccess.Unsupported,"Não expostos pelo fornecedor genérico; valores permanecem desconhecidos.") };
    public static string DevicePath(nint handle)
    {uint n=0;GetRawInputDeviceName(handle,0x20000007,null,ref n);if(n is 0 or >8192)return "";var s=new StringBuilder((int)n);return GetRawInputDeviceName(handle,0x20000007,s,ref n)==uint.MaxValue?"":s.ToString();}
    [StructLayout(LayoutKind.Sequential)] private struct RawDevice { public nint Handle;public uint Type; }
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRawInputDeviceList([Out] RawDevice[]? list,ref uint count,uint size);
    [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern uint GetRawInputDeviceName(nint handle,uint command,StringBuilder? data,ref uint size);
    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateFile(string name,uint access,uint share,nint security,uint creation,uint flags,nint template);
    [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool HidD_GetProductString(SafeFileHandle device,[Out]byte[] buffer,int length);
    [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool HidD_GetManufacturerString(SafeFileHandle device,[Out]byte[] buffer,int length);
}

// Foreground-only mouse capture owned by one window. No keyboard, hooks, injection or INPUTSINK.
public sealed class MouseRawCapture : IDisposable
{
    private readonly nint hwnd;
    private readonly Subclass callback;
    private readonly string devicePath;
    private readonly Stopwatch watch=new();
    private readonly List<MouseSample> samples=new(16000);
    private readonly nint buffer=Marshal.AllocHGlobal(128);
    private bool disposed;
    private nint selectedHandle;
    private bool registered,subclassed;
    public bool Active {get;private set;}
    public string? StopReason {get;private set;}
    public int Discarded {get;private set;}
    public ushort ObservedButtons {get;private set;}
    public int WheelEvents {get;private set;}
    public double ElapsedMs=>watch.Elapsed.TotalMilliseconds;
    public IReadOnlyList<MouseSample> Samples=>samples;
    public MouseRawCapture(nint hwnd,string path)
    {this.hwnd=hwnd;devicePath=path;callback=WindowMessage;}
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(Active || watch.IsRunning || samples.Count!=0)throw new InvalidOperationException("Cria uma nova sessão de captura.");
        if(hwnd==0 || GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Mantém a janela NEXUS em primeiro plano.");
        // Resolve selected identity once. The native callback does not allocate or re-query device strings per event.
        selectedHandle=FindHandle(devicePath);
        if(selectedHandle==0)throw new InvalidOperationException("O dispositivo já não está disponível.");
        if(!SetWindowSubclass(hwnd,callback,0x4E584D,0))throw new Win32Exception();subclassed=true;
        if(!RegisterRawInputDevices([new(){Page=1,Usage=2,Flags=0x2000,Target=hwnd}],1,(uint)Marshal.SizeOf<Registration>()))
        {RemoveWindowSubclass(hwnd,callback,0x4E584D);subclassed=false;throw new Win32Exception();}
        registered=true;Active=true;watch.Start();
    }
    private static nint FindHandle(string path)
    {
        uint count=0;var size=(uint)Marshal.SizeOf<Device>();if(GetRawInputDeviceList(null,ref count,size)==uint.MaxValue || count>1024)return 0;
        var list=new Device[count];var read=GetRawInputDeviceList(list,ref count,size);if(read==uint.MaxValue)return 0;
        return list.Take((int)read).FirstOrDefault(d=>d.Type==0 && WindowsMouseProvider.DevicePath(d.Handle).Equals(path,StringComparison.OrdinalIgnoreCase)).Handle;
    }
    private nint WindowMessage(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data)
    {
        try
        {
            if(Active && message==0x00FF)
            {
                if(watch.ElapsedMilliseconds>=15000){Stop("Tempo de 15 segundos concluído.");}
                else if(GetForegroundWindow()!=hwnd){Stop("Janela sem foco; captura interrompida.");}
                else
                {
                    uint size=128;var read=GetRawInputData(lParam,0x10000003,buffer,ref size,24);
                    if(read==uint.MaxValue || read<48){Discarded++;}
                    else if(Marshal.ReadInt32(buffer)==0 && Marshal.ReadIntPtr(buffer,8)==selectedHandle)
                    {
                        if(samples.Count>=MouseAnalyzer.MaximumSamples){Discarded++;Stop("Limite de amostras atingido.");}
                        else
                        {
                            var buttons=unchecked((ushort)Marshal.ReadInt16(buffer,28));
                            var wheel=(buttons&0x0C00)!=0?Marshal.ReadInt16(buffer,30):(short)0;
                            ObservedButtons|=buttons;if(wheel!=0)WheelEvents++;
                            samples.Add(new(watch.Elapsed.TotalMilliseconds,Marshal.ReadInt32(buffer,36),Marshal.ReadInt32(buffer,40),buttons,wheel,(Marshal.ReadInt16(buffer,24)&1)!=0));
                        }
                    }
                }
            }
            if(Active && message==0x00FE && wParam==2 && lParam==selectedHandle)Stop("Dispositivo desligado durante o teste.");
            if(Active && message==0x0006 && (wParam&0xFFFF)==0)Stop("Janela sem foco; captura interrompida.");
        }
        catch(Exception){Discarded++;Stop("Erro na leitura; captura interrompida.");}
        return DefSubclassProc(window,message,wParam,lParam);
    }
    public void Stop(string reason="Teste parado pelo utilizador.")
    {
        Active=false;watch.Stop();StopReason=reason;
        if(registered)
        {if(RegisterRawInputDevices([new(){Page=1,Usage=2,Flags=1,Target=0}],1,(uint)Marshal.SizeOf<Registration>()))registered=false;else StopReason+=" Não foi possível confirmar a remoção do registo Raw Input; fecha a janela da NEXUS se o problema persistir.";}
    }
    public static bool IsRegisteredForWindow(nint window)
    {
        uint count=0;var size=(uint)Marshal.SizeOf<Registration>();
        var queried=GetRegisteredRawInputDevices(null,ref count,size);
        if((queried==uint.MaxValue && Marshal.GetLastWin32Error()!=122) || count>1024)throw new Win32Exception();
        if(count==0)return false;var entries=new Registration[count];var read=GetRegisteredRawInputDevices(entries,ref count,size);
        if(read==uint.MaxValue)throw new Win32Exception();
        return entries.Take((int)read).Any(e=>e.Page==1 && e.Usage==2 && e.Target==window);
    }
    public void Dispose()
    {if(disposed)return;Stop(StopReason??"Captura encerrada.");if(subclassed)RemoveWindowSubclass(hwnd,callback,0x4E584D);Marshal.FreeHGlobal(buffer);disposed=true;GC.KeepAlive(callback);}
    [StructLayout(LayoutKind.Sequential)]private struct Device{public nint Handle;public uint Type;}
    [StructLayout(LayoutKind.Sequential)]private struct Registration{public ushort Page,Usage;public uint Flags;public nint Target;}
    private delegate nint Subclass(nint hwnd,uint msg,nuint wp,nint lp,nuint id,nuint data);
    [DllImport("comctl32.dll")] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowSubclass(nint hwnd,Subclass callback,nuint id,nuint data);
    [DllImport("comctl32.dll")] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool RemoveWindowSubclass(nint hwnd,Subclass callback,nuint id);
    [DllImport("comctl32.dll")]private static extern nint DefSubclassProc(nint hwnd,uint msg,nuint wp,nint lp);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool RegisterRawInputDevices(Registration[] devices,uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRawInputData(nint handle,uint command,nint data,ref uint size,uint headerSize);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRawInputDeviceList([Out] Device[]? list,ref uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRegisteredRawInputDevices([Out] Registration[]? entries,ref uint count,uint size);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
}
