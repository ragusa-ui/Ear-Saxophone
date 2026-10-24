using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Input;

/// <summary>
/// Sends keyboard input to Windows. Implementations throw when Windows refuses the input
/// (for example when a program running as administrator has the focus).
/// </summary>
public interface IKeyboardSender
{
    /// <summary>Presses the system-wide media key. Windows decides which application receives it.</summary>
    void SendMediaCommand(MediaCommand command);

    void SendHotkey(Hotkey hotkey);
}
