using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.App;

/// <summary>Sample profile and window state for the CI screenshot (--demo-screenshot); never used in normal operation.</summary>
internal static class DemoData
{
    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    private static AudioTarget App(string exe) => AudioTarget.Application(exe);

    /// <summary>
    /// A profile with several targets on one control (fader 1 "Spiele" with three games, fader 4 and M 3 with two programs),
    /// all action kinds, a push-to-talk key and R 4 with "Wie Fader 4". Fader 8 stays free (the self-check drags onto it); S 4 and M 4
    /// stay free (the self-check chooses "Stumm" there and expects "Wie Fader 4" preselected).
    /// </summary>
    public static ProfileDocument Create() => new(ProfileDocument.CurrentSchemaVersion, "Streaming",
    [
        new Profile("Streaming",
        [
            new ControlMapping(Cc(0),
                [new SetVolumeAction(App("Cyberpunk2077.exe")), new SetVolumeAction(App("eldenring.exe")), new SetVolumeAction(App("RocketLeague.exe"))],
                Name: "Spiele"),
            new ControlMapping(Cc(1), new SetVolumeAction(AudioTarget.Speakers)),
            new ControlMapping(Cc(2), new SetVolumeAction(AudioTarget.Microphone), new ValueScaling(0.1f, 0.9f, false)),
            new ControlMapping(Cc(3), [new SetVolumeAction(App("Spotify.exe")), new SetVolumeAction(App("chrome.exe"))]),
            new ControlMapping(Cc(4), new SetVolumeAction(App("Discord.exe"))),
            new ControlMapping(Cc(16), [new SetVolumeAction(App("firefox.exe"))], Name: "Browser"),
            new ControlMapping(Cc(32), new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld)),
            new ControlMapping(Cc(48), new MuteAction(AudioTarget.Microphone)),
            new ControlMapping(Cc(49), new MuteAction(App("Discord.exe"))),
            new ControlMapping(Cc(50), [new MuteAction(App("Spotify.exe")), new MuteAction(App("chrome.exe"))]),
            new ControlMapping(Cc(64), new SwitchDeviceAction(DeviceFlow.Render)),
            new ControlMapping(Cc(67), new MuteAction(AudioTarget.Strip)), // R 4: "Wie Fader 4" (Spotify + chrome)
            new ControlMapping(Cc(41), new MediaAction(MediaCommand.PlayPause)),
            new ControlMapping(Cc(59), new MediaAction(MediaCommand.Next)),
            new ControlMapping(Cc(58), new MediaAction(MediaCommand.Previous)),
            new ControlMapping(Cc(46), new HotkeyAction("Ctrl+Alt+M")),
        ]),
        new Profile("Spiele", []),
    ]);

    /// <summary>
    /// Window state of the screenshot: fader 1 ("Spiele", three games) is selected and a fourth program was added but not
    /// applied yet, so the picture shows the target list and the highlighted apply bar. <see cref="UiSelfCheck"/> starts from here.
    /// </summary>
    public static void PrepareScreenshot(MainViewModel viewModel)
    {
        viewModel.Select(viewModel.Elements.First(e => e.Label == "Fader 1"));
        viewModel.Editor.AddApplication("steam.exe");
    }
}

