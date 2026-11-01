using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Localization;

/// <summary>One-line German description of what a mapping does, for the controller overview.</summary>
public static class MappingDescriber
{
    public static string Describe(ControlMapping? mapping) => mapping is null ? Texts.UiNoMapping : Describe(mapping.Action);

    public static string Describe(ActionDefinition action) => action switch
    {
        SetVolumeAction a => Texts.DescribeVolume(Describe(a.Target)),
        ToggleMuteAction a => Texts.DescribeMute(Describe(a.Target)),
        MediaAction a => Texts.DescribeMedia(a.Command),
        HotkeyAction a => Texts.DescribeHotkey(a.Keys),
        SwitchDeviceAction a => Texts.DescribeSwitch(a.Flow, a.SelectedIds.Count),
        _ => Texts.UiNoMapping,
    };

    public static string Describe(AudioTarget target) => target.Kind switch
    {
        AudioTargetKind.Application => target.App?.ExeName ?? "?",
        AudioTargetKind.SpecificDevice => Texts.TargetDeviceShort,
        _ when target.Flow == DeviceFlow.Capture => Texts.TargetMicrophoneShort,
        _ => Texts.TargetSpeakersShort,
    };
}
