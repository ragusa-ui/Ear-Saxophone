using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Windows.Input;

internal static class MediaKeys
{
    public static ushort CodeFor(MediaCommand command) => command switch
    {
        MediaCommand.PlayPause => 0xB3,
        MediaCommand.Next => 0xB0,
        MediaCommand.Previous => 0xB1,
        MediaCommand.Stop => 0xB2,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown media command."),
    };
}
