namespace EarSaxophone.Core.Midi;

/// <summary>One MIDI input port as reported by the OS. Index can change between runs; match devices by name.</summary>
public sealed record MidiDeviceInfo(int Index, string Name);

/// <summary>An opened MIDI input port. Events may be raised on a driver thread.</summary>
public interface IMidiInput : IDisposable
{
    event Action<MidiMessage>? MessageReceived;

    /// <summary>Raised when the driver reports a problem (for example a malformed message). It does not necessarily mean the device is gone.</summary>
    event Action<string>? Faulted;

    void Start();

    void Stop();
}

/// <summary>Lists and opens MIDI input ports. The Windows implementation wraps NAudio; tests use a fake.</summary>
public interface IMidiInputProvider
{
    IReadOnlyList<MidiDeviceInfo> GetDevices();

    IMidiInput Open(MidiDeviceInfo device);
}
