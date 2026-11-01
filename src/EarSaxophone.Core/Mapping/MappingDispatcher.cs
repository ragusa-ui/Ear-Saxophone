using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Mapping;

/// <summary>
/// One action that should run now.
/// </summary>
/// <param name="Mapping">The mapping the action belongs to (all actions of one control message share it).</param>
/// <param name="Action">The action to run, one of <see cref="ControlMapping.Actions"/>.</param>
/// <param name="Level">Scaled control value 0..1 for continuous actions; 1 on press and 0 on release for the others.</param>
/// <param name="Phase">Whether a value changed, a key went down or a key went up.</param>
public sealed record ActionInvocation(ControlMapping Mapping, ActionDefinition Action, float Level, ActionPhase Phase)
{
    /// <summary>
    /// The executions one control event causes for a mapping: every continuous action with <paramref name="level"/>
    /// (phase <see cref="ActionPhase.Changed"/>), and, when <paramref name="keyPhase"/> is a press or release, every other
    /// action that reacts to it (see <see cref="ActionRules.RespondsTo"/>). The order is the order of the actions.
    /// </summary>
    public static IReadOnlyList<ActionInvocation> ForMapping(ControlMapping mapping, ActionPhase? keyPhase, float level)
    {
        var result = new List<ActionInvocation>(mapping.Actions.Count);
        foreach (ActionDefinition action in mapping.Actions)
        {
            if (action.IsContinuous)
            {
                result.Add(new ActionInvocation(mapping, action, level, ActionPhase.Changed));
            }
            else if (keyPhase is { } phase && ActionRules.RespondsTo(action, phase))
            {
                result.Add(new ActionInvocation(mapping, action, phase == ActionPhase.Released ? 0f : 1f, phase));
            }
        }

        return result;
    }
}

/// <summary>
/// Turns MIDI messages into action executions for the active profile.
/// Continuous actions run on every message with the scaled value. For the other actions the rising edge
/// (value &gt;= 64) is <see cref="ActionPhase.Pressed"/> and the falling edge is <see cref="ActionPhase.Released"/>; repeats on
/// the same side of the threshold do nothing. Released only reaches mute actions in a "while held" mode.
/// "Wie Fader" targets are resolved against the current profile for every message (see <see cref="StripTargets"/>), so the
/// executions always carry the fader's current programs; <see cref="ActionInvocation.Mapping"/> is the resolved mapping.
/// </summary>
public sealed class MappingDispatcher
{
    public const byte PressThreshold = 64;

    private readonly HashSet<ControlId> _pressed = [];
    private readonly FaderLookup? _faders;
    private Profile _profile;

    public MappingDispatcher(Profile profile, FaderLookup? faders = null)
    {
        _profile = profile;
        _faders = faders;
    }

    public void SetProfile(Profile profile)
    {
        _profile = profile;
        _pressed.Clear();
    }

    /// <summary>All executions caused by the message, in the order of the mapping's actions; empty when nothing should run.</summary>
    public IReadOnlyList<ActionInvocation> Dispatch(MidiMessage message)
    {
        ControlId control = message.Control;
        bool isDown = message.Value >= PressThreshold;
        bool wasDown = isDown ? !_pressed.Add(control) : _pressed.Remove(control);

        if (_profile.Find(control) is not { } stored)
        {
            return [];
        }

        ControlMapping mapping = StripTargets.Resolve(_profile, stored, _faders);
        ActionPhase? edge = isDown == wasDown ? null : isDown ? ActionPhase.Pressed : ActionPhase.Released;
        return ActionInvocation.ForMapping(mapping, edge, mapping.EffectiveScaling.Apply(message.Value));
    }
}
