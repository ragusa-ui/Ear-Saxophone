using EarSaxophone.Core.Midi;
using NAudio.Midi;
using IMidiOutput = EarSaxophone.Core.Midi.IMidiOutput;

namespace EarSaxophone.Windows.Midi;

/// <summary>
/// WinMM MIDI output via NAudio (for the key LEDs). The output list is separate from the input list. Compiled and smoke-tested
/// in CI without hardware; whether the nanoKONTROL2 reacts must be verified locally.
/// </summary>
public sealed class NAudioMidiOutputProvider : IMidiOutputProvider
{
    public IReadOnlyList<MidiDeviceInfo> GetDevices()
    {
        var devices = new List<MidiDeviceInfo>();
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            try
            {
                devices.Add(new MidiDeviceInfo(i, MidiOut.DeviceInfo(i).ProductName));
            }
            catch (Exception)
            {
                // A device was removed while listing; skip this entry.
            }
        }

        return devices;
    }

    public IMidiOutput Open(MidiDeviceInfo device) => new NAudioMidiOutput(device.Index);

    /// <summary>Thread-safe; sending after <see cref="Dispose"/> throws <see cref="ObjectDisposedException"/>, disposing twice is harmless.</summary>
    internal sealed class NAudioMidiOutput(int deviceIndex) : IMidiOutput
    {
        private readonly object _gate = new();
        private MidiOut? _midiOut = new(deviceIndex);

        public void SendControlChange(byte channel, byte number, byte value) => Send(MidiShortMessage.ControlChange(channel, number, value));

        public void SendNoteOn(byte channel, byte number, byte velocity) => Send(MidiShortMessage.NoteOn(channel, number, velocity));

        public void Dispose()
        {
            lock (_gate)
            {
                MidiOut? midiOut = _midiOut;
                _midiOut = null;
                try
                {
                    midiOut?.Dispose();
                }
                catch (Exception)
                {
                    // The device may already be gone; the handle is released either way.
                }
            }
        }

        private void Send(int message)
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_midiOut is null, this);
                _midiOut.Send(message);
            }
        }
    }
}
