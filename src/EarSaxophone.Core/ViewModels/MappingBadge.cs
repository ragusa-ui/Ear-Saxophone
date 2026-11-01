using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.ViewModels;

/// <summary>
/// What the small badge on a control in the picture shows: the kinds of its actions (as symbol ids, in the order of
/// first appearance) and how many actions there are. The App turns <see cref="ActionKind"/> into a symbol.
/// </summary>
public sealed record MappingBadge(IReadOnlyList<ActionKind> Kinds, int ActionCount)
{
    public static MappingBadge Empty { get; } = new([], 0);

    /// <summary>Symbol of the first action, <see cref="ActionKind.None"/> for an unmapped control.</summary>
    public ActionKind PrimaryKind => Kinds.Count > 0 ? Kinds[0] : ActionKind.None;

    public bool HasBadge => ActionCount > 0;

    /// <summary>The count is only worth showing when there is more than one action (for example "3" programs on a fader).</summary>
    public bool ShowsCount => ActionCount > 1;

    public static MappingBadge From(ControlMapping? mapping) => mapping is null || mapping.Actions.Count == 0
        ? Empty
        : new MappingBadge(mapping.Actions.Select(KindOf).Distinct().ToList(), mapping.Actions.Count);

    /// <summary>The editor kind / symbol id of an action.</summary>
    public static ActionKind KindOf(ActionDefinition action) => action switch
    {
        SetVolumeAction => ActionKind.SetVolume,
        MuteAction => ActionKind.Mute,
        MediaAction => ActionKind.Media,
        HotkeyAction => ActionKind.Hotkey,
        SwitchDeviceAction => ActionKind.SwitchDevice,
        _ => ActionKind.None,
    };
}
