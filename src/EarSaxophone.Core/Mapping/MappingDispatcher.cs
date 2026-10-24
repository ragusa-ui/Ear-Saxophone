using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Mapping;

/// <summary>An action that should run now. Level is the scaled control value (continuous actions only).</summary>
public sealed record ActionInvocation(ControlMapping Mapping, float Level);

/// <summary>
/// Turns MIDI messages into action invocations for the active profile.
/// Continuous actions fire on every message. Trigger actions fire once on the rising edge (value &gt;= 64) and
/// stay quiet until the control was released (value &lt; 64), so button release and repeats do not re-trigger.
/// </summary>
public sealed class MappingDispatcher
{
    public const byte PressThreshold = 64;

    private readonly HashSet<ControlId> _pressed = [];
    private Profile _profile;

    public MappingDispatcher(Profile profile)
    {
        _profile = profile;
    }

    public void SetProfile(Profile profile)
    {
        _profile = profile;
        _pressed.Clear();
    }

    public ActionInvocation? Dispatch(MidiMessage message)
    {
        ControlId control = message.Control;
        bool isDown = message.Value >= PressThreshold;
        bool wasDown = isDown ? !_pressed.Add(control) : _pressed.Remove(control);

        ControlMapping? mapping = _profile.Find(control);
        if (mapping is null)
        {
            return null;
        }

        if (mapping.Action.IsContinuous)
        {
            return new ActionInvocation(mapping, mapping.EffectiveScaling.Apply(message.Value));
        }

        return isDown && !wasDown ? new ActionInvocation(mapping, 1f) : null;
    }
}
