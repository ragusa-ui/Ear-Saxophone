namespace EarSaxophone.Core.Input;

/// <summary>Key names accepted in shortcuts, mapped to Windows virtual-key codes. Punctuation keys are not supported yet.</summary>
internal static class KeyTable
{
    private static readonly Dictionary<string, ushort> ByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ushort, string> ByCode = [];

    static KeyTable()
    {
        for (char c = 'A'; c <= 'Z'; c++)
        {
            Add(c.ToString(), c);
        }

        for (char c = '0'; c <= '9'; c++)
        {
            Add(c.ToString(), c);
        }

        for (int n = 1; n <= 24; n++)
        {
            Add($"F{n}", (ushort)(0x70 + n - 1));
        }

        for (int n = 0; n <= 9; n++)
        {
            Add($"Numpad{n}", (ushort)(0x60 + n));
        }

        Add("Enter", 0x0D, "Return");
        Add("Escape", 0x1B, "Esc");
        Add("Tab", 0x09);
        Add("Space", 0x20);
        Add("Backspace", 0x08);
        Add("Delete", 0x2E, "Del", "Entf");
        Add("Insert", 0x2D, "Ins");
        Add("Home", 0x24);
        Add("End", 0x23);
        Add("PageUp", 0x21, "PgUp");
        Add("PageDown", 0x22, "PgDn");
        Add("Left", 0x25);
        Add("Up", 0x26);
        Add("Right", 0x27);
        Add("Down", 0x28);
        Add("PrintScreen", 0x2C);
        Add("MediaNext", 0xB0);
        Add("MediaPrevious", 0xB1, "MediaPrev");
        Add("MediaStop", 0xB2);
        Add("MediaPlayPause", 0xB3);
        Add("VolumeMute", 0xAD);
        Add("VolumeDown", 0xAE);
        Add("VolumeUp", 0xAF);
    }

    public static bool TryGetCode(string name, out ushort code) => ByName.TryGetValue(name, out code);

    public static string GetName(ushort code) => ByCode.TryGetValue(code, out string? name) ? name : $"VK_0x{code:X2}";

    private static void Add(string name, ushort code, params string[] aliases)
    {
        ByName[name] = code;
        ByCode[code] = name;
        foreach (string alias in aliases)
        {
            ByName[alias] = code;
        }
    }
}
