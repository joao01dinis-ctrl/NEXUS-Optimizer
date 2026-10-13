using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Nexus.Optimization;

public static class UserAccessibility
{
    public const string StickyKeysKey = "sticky-keys";
    private const uint ChangedFlags = 0x1 | 0x4 | 0x8;
    private const uint ConfigurationFlags = 0x1FF;
    public static uint DisableStickyKeys(uint flags) => flags & ~ChangedFlags;
    public static string DisabledState(string current) => DisableStickyKeys(uint.Parse(current, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
    public static ISystemSetting Resolve() => new StickyKeysSetting();
    private sealed class StickyKeysSetting : ISystemSetting
    {
        public string Key => StickyKeysKey;
        public string Name => "Desativar Teclas de Aderência e atalho Shift";
        public string Read()
        {
            return (Get().Flags & ConfigurationFlags).ToString(CultureInfo.InvariantCulture);
        }
        public void Write(string serialized)
        {
            var flags = uint.Parse(serialized, CultureInfo.InvariantCulture);
            if (serialized != flags.ToString(CultureInfo.InvariantCulture) || (flags & ~ConfigurationFlags) != 0) throw new ArgumentException("Estado inválido.");
            var currentState = Get(); var current = currentState.Flags & ConfigurationFlags;
            if ((current & ~ChangedFlags) != (flags & ~ChangedFlags)) throw new InvalidOperationException("Outras preferências de acessibilidade mudaram; não foram substituídas.");
            var value = new StickyKeys { Size = 8, Flags = (currentState.Flags & ~ConfigurationFlags) | flags };
            Check(SystemParametersInfo(0x3B, 8, ref value, 3));
        }
        private static StickyKeys Get()
        {
            var value = new StickyKeys { Size = 8 };
            Check(SystemParametersInfo(0x3A, 8, ref value, 0)); return value;
        }
        private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct StickyKeys { public uint Size, Flags; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SystemParametersInfo(uint action, uint param, ref StickyKeys value, uint flags);
}
