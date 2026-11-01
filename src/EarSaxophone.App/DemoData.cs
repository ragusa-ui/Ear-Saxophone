using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;

namespace EarSaxophone.App;

/// <summary>Sample profile for the CI screenshot (--demo-screenshot); never used in normal operation.</summary>
internal static class DemoData
{
    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    public static ProfileDocument Create() => new(ProfileDocument.CurrentSchemaVersion, "Streaming",
    [
        new Profile("Streaming",
        [
            new ControlMapping(Cc(0), new SetVolumeAction(AudioTarget.Application("Discord.exe"))),
            new ControlMapping(Cc(1), new SetVolumeAction(AudioTarget.Speakers)),
            new ControlMapping(Cc(2), new SetVolumeAction(AudioTarget.Microphone), new ValueScaling(0.1f, 0.9f, false)),
            new ControlMapping(Cc(3), new SetVolumeAction(AudioTarget.Application("Spotify.exe"))),
            new ControlMapping(Cc(48), new MuteAction(AudioTarget.Microphone)),
            new ControlMapping(Cc(49), new MuteAction(AudioTarget.Application("Discord.exe"))),
            new ControlMapping(Cc(41), new MediaAction(MediaCommand.PlayPause)),
            new ControlMapping(Cc(59), new MediaAction(MediaCommand.Next)),
            new ControlMapping(Cc(58), new MediaAction(MediaCommand.Previous)),
            new ControlMapping(Cc(46), new HotkeyAction("Ctrl+Alt+M")),
        ]),
        new Profile("Spiele", []),
    ]);
}
