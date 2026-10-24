using System.Runtime.InteropServices;
using EarSaxophone.Core.Input;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Windows.Input;

namespace EarSaxophone.Windows.Tests;

public class KeyInputTests
{
    [Fact]
    public void InputStructure_HasTheSizeWindowsExpects() =>
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf<NativeInput.INPUT>());

    [Fact]
    public void Shortcut_PressesModifiersFirst_AndReleasesInReverseOrder()
    {
        NativeInput.INPUT[] inputs = KeyInputBuilder.Build(Hotkey.Parse("Ctrl+Alt+M"));

        var sequence = inputs.Select(i => (i.u.ki.wVk, Up: (i.u.ki.dwFlags & NativeInput.KeyEventKeyUp) != 0)).ToArray();
        Assert.Equal(
            [((ushort)0x11, false), ((ushort)0x12, false), ((ushort)0x4D, false), ((ushort)0x4D, true), ((ushort)0x12, true), ((ushort)0x11, true)],
            sequence);
        Assert.All(inputs, i => Assert.Equal(NativeInput.InputKeyboard, i.type));
    }

    [Fact]
    public void AllFourModifiers_AreSupported()
    {
        NativeInput.INPUT[] inputs = KeyInputBuilder.Build(Hotkey.Parse("Ctrl+Alt+Shift+Win+F5"));

        Assert.Equal(10, inputs.Length);
        Assert.Equal([(ushort)0x11, (ushort)0x12, (ushort)0x10, (ushort)0x5B], inputs.Take(4).Select(i => i.u.ki.wVk));
    }

    [Theory]
    [InlineData(MediaCommand.PlayPause)]
    [InlineData(MediaCommand.Next)]
    [InlineData(MediaCommand.Previous)]
    [InlineData(MediaCommand.Stop)]
    public void MediaKeys_AreSingleExtendedKeyPresses_AndMatchTheCoreKeyTable(MediaCommand command)
    {
        ushort code = MediaKeys.CodeFor(command);
        NativeInput.INPUT[] inputs = KeyInputBuilder.Build(new Hotkey(KeyModifiers.None, code));

        Assert.Equal(2, inputs.Length);
        Assert.All(inputs, i => Assert.NotEqual(0u, i.u.ki.dwFlags & NativeInput.KeyEventExtendedKey));
        Assert.Equal(0u, inputs[0].u.ki.dwFlags & NativeInput.KeyEventKeyUp);
        Assert.NotEqual(0u, inputs[1].u.ki.dwFlags & NativeInput.KeyEventKeyUp);

        string name = command switch
        {
            MediaCommand.PlayPause => "MediaPlayPause",
            MediaCommand.Next => "MediaNext",
            MediaCommand.Previous => "MediaPrevious",
            _ => "MediaStop",
        };
        Assert.Equal(code, Hotkey.Parse(name).VirtualKey);
    }

    [Theory]
    [InlineData("Left", true)]
    [InlineData("Delete", true)]
    [InlineData("Win+E", false)] // E itself is not extended, only the Win modifier is
    [InlineData("A", false)]
    [InlineData("F5", false)]
    [InlineData("Enter", false)]
    public void ExtendedFlag_IsSetOnlyForKeysAKeyboardReportsAsExtended(string text, bool extended)
    {
        Hotkey hotkey = Hotkey.Parse(text);
        NativeInput.INPUT mainKeyDown = KeyInputBuilder.Build(hotkey).First(i => i.u.ki.wVk == hotkey.VirtualKey);

        Assert.Equal(extended, (mainKeyDown.u.ki.dwFlags & NativeInput.KeyEventExtendedKey) != 0);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Enter")]
    [InlineData("F5")]
    [InlineData("Left")]
    public void Keys_CarryAScanCode_ForProgramsThatReadRawInput(string key)
    {
        Hotkey hotkey = Hotkey.Parse(key);

        NativeInput.INPUT down = KeyInputBuilder.Build(hotkey).First(i => i.u.ki.wVk == hotkey.VirtualKey);

        Assert.NotEqual((ushort)0, down.u.ki.wScan);
    }

    [Fact]
    public void UnknownMediaCommand_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MediaKeys.CodeFor((MediaCommand)99));
}
