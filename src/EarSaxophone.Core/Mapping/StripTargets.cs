using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Mapping;

/// <summary>
/// The fader of the channel strip a control belongs to (the fader itself included), null when the control is in no strip
/// (transport keys, controls found by MIDI-Learn). The default is <see cref="Nanokontrol2Layout.StripFaderOf"/>.
/// </summary>
public delegate ControlId? FaderLookup(ControlId control);

/// <summary>
/// Resolves the "Wie Fader" target (<see cref="AudioTarget.Strip"/>): an action with this target stands for one action of the same
/// kind (and mute mode) per volume target of the fader in the same strip. It is resolved whenever it is used (key press, LED,
/// picture, texts), so changing the fader's programs changes what the strip's keys mute at once. Rules:
/// <list type="bullet">
/// <item>Only concrete volume targets of the fader count; a strip target on the fader itself is never followed, so there are no
/// cycles and no self reference (resolution is one level deep).</item>
/// <item>A control outside a strip, the fader itself, or a fader without volume targets resolves to nothing: the strip action stays
/// as it is and does nothing when run (it reports why, see <c>ActionExecutor</c>).</item>
/// <item>Resolved targets that the mapping already has explicitly are not added twice.</item>
/// </list>
/// </summary>
public static class StripTargets
{
    /// <summary>The fader of the control's strip on the nanoKONTROL2 layout.</summary>
    public static ControlId? DefaultFaderOf(ControlId control) => Nanokontrol2Layout.StripFaderOf(control);

    /// <summary>The audio target of a volume or mute action, null for other actions.</summary>
    public static AudioTarget? TargetOf(ActionDefinition action) => action switch
    {
        SetVolumeAction volume => volume.Target,
        MuteAction mute => mute.Target,
        _ => null,
    };

    /// <summary>True for a volume or mute action with the "Wie Fader" target.</summary>
    public static bool IsStripAction(ActionDefinition action) => TargetOf(action) is { IsStrip: true };

    /// <summary>True when the mapping has at least one action with the "Wie Fader" target.</summary>
    public static bool HasStripTarget(ControlMapping? mapping) => mapping is not null && mapping.Actions.Any(a => a is not null && IsStripAction(a));

    /// <summary>
    /// The fader a strip target on <paramref name="control"/> refers to: the fader of its strip, null when the control is in no
    /// strip or is that fader itself (a fader never follows itself).
    /// </summary>
    public static ControlId? SourceFader(ControlId control, FaderLookup? faders = null)
    {
        ControlId? fader = (faders ?? DefaultFaderOf)(control);
        return fader is { } f && f != control ? f : null;
    }

    /// <summary>
    /// The concrete volume targets of the fader a strip target on <paramref name="control"/> refers to, in the fader's order and
    /// without duplicates; empty when there is no such fader, it is unmapped or it has no (valid) volume targets.
    /// </summary>
    public static IReadOnlyList<AudioTarget> FaderTargets(Profile profile, ControlId control, FaderLookup? faders = null)
    {
        if (SourceFader(control, faders) is not { } fader || profile.Find(fader) is not { } mapping)
        {
            return [];
        }

        return mapping.Actions.OfType<SetVolumeAction>()
            .Where(a => a.IsValid && !a.Target.IsStrip)
            .Select(a => a.Target)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// The mapping as it acts right now in <paramref name="profile"/>: every strip action replaced by one action per fader target.
    /// A mapping without strip target is returned unchanged (the same instance); when the fader has no targets the strip actions
    /// stay in place (they do nothing).
    /// </summary>
    public static ControlMapping Resolve(Profile profile, ControlMapping mapping, FaderLookup? faders = null)
    {
        if (!HasStripTarget(mapping))
        {
            return mapping;
        }

        IReadOnlyList<AudioTarget> targets = FaderTargets(profile, mapping.Control, faders);
        return targets.Count == 0 ? mapping : mapping with { Actions = Expand(mapping.Actions, targets) };
    }

    /// <summary>Like <see cref="Resolve(Profile, ControlMapping, FaderLookup?)"/>; null stays null.</summary>
    public static ControlMapping? ResolveOrNull(Profile profile, ControlMapping? mapping, FaderLookup? faders = null) =>
        mapping is null ? null : Resolve(profile, mapping, faders);

    /// <summary>
    /// Replaces the strip actions by actions with the given concrete <paramref name="targets"/> (used when a mapping is moved or
    /// copied to a control where "Wie Fader" would mean nothing or the fader itself). With no targets the strip actions are dropped.
    /// </summary>
    public static ControlMapping Materialize(ControlMapping mapping, IReadOnlyList<AudioTarget> targets) =>
        HasStripTarget(mapping) ? mapping with { Actions = Expand(mapping.Actions, targets) } : mapping;

    private static List<ActionDefinition> Expand(IReadOnlyList<ActionDefinition> actions, IReadOnlyList<AudioTarget> targets)
    {
        var result = new List<ActionDefinition>(actions.Count + targets.Count);
        var audio = new HashSet<ActionDefinition>();
        foreach (ActionDefinition action in actions)
        {
            if (!IsStripAction(action))
            {
                if (action is not SetVolumeAction and not MuteAction || audio.Add(action))
                {
                    result.Add(action);
                }

                continue;
            }

            foreach (AudioTarget target in targets)
            {
                ActionDefinition concrete = action switch
                {
                    SetVolumeAction volume => volume with { Target = target },
                    MuteAction mute => mute with { Target = target },
                    _ => action,
                };
                if (audio.Add(concrete))
                {
                    result.Add(concrete);
                }
            }
        }

        return result;
    }
}
