namespace EarSaxophone.Core.Midi;

/// <summary>
/// An opened MIDI output port (used to switch the key LEDs of the device). Implementations must be thread-safe; a send on a
/// disposed or vanished port throws, which the caller treats as "port gone".
/// </summary>
public interface IMidiOutput : IDisposable
{
    /// <summary>Sends a Control-Change. Channel is 0-15, number and value 0-127.</summary>
    void SendControlChange(byte channel, byte number, byte value);

    /// <summary>Sends a Note-On (velocity 0 is the usual way to say Note-Off). Channel is 0-15, number and velocity 0-127.</summary>
    void SendNoteOn(byte channel, byte number, byte velocity);
}

/// <summary>
/// Lists and opens MIDI output ports. The output list is separate from the input list (other indexes, possibly other names).
/// The Windows implementation wraps NAudio; tests use a fake.
/// </summary>
public interface IMidiOutputProvider
{
    IReadOnlyList<MidiDeviceInfo> GetDevices();

    IMidiOutput Open(MidiDeviceInfo device);
}

/// <summary>Builds WinMM short messages (status | data1 &lt;&lt; 8 | data2 &lt;&lt; 16), the counterpart of <see cref="MidiMessageParser.TryParse(int, out MidiMessage)"/>.</summary>
public static class MidiShortMessage
{
    public static int ControlChange(byte channel, byte number, byte value) => Pack(0xB0, channel, number, value);

    public static int NoteOn(byte channel, byte number, byte velocity) => Pack(0x90, channel, number, velocity);

    private static int Pack(int status, byte channel, byte data1, byte data2)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channel, (byte)15);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(data1, (byte)127);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(data2, (byte)127);
        return (status | channel) | (data1 << 8) | (data2 << 16);
    }
}
