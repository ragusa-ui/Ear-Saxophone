using System.Text;

namespace EarSaxophone.Core.Input;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>A keyboard shortcut: modifiers plus one key given as Windows virtual-key code.</summary>
public readonly record struct Hotkey(KeyModifiers Modifiers, ushort VirtualKey)
{
    private static readonly (string Name, KeyModifiers Modifier)[] ModifierAliases =
    [
        ("ctrl", KeyModifiers.Ctrl), ("control", KeyModifiers.Ctrl), ("strg", KeyModifiers.Ctrl),
        ("alt", KeyModifiers.Alt),
        ("shift", KeyModifiers.Shift), ("umschalt", KeyModifiers.Shift),
        ("win", KeyModifiers.Win), ("windows", KeyModifiers.Win), ("meta", KeyModifiers.Win), ("super", KeyModifiers.Win),
    ];

    /// <summary>Parses text like "Ctrl+Alt+M", "Shift + F5" or "MediaPlayPause". Case-insensitive.</summary>
    public static bool TryParse(string? text, out Hotkey hotkey, out string error)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "The shortcut is empty.";
            return false;
        }

        KeyModifiers modifiers = KeyModifiers.None;
        ushort? key = null;
        foreach (string raw in text.Split('+'))
        {
            string token = raw.Trim();
            if (token.Length == 0)
            {
                error = $"'{text}' has an empty part (a '+' without a key next to it).";
                return false;
            }

            var modifier = ModifierAliases.FirstOrDefault(m => string.Equals(m.Name, token, StringComparison.OrdinalIgnoreCase));
            if (modifier.Name is not null)
            {
                modifiers |= modifier.Modifier;
                continue;
            }

            if (!KeyTable.TryGetCode(token, out ushort code))
            {
                error = $"Unknown key '{token}'.";
                return false;
            }

            if (key is not null)
            {
                error = $"'{text}' contains more than one key; use modifiers (Ctrl, Alt, Shift, Win) plus a single key.";
                return false;
            }

            key = code;
        }

        if (key is null)
        {
            error = "The shortcut needs a key in addition to the modifiers.";
            return false;
        }

        hotkey = new Hotkey(modifiers, key.Value);
        error = string.Empty;
        return true;
    }

    public static Hotkey Parse(string text) =>
        TryParse(text, out Hotkey hotkey, out string error) ? hotkey : throw new FormatException(error);

    /// <summary>Canonical text, for example "Ctrl+Alt+M". Parsing it again yields the same value.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach ((KeyModifiers flag, string name) in new[]
        {
            (KeyModifiers.Ctrl, "Ctrl"), (KeyModifiers.Alt, "Alt"), (KeyModifiers.Shift, "Shift"), (KeyModifiers.Win, "Win"),
        })
        {
            if (Modifiers.HasFlag(flag))
            {
                builder.Append(name).Append('+');
            }
        }

        return builder.Append(KeyTable.GetName(VirtualKey)).ToString();
    }
}
