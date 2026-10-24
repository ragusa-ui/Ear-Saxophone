using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Midi;
using NAudio.Midi;
using IMidiInput = EarSaxophone.Core.Midi.IMidiInput;
using MidiMessage = EarSaxophone.Core.Midi.MidiMessage;

namespace EarSaxophone.Windows.Midi;

/// <summary>WinMM MIDI input via NAudio. Compiled in CI; behaviour with real hardware must be verified locally.</summary>
public sealed class NAudioMidiInputProvider : IMidiInputProvider
{
    public IReadOnlyList<MidiDeviceInfo> GetDevices()
    {
        var devices = new List<MidiDeviceInfo>();
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            devices.Add(new MidiDeviceInfo(i, MidiIn.DeviceInfo(i).ProductName));
        }

        return devices;
    }

    public IMidiInput Open(MidiDeviceInfo device) => new NAudioMidiInput(device.Index);

    private sealed class NAudioMidiInput : IMidiInput
    {
        private readonly MidiIn _midiIn;

        public NAudioMidiInput(int deviceIndex)
        {
            _midiIn = new MidiIn(deviceIndex);
            _midiIn.MessageReceived += OnMessage;
            _midiIn.ErrorReceived += OnError;
        }

        public event Action<MidiMessage>? MessageReceived;

        public event Action<string>? Faulted;

        public void Start() => _midiIn.Start();

        public void Stop() => _midiIn.Stop();

        public void Dispose()
        {
            _midiIn.MessageReceived -= OnMessage;
            _midiIn.ErrorReceived -= OnError;
            _midiIn.Dispose();
        }

        private void OnMessage(object? sender, MidiInMessageEventArgs e)
        {
            if (MidiMessageParser.TryParse(e.RawMessage, out MidiMessage message))
            {
                MessageReceived?.Invoke(message);
            }
        }

        private void OnError(object? sender, MidiInMessageEventArgs e) =>
            Faulted?.Invoke(Texts.MidiDriverWarning(e.RawMessage));
    }
}
