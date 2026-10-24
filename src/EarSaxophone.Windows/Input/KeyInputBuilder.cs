using EarSaxophone.Core.Input;
using static EarSaxophone.Windows.Input.NativeInput;

namespace EarSaxophone.Windows.Input;

/// <summary>Builds the SendInput event sequence for a shortcut: modifiers down, key down, key up, modifiers up in reverse order.</summary>
internal static class KeyInputBuilder
{
    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkMenu = 0x12;
    private const ushort VkLeftWin = 0x5B;

    public static INPUT[] Build(Hotkey hotkey)
    {
        var modifierKeys = new List<ushort>();
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Ctrl))
        {
            modifierKeys.Add(VkControl);
        }

        if (hotkey.Modifiers.HasFlag(KeyModifiers.Alt))
        {
            modifierKeys.Add(VkMenu);
        }

        if (hotkey.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            modifierKeys.Add(VkShift);
        }

        if (hotkey.Modifiers.HasFlag(KeyModifiers.Win))
        {
            modifierKeys.Add(VkLeftWin);
        }

        var inputs = new List<INPUT>();
        foreach (ushort modifier in modifierKeys)
        {
            inputs.Add(Key(modifier, up: false));
        }

        inputs.Add(Key(hotkey.VirtualKey, up: false));
        inputs.Add(Key(hotkey.VirtualKey, up: true));
        for (int i = modifierKeys.Count - 1; i >= 0; i--)
        {
            inputs.Add(Key(modifierKeys[i], up: true));
        }

        return inputs.ToArray();
    }

    /// <summary>Keys that a physical keyboard reports with the "extended" flag (navigation block, Win, media/volume keys).</summary>
    public static bool IsExtended(ushort virtualKey) =>
        virtualKey is >= 0x21 and <= 0x28 // PageUp..Down, End, Home, arrows
        or 0x2C or 0x2D or 0x2E // PrintScreen, Insert, Delete
        or 0x5B or 0x5C // Win keys
        or (>= 0xA6 and <= 0xB7); // browser, volume and media keys

    private static INPUT Key(ushort virtualKey, bool up)
    {
        uint flags = (IsExtended(virtualKey) ? KeyEventExtendedKey : 0) | (up ? KeyEventKeyUp : 0);
        return new INPUT
        {
            type = InputKeyboard,
            u = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = virtualKey, wScan = 0, dwFlags = flags },
            },
        };
    }
}
