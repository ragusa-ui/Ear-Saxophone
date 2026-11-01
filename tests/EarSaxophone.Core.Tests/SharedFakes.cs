using EarSaxophone.Core.Input;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

internal sealed class ImmediateUi : IUiThread
{
    public void Post(Action action) => action();
}

/// <summary>A clock that only moves when the test says so.</summary>
internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public void AdvanceMs(int milliseconds) => _now += TimeSpan.FromMilliseconds(milliseconds);
}

internal sealed class RecordingKeyboard : IKeyboardSender
{
    public List<MediaCommand> Media { get; } = [];

    public List<Hotkey> Hotkeys { get; } = [];

    public void SendMediaCommand(MediaCommand command) => Media.Add(command);

    public void SendHotkey(Hotkey hotkey) => Hotkeys.Add(hotkey);
}

internal sealed class FakeMidiPort : IMidiInput
{
    public event Action<MidiMessage>? MessageReceived;

    public event Action<string>? Faulted;

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }

    public void Send(byte cc, byte value, byte channel = 0) =>
        MessageReceived?.Invoke(new MidiMessage(MidiMessageKind.ControlChange, channel, cc, value));

    public void RaiseFault(string text) => Faulted?.Invoke(text);
}

internal sealed class FakeMidiProvider : IMidiInputProvider
{
    public List<MidiDeviceInfo> Devices { get; } = [];

    public List<FakeMidiPort> Ports { get; } = [];

    public IReadOnlyList<MidiDeviceInfo> GetDevices() => Devices.ToList();

    public IMidiInput Open(MidiDeviceInfo device)
    {
        var port = new FakeMidiPort();
        Ports.Add(port);
        return port;
    }
}

internal sealed class FakeAutostart : IAutostart
{
    public bool IsEnabled { get; private set; }

    public Exception? Failure { get; set; }

    public void SetEnabled(bool enabled)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        IsEnabled = enabled;
    }
}
