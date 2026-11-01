using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Mapping;

/// <summary>How a physical control behaves: stepless (knob, fader: 0..127) or a key (pressed / released).</summary>
public enum ControlShape
{
    /// <summary>Not known yet (a control found by MIDI-Learn that has not shown its behaviour); every action is allowed.</summary>
    Unknown,

    /// <summary>Knob or fader: every value 0..127 is meaningful.</summary>
    Continuous,

    /// <summary>Key: only pressed (value &gt;= 64) and released matter.</summary>
    Button,
}

/// <summary>Tells the shape of a control (for example <see cref="Devices.Nanokontrol2Layout.ShapeOf"/> or <see cref="ControlShapeTracker.ShapeOf"/>).</summary>
public delegate ControlShape ShapeLookup(ControlId control);

/// <summary>Which phase of a control a single action execution belongs to.</summary>
public enum ActionPhase
{
    /// <summary>A stepless control sent a new value (continuous actions, every message).</summary>
    Changed,

    /// <summary>A key went down (rising edge, value &gt;= 64).</summary>
    Pressed,

    /// <summary>A key went up (falling edge). Only "while held" mute actions react to it.</summary>
    Released,
}

/// <summary>Which actions fit which kind of control, and which control phase an action reacts to.</summary>
public static class ActionRules
{
    /// <summary>
    /// Stepless controls only take continuous actions (volume), keys only take the others (mute, media, switch device,
    /// shortcut). For an unknown control everything is allowed.
    /// </summary>
    public static bool IsAllowed(ActionDefinition action, ControlShape shape) => shape switch
    {
        ControlShape.Continuous => action.IsContinuous,
        ControlShape.Button => !action.IsContinuous,
        _ => true,
    };

    /// <summary>True when every action of the mapping fits the shape (an empty mapping fits everywhere).</summary>
    public static bool IsAllowed(ControlMapping mapping, ControlShape shape) => mapping.Actions.All(a => IsAllowed(a, shape));

    /// <summary>
    /// Whether the action runs in this phase: continuous actions on <see cref="ActionPhase.Changed"/>, the others on
    /// <see cref="ActionPhase.Pressed"/>, and "while held" mute actions also on <see cref="ActionPhase.Released"/>.
    /// </summary>
    public static bool RespondsTo(ActionDefinition action, ActionPhase phase) => phase switch
    {
        ActionPhase.Changed => action.IsContinuous,
        ActionPhase.Pressed => !action.IsContinuous,
        ActionPhase.Released => action is MuteAction { IsWhileHeld: true },
        _ => false,
    };
}

/// <summary>
/// Learns the shape of controls that are not part of the known layout from the values they send. A control is
/// <see cref="ControlShape.Unknown"/> until it sent a value other than 0 and 127 (then <see cref="ControlShape.Continuous"/>,
/// for good) or both 0 and 127 and nothing else (then <see cref="ControlShape.Button"/>). Note messages always come from
/// keys or pads (their value is the velocity), so they are <see cref="ControlShape.Button"/> at once. Thread-safe.
/// </summary>
public sealed class ControlShapeTracker(ShapeLookup? known = null)
{
    private readonly object _gate = new();
    private readonly Dictionary<ControlId, (bool SawLow, bool SawHigh, bool SawMiddle)> _seen = [];

    /// <summary>Feeds one message and returns the shape known afterwards.</summary>
    public ControlShape Observe(MidiMessage message)
    {
        ControlId control = message.Control;
        lock (_gate)
        {
            _seen.TryGetValue(control, out var seen);
            switch (message.Value)
            {
                case 0:
                    seen.SawLow = true;
                    break;
                case 127:
                    seen.SawHigh = true;
                    break;
                default:
                    seen.SawMiddle = true;
                    break;
            }

            _seen[control] = seen;
        }

        return ShapeOf(control);
    }

    /// <summary>The shape from the known layout, otherwise what the control has shown so far.</summary>
    public ControlShape ShapeOf(ControlId control)
    {
        if (known?.Invoke(control) is { } fixedShape and not ControlShape.Unknown)
        {
            return fixedShape;
        }

        if (control.Kind == ControlKind.Note)
        {
            return ControlShape.Button;
        }

        lock (_gate)
        {
            if (!_seen.TryGetValue(control, out var seen))
            {
                return ControlShape.Unknown;
            }

            return seen.SawMiddle ? ControlShape.Continuous
                : seen.SawLow && seen.SawHigh ? ControlShape.Button
                : ControlShape.Unknown;
        }
    }
}
