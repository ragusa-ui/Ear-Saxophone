namespace EarSaxophone.Core.Midi;

/// <summary>
/// Parses raw MIDI bytes into <see cref="MidiMessage"/>. Only Note-Off, Note-On and Control-Change are
/// supported; everything else (system messages, pitch bend, program change, ...) is rejected.
/// A Note-On with velocity 0 is normalised to Note-Off. Running status is not handled; the OS driver resolves it.
/// </summary>
public static class MidiMessageParser
{
    public static bool TryParse(ReadOnlySpan<byte> bytes, out MidiMessage message)
    {
        message = default;
        if (bytes.Length != 3)
        {
            return false;
        }

        byte status = bytes[0];
        byte data1 = bytes[1];
        byte data2 = bytes[2];
        if ((status & 0x80) == 0 || data1 > 127 || data2 > 127)
        {
            return false;
        }

        byte channel = (byte)(status & 0x0F);
        switch (status & 0xF0)
        {
            case 0x80:
                message = new MidiMessage(MidiMessageKind.NoteOff, channel, data1, 0);
                return true;
            case 0x90:
                message = data2 == 0
                    ? new MidiMessage(MidiMessageKind.NoteOff, channel, data1, 0)
                    : new MidiMessage(MidiMessageKind.NoteOn, channel, data1, data2);
                return true;
            case 0xB0:
                message = new MidiMessage(MidiMessageKind.ControlChange, channel, data1, data2);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Parses a Windows WinMM short message (status | data1 &lt;&lt; 8 | data2 &lt;&lt; 16).</summary>
    public static bool TryParse(int packed, out MidiMessage message)
    {
        if ((packed & unchecked((int)0xFF000000)) != 0)
        {
            message = default;
            return false;
        }

        Span<byte> bytes = stackalloc byte[3]
        {
            (byte)(packed & 0xFF),
            (byte)((packed >> 8) & 0xFF),
            (byte)((packed >> 16) & 0xFF),
        };
        return TryParse(bytes, out message);
    }
}
