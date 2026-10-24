using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class MidiConnectionManagerTests
{
    private sealed class FakeInput : IMidiInput
    {
        public bool Started, Stopped, Disposed;

        public event Action<MidiMessage>? MessageReceived;

        public event Action<string>? Faulted;

        public void Start()
        {
            if (FailOnStart)
            {
                throw new InvalidOperationException("device busy");
            }

            Started = true;
        }

        public bool FailOnStart { get; set; }

        public void Stop() => Stopped = true;

        public void Dispose() => Disposed = true;

        public void Raise(MidiMessage m) => MessageReceived?.Invoke(m);

        public void Fault(string text) => Faulted?.Invoke(text);
    }

    private sealed class FakeProvider : IMidiInputProvider
    {
        public List<MidiDeviceInfo> Devices { get; } = [];

        public List<FakeInput> Opened { get; } = [];

        public bool FailOpen { get; set; }

        public bool FailStart { get; set; }

        public bool FailList { get; set; }

        public IReadOnlyList<MidiDeviceInfo> GetDevices() =>
            FailList ? throw new InvalidOperationException("driver error") : Devices.ToList();

        public IMidiInput Open(MidiDeviceInfo device)
        {
            if (FailOpen)
            {
                throw new InvalidOperationException("in use");
            }

            var input = new FakeInput { FailOnStart = FailStart };
            Opened.Add(input);
            return input;
        }
    }

    private static readonly MidiMessage Sample = new(MidiMessageKind.ControlChange, 0, 1, 64);

    [Fact]
    public void DeviceMissing_StaysDisconnected_WithHint_AndDoesNotRepeatEvents()
    {
        var provider = new FakeProvider();
        using var manager = new MidiConnectionManager(provider);
        var events = new List<string>();
        manager.StateChanged += (_, text) => events.Add(text);

        manager.Poll();
        manager.Poll();
        manager.Poll();

        Assert.Equal(MidiConnectionState.Disconnected, manager.State);
        Assert.Single(events);
        Assert.Contains("nicht gefunden", events[0]);
    }

    [Fact]
    public void DevicePresent_Connects_StartsInput_AndForwardsMessages()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);
        var received = new List<MidiMessage>();
        manager.MessageReceived += received.Add;

        manager.Poll();
        provider.Opened[0].Raise(Sample);

        Assert.Equal(MidiConnectionState.Connected, manager.State);
        Assert.Equal("nanoKONTROL2", manager.ConnectedDeviceName);
        Assert.True(provider.Opened[0].Started);
        Assert.Equal([Sample], received);
    }

    [Fact]
    public void NameMatch_IsCaseInsensitive_AndIgnoresOtherDevices()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(0, "Some Keyboard"));
        provider.Devices.Add(new MidiDeviceInfo(1, "2- NANOKONTROL2"));
        using var manager = new MidiConnectionManager(provider);

        manager.Poll();

        Assert.Equal(MidiConnectionState.Connected, manager.State);
        Assert.Equal("2- NANOKONTROL2", manager.ConnectedDeviceName);
    }

    [Fact]
    public void RepeatedPolls_DoNotOpenTheDeviceTwice()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);

        manager.Poll();
        manager.Poll();
        manager.Poll();

        Assert.Single(provider.Opened);
    }

    [Fact]
    public void Unplug_ClosesInput_ThenReplugAtOtherIndex_Reconnects()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);
        var states = new List<MidiConnectionState>();
        manager.StateChanged += (s, _) => states.Add(s);
        var received = new List<MidiMessage>();
        manager.MessageReceived += received.Add;

        manager.Poll();
        FakeInput first = provider.Opened[0];
        provider.Devices.Clear();
        manager.Poll();

        Assert.Equal(MidiConnectionState.Disconnected, manager.State);
        Assert.True(first.Stopped && first.Disposed);

        provider.Devices.Add(new MidiDeviceInfo(3, "nanoKONTROL2"));
        manager.Poll();
        first.Raise(Sample); // old input must no longer deliver
        provider.Opened[1].Raise(Sample);

        Assert.Equal(MidiConnectionState.Connected, manager.State);
        Assert.Equal(2, provider.Opened.Count);
        Assert.Single(received);
        Assert.Equal(
            [MidiConnectionState.Connected, MidiConnectionState.Disconnected, MidiConnectionState.Disconnected, MidiConnectionState.Connected],
            states);
    }

    [Fact]
    public void ChangedPortIndex_ReopensTheDevice_BecauseTheOldHandleCannotBeTrusted()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(1, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);
        manager.Poll();
        FakeInput first = provider.Opened[0];

        provider.Devices.Clear();
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2")); // another MIDI device went away, numbering shifted
        manager.Poll();

        Assert.Equal(MidiConnectionState.Connected, manager.State);
        Assert.True(first.Disposed);
        Assert.Equal(2, provider.Opened.Count);
    }

    [Fact]
    public void Reconnect_ClosesAndReopens_EvenThoughTheDeviceStillLooksFine()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);
        var received = new List<MidiMessage>();
        manager.MessageReceived += received.Add;
        manager.Poll();
        FakeInput stale = provider.Opened[0];

        manager.Reconnect();
        stale.Raise(Sample);          // the stale handle must be silent
        provider.Opened[1].Raise(Sample);

        Assert.Equal(MidiConnectionState.Connected, manager.State);
        Assert.True(stale.Disposed);
        Assert.Equal(2, provider.Opened.Count);
        Assert.Single(received);
    }

    [Fact]
    public void Reconnect_WithoutDevice_StaysDisconnected()
    {
        var provider = new FakeProvider();
        using var manager = new MidiConnectionManager(provider);

        manager.Reconnect();

        Assert.Equal(MidiConnectionState.Disconnected, manager.State);
    }

    [Fact]
    public void OpenFailure_IsReported_AndRetriedOnNextPoll()
    {
        var provider = new FakeProvider { FailOpen = true };
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);
        string? detail = null;
        manager.StateChanged += (_, text) => detail = text;

        manager.Poll();
        Assert.Equal(MidiConnectionState.Disconnected, manager.State);
        Assert.Contains("nicht geöffnet", detail);

        provider.FailOpen = false;
        manager.Poll();
        Assert.Equal(MidiConnectionState.Connected, manager.State);
    }

    [Fact]
    public void StartFailure_DisposesTheInput_AndStaysDisconnected()
    {
        var provider = new FakeProvider { FailStart = true };
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);

        manager.Poll();

        Assert.Equal(MidiConnectionState.Disconnected, manager.State);
        Assert.True(provider.Opened[0].Disposed);
    }

    [Fact]
    public void ListingFailure_DisconnectsWithoutThrowing()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);
        manager.Poll();

        provider.FailList = true;
        manager.Poll();

        Assert.Equal(MidiConnectionState.Disconnected, manager.State);
        Assert.True(provider.Opened[0].Disposed);
    }

    [Fact]
    public void DriverFault_RaisesWarning_ButKeepsConnection()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        using var manager = new MidiConnectionManager(provider);
        string? warning = null;
        manager.Warning += w => warning = w;
        manager.Poll();

        provider.Opened[0].Fault("bad message");

        Assert.Equal("bad message", warning);
        Assert.Equal(MidiConnectionState.Connected, manager.State);
    }

    [Fact]
    public void Dispose_ClosesInput_AndLaterPollsDoNothing()
    {
        var provider = new FakeProvider();
        provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        var manager = new MidiConnectionManager(provider);
        manager.Poll();

        manager.Dispose();
        manager.Poll();

        Assert.True(provider.Opened[0].Disposed);
        Assert.Single(provider.Opened);
    }
}
