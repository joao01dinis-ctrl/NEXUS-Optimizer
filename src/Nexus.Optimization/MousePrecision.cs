using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Nexus.Optimization;

public static class MousePrecision
{
    public const string AccelerationKey = "mouse-acceleration";
    public const string SpeedKey = "mouse-speed";
    public static ISystemSetting Resolve() => new AccelerationSetting();
    public static ISystemSetting ResolveSpeed() => new SpeedSetting();
    public static int ParseSpeed(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var speed) || speed is < 1 or > 20)
            throw new ArgumentException("Escolhe uma velocidade inteira entre 1 e 20.");
        return speed;
    }
    private sealed class SpeedSetting : ISystemSetting
    {
        public string Key => SpeedKey;
        public string Name => "Velocidade do ponteiro do Windows";
        public string Read()
        {
            var result = new int[1];
            if (!SystemParametersInfo(0x0070, 0, result, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return ParseSpeed(result[0].ToString(CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
        }
        public void Write(string value)
        {
            if (!SetSystemParametersInfo(0x0071, 0, (nint)ParseSpeed(value), 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
    public static double EquivalentSensitivity(double originalDpi, double originalSensitivity, double newDpi)
    {
        if (!double.IsFinite(originalDpi) || !double.IsFinite(newDpi) || !double.IsFinite(originalSensitivity) ||
            originalDpi is < 50 or > 100000 || newDpi is < 50 or > 100000 || originalSensitivity is <= 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(originalDpi), "Introduz DPI entre 50 e 100000 e sensibilidade positiva até 1000.");
        return originalDpi * originalSensitivity / newDpi;
    }
    public static int[] ParseState(string state)
    {
        var parts = state.Split(',');
        if (parts.Length != 3 || parts.Any(p => !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            throw new ArgumentException("Estado do rato inválido.");
        var values = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        if (values[0] < 0 || values[1] < 0 || values[2] is < 0 or > 2)
            throw new ArgumentException("Estado do rato fora dos limites suportados.");
        return values;
    }
    private sealed class AccelerationSetting : ISystemSetting
    {
        public string Key => AccelerationKey;
        public string Name => "Aceleração do ponteiro do Windows";
        public string Read()
        {
            var values = new int[3];
            if (!SystemParametersInfo(0x0003, 0, values, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
        }
        public void Write(string value)
        {
            var values = ParseState(value);
            if (!SystemParametersInfo(0x0004, 0, values, 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, [In, Out] int[] values, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemParametersInfo(uint action, uint parameter, nint value, uint flags);
}
