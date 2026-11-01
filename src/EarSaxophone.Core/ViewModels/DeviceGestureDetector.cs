using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.ViewModels;

/// <summary>
/// Decides when operating a control at the device means "I want this control" (selection follows the device, and the
/// question for a new program can be answered by moving a fader). A gesture starts with the first message of a control
/// after <see cref="QuietTime"/> without messages from it, and it counts once per gesture:
/// <list type="bullet">
/// <item>a key counts when it is pressed (value &gt;= 64), not when it is released;</item>
/// <item>a fader or knob counts when it moved at least <see cref="JitterSteps"/> away from where it was before the gesture
/// (a single jittering value does not count); the very first value of a control counts at once;</item>
/// <item>while another control is being operated (its gesture counted and its last message is younger than
/// <see cref="QuietTime"/>), nothing else counts, so the selection does not jump around while you operate one control.
/// A control that keeps moving counts as soon as the other one rests.</item>
/// </list>
/// Not thread-safe: the view model calls it on the UI thread in the order the messages arrived.
/// </summary>
public sealed class DeviceGestureDetector
{
    public static readonly TimeSpan QuietTime = TimeSpan.FromMilliseconds(600);

    public const int JitterSteps = 2;

    private readonly Dictionary<ControlId, State> _states = [];
    private ControlId? _owner;

    /// <summary>Feeds one message; returns its control when this message makes a gesture count, otherwise null.</summary>
    public ControlId? Observe(MidiMessage message, ControlShape shape, DateTimeOffset now)
    {
        ControlId control = message.Control;
        if (!_states.TryGetValue(control, out State? state))
        {
            state = new State();
            _states[control] = state;
        }

        if (state.LastTime is not { } last || now - last >= QuietTime)
        {
            state.Anchor = state.LastValue;
            state.Counted = false;
        }

        state.LastTime = now;
        state.LastValue = message.Value;
        if (state.Counted)
        {
            return null;
        }

        if (_owner is { } owner && owner != control && _states[owner].LastTime is { } ownerLast && now - ownerLast < QuietTime)
        {
            return null;
        }

        if (!IsDeliberate(shape, message.Value, state.Anchor))
        {
            return null;
        }

        state.Counted = true;
        _owner = control;
        return control;
    }

    private static bool IsDeliberate(ControlShape shape, int value, int? anchor) => shape switch
    {
        ControlShape.Button => value >= MappingDispatcher.PressThreshold,
        ControlShape.Continuous => anchor is not { } before || Math.Abs(value - before) >= JitterSteps,
        _ => value >= MappingDispatcher.PressThreshold || anchor is not { } previous || Math.Abs(value - previous) >= JitterSteps,
    };

    private sealed class State
    {
        public DateTimeOffset? LastTime { get; set; }

        public int? LastValue { get; set; }

        public int? Anchor { get; set; }

        public bool Counted { get; set; }
    }
}
