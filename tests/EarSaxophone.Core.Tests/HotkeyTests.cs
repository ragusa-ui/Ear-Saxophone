using EarSaxophone.Core.Input;

namespace EarSaxophone.Core.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+M", KeyModifiers.Ctrl | KeyModifiers.Alt, 0x4D)]
    [InlineData("ctrl+alt+m", KeyModifiers.Ctrl | KeyModifiers.Alt, 0x4D)]
    [InlineData("  Shift + F5 ", KeyModifiers.Shift, 0x74)]
    [InlineData("Strg+Entf", KeyModifiers.Ctrl, 0x2E)]
    [InlineData("Win+Up", KeyModifiers.Win, 0x26)]
    [InlineData("Ctrl+Shift+Alt+Win+F24", KeyModifiers.Ctrl | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Win, 0x87)]
    [InlineData("MediaPlayPause", KeyModifiers.None, 0xB3)]
    [InlineData("Enter", KeyModifiers.None, 0x0D)]
    [InlineData("7", KeyModifiers.None, 0x37)]
    [InlineData("Ctrl+Numpad5", KeyModifiers.Ctrl, 0x65)]
    public void ValidShortcuts_AreParsed(string text, KeyModifiers modifiers, int virtualKey)
    {
        Assert.True(Hotkey.TryParse(text, out Hotkey hotkey, out string error), error);
        Assert.Equal(modifiers, hotkey.Modifiers);
        Assert.Equal((ushort)virtualKey, hotkey.VirtualKey);
    }

    [Theory]
    [InlineData("", "leer")]
    [InlineData("   ", "leer")]
    [InlineData(null, "leer")]
    [InlineData("Ctrl+Alt", "fehlt eine Taste")]
    [InlineData("Ctrl+", "leeren Teil")]
    [InlineData("+M", "leeren Teil")]
    [InlineData("Ctrl++M", "leeren Teil")]
    [InlineData("Ctrl+A+B", "mehr als eine Taste")]
    [InlineData("Ctrl+Banana", "Unbekannte Taste 'Banana'")]
    [InlineData("F25", "Unbekannte Taste")]
    public void InvalidShortcuts_AreRejectedWithReadableReason(string? text, string expectedFragment)
    {
        Assert.False(Hotkey.TryParse(text, out Hotkey hotkey, out string error));
        Assert.Equal(default, hotkey);
        Assert.Contains(expectedFragment, error);
    }

    [Theory]
    [InlineData("alt+ctrl+m", "Ctrl+Alt+M")]
    [InlineData("STRG + shift + pgup", "Ctrl+Shift+PageUp")]
    [InlineData("win+esc", "Win+Escape")]
    [InlineData("mediaprev", "MediaPrevious")]
    public void Canonical_TextIsNormalised_AndRoundTrips(string input, string canonical)
    {
        Hotkey hotkey = Hotkey.Parse(input);

        Assert.Equal(canonical, hotkey.ToString());
        Assert.Equal(hotkey, Hotkey.Parse(canonical));
    }

    [Fact]
    public void Parse_ThrowsFormatException_ForInvalidText() =>
        Assert.Throws<FormatException>(() => Hotkey.Parse("Ctrl+"));

    [Fact]
    public void DuplicateModifiers_AreHarmless() =>
        Assert.Equal("Ctrl+A", Hotkey.Parse("Ctrl+Control+A").ToString());
}
