using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Localization;

/// <summary>One-line German description of what a mapping does, for the controller overview.</summary>
public static class MappingDescriber
{
    /// <summary>The own name of the mapping if it has one, otherwise its first action (plus how many more there are).</summary>
    public static string Describe(ControlMapping? mapping)
    {
        if (mapping?.FirstAction is not { } first)
        {
            return Texts.UiNoMapping;
        }

        if (!string.IsNullOrWhiteSpace(mapping.Name))
        {
            return mapping.Name;
        }

        string text = Describe(first);
        return mapping.Actions.Count > 1 ? Texts.DescribeMore(text, mapping.Actions.Count - 1) : text;
    }

    public static string Describe(ActionDefinition action) => action switch
    {
        SetVolumeAction a => Texts.DescribeVolume(Describe(a.Target)),
        MuteAction { Mode: MuteMode.MuteWhileHeld } a => Texts.DescribeMuteWhileHeld(Describe(a.Target)),
        MuteAction { Mode: MuteMode.UnmuteWhileHeld } a => Texts.DescribeUnmuteWhileHeld(Describe(a.Target)),
        MuteAction a => Texts.DescribeMute(Describe(a.Target)),
        MediaAction a => Texts.DescribeMedia(a.Command),
        HotkeyAction a => Texts.DescribeHotkey(a.Keys),
        SwitchDeviceAction a => Texts.DescribeSwitch(a.Flow, a.SelectedIds.Count),
        _ => Texts.UiNoMapping,
    };

    /// <summary>Name of the kind of action, used in messages such as "Lautstärke passt nicht zu einer Taste".</summary>
    public static string KindName(ActionDefinition action) => action switch
    {
        SetVolumeAction => Texts.KindNameVolume,
        MuteAction => Texts.KindNameMute,
        MediaAction => Texts.KindNameMedia,
        HotkeyAction => Texts.KindNameHotkey,
        SwitchDeviceAction => Texts.KindNameSwitch,
        _ => Texts.KindNameUnknown,
    };

    public static string Describe(AudioTarget target) => target.Kind switch
    {
        AudioTargetKind.Application => target.App?.ExeName ?? "?",
        AudioTargetKind.SpecificDevice => Texts.TargetDeviceShort,
        _ when target.Flow == DeviceFlow.Capture => Texts.TargetMicrophoneShort,
        _ => Texts.TargetSpeakersShort,
    };
}
