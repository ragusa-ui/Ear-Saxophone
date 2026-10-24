namespace EarSaxophone.Core.Midi;

public enum MidiMessageKind
{
    NoteOff,
    NoteOn,
    ControlChange,
}

/// <summary>Kind of physical control a message originates from. Note-On and Note-Off share one control.</summary>
public enum ControlKind
{
    ControlChange,
    Note,
}

/// <summary>Identity of a physical control on a device, independent of the value it sends.</summary>
public readonly record struct ControlId(ControlKind Kind, byte Channel, byte Number)
{
    public override string ToString() => $"{(Kind == ControlKind.ControlChange ? "CC" : "Note")} {Number} (Kanal {Channel + 1})";
}

/// <summary>A parsed channel-voice MIDI message. Channel is 0-15, Number and Value are 0-127.</summary>
public readonly record struct MidiMessage(MidiMessageKind Kind, byte Channel, byte Number, byte Value)
{
    public ControlId Control => new(
        Kind == MidiMessageKind.ControlChange ? ControlKind.ControlChange : ControlKind.Note,
        Channel,
        Number);
}
