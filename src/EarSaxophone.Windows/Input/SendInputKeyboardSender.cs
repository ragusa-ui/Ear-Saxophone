using System.Runtime.InteropServices;
using EarSaxophone.Core.Input;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Windows.Input;

/// <summary>
/// Sends media keys and shortcuts with SendInput. Media keys are system-wide: Windows routes them to the application it
/// considers the current media session, which is not always the one the user means (see docs/MEDIA_CONTROL.md).
/// Compiled and structurally tested in CI; real key delivery must be verified locally.
/// </summary>
public sealed class SendInputKeyboardSender : IKeyboardSender
{
    public void SendMediaCommand(MediaCommand command) =>
        Send(KeyInputBuilder.Build(new Hotkey(KeyModifiers.None, MediaKeys.CodeFor(command))));

    public void SendHotkey(Hotkey hotkey) => Send(KeyInputBuilder.Build(hotkey));

    private static void Send(NativeInput.INPUT[] inputs)
    {
        uint sent = NativeInput.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput.INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"Windows blocked the key input ({sent} of {inputs.Length} events, error {Marshal.GetLastWin32Error()}). " +
                "A program running as administrator may have the focus.");
        }
    }
}
