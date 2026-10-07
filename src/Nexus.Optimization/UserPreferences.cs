using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Nexus.Optimization;

public sealed record UserAdjustment(string Key, string Name, string Description, string Value);

// Supported user preferences, deliberately excluding security and service policies.
public static class UserPreferences
{
    public static IReadOnlyList<UserAdjustment> All { get; } = [
        new("menu-animation", "Reduzir animações dos menus", "Mostra os menus diretamente nas aplicações que respeitam esta preferência.", "0"),
        new("tooltip-animation", "Reduzir animações das dicas", "Mostra dicas de contexto sem animação.", "0"),
        new("combo-animation", "Reduzir animações das listas de escolha", "Abre listas de escolha sem o efeito de deslizar.", "0"),
        new("list-smooth", "Reduzir deslocamento animado das listas", "Desativa a deslocação suave em listas compatíveis.", "0"),
        new("selection-fade", "Desativar desaparecimento gradual da seleção", "Remove o efeito visual após escolher um item de menu.", "0"),
        new("minimize-animation", "Reduzir animações ao minimizar", "Minimiza e repõe janelas sem animação.", "0"),
        new("drag-full", "Arrastar só o contorno da janela", "Reduz o conteúdo desenhado enquanto arrastas uma janela; muda a experiência visual.", "0"),
        new("keyboard-delay", "Encurtar repetição do teclado", "Repete uma tecla premida após cerca de 250 ms. Não reduz a latência do primeiro toque nem o ping.", "0")
    ];
    private static readonly Dictionary<string, (uint Get, uint Set, bool UiParam, uint Max)> Actions = new()
    {
        ["menu-animation"] = (0x1002, 0x1003, false, 1),
        ["tooltip-animation"] = (0x1016, 0x1017, false, 1),
        ["combo-animation"] = (0x1004, 0x1005, false, 1),
        ["list-smooth"] = (0x1006, 0x1007, false, 1),
        ["selection-fade"] = (0x1014, 0x1015, false, 1),
        ["drag-full"] = (0x26, 0x25, true, 1),
        ["keyboard-delay"] = (0x16, 0x17, true, 3)
    };
    public static bool Contains(string key) => All.Any(x => x.Key == key);
    public static ISystemSetting Resolve(string key) => new Preference(key);
    private sealed class Preference(string key) : ISystemSetting
    {
        public string Key => key;
        public string Name => All.Single(x => x.Key == key).Name;
        public string Read()
        {
            if (key == "minimize-animation")
            {
                var a = new Animation { Size = 8 };
                Check(GetAnimation(0x48, 8, ref a, 0));
                return a.Enabled.ToString(CultureInfo.InvariantCulture);
            }
            var action = Actions[key]; uint value = 0;
            Check(Get(action.Get, 0, ref value, 0));
            return value.ToString(CultureInfo.InvariantCulture);
        }
        public void Write(string value)
        {
            var number = uint.Parse(value, CultureInfo.InvariantCulture);
            if (key == "minimize-animation")
            {
                if (number > 1) throw new ArgumentException("Valor inválido.");
                var a = new Animation { Size = 8, Enabled = (int)number };
                Check(GetAnimation(0x49, 8, ref a, 3)); return;
            }
            var action = Actions[key];
            if (number > action.Max) throw new ArgumentException("Valor inválido.");
            Check(Set(action.Set, action.UiParam ? number : 0, action.UiParam ? 0 : (nint)number, 3));
        }
    }
    private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    [StructLayout(LayoutKind.Sequential)] private struct Animation { public uint Size; public int Enabled; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Get(uint action, uint param, ref uint value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Set(uint action, uint param, nint value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetAnimation(uint action, uint param, ref Animation value, uint flags);
}
