using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Mapping;

/// <summary>
/// MIDI-Learn: after <see cref="Start"/>, the first meaningful message identifies the control.
/// Note-Off and Control-Change with value 0 are ignored so that the release of a button pressed just before
/// learning started cannot be captured by mistake.
/// </summary>
public sealed class MidiLearnSession
{
    public bool IsActive { get; private set; }

    public event Action<ControlId>? Captured;

    public void Start() => IsActive = true;

    public void Cancel() => IsActive = false;

    /// <summary>Feeds a message; returns the captured control when learning finished, otherwise null.</summary>
    public ControlId? TryCapture(MidiMessage message)
    {
        if (!IsActive || message.Kind == MidiMessageKind.NoteOff || message.Value == 0)
        {
            return null;
        }

        IsActive = false;
        ControlId control = message.Control;
        Captured?.Invoke(control);
        return control;
    }
}
