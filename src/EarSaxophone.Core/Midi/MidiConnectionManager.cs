using EarSaxophone.Core.Localization;
namespace EarSaxophone.Core.Midi;

public enum MidiConnectionState
{
    Disconnected,
    Connected,
}

/// <summary>
/// Keeps a connection to one MIDI device (matched by a name fragment, because port indexes change) and reconnects
/// automatically. Call <see cref="Poll"/> periodically (about once per second); WinMM raises no event when a USB
/// device disappears, so the device list is compared on every poll. A device that is unplugged and replugged
/// between two polls keeps its name and index and is therefore not noticed until a local test shows otherwise.
/// </summary>
public sealed class MidiConnectionManager(IMidiInputProvider provider, string deviceNameFragment = "nanoKONTROL2") : IDisposable
{
    private readonly object _gate = new();
    private IMidiInput? _input;
    private string? _lastDetail;
    private int _connectedIndex = -1;
    private bool _disposed;

    public MidiConnectionState State { get; private set; } = MidiConnectionState.Disconnected;

    public string? ConnectedDeviceName { get; private set; }

    /// <summary>Raised on a driver thread for every parsed message of the connected device.</summary>
    public event Action<MidiMessage>? MessageReceived;

    /// <summary>Raised when the state or the user-facing detail text changes (never on repeated identical polls).</summary>
    public event Action<MidiConnectionState, string>? StateChanged;

    /// <summary>Raised when the driver reports a problem on an otherwise working connection.</summary>
    public event Action<string>? Warning;

    public void Poll()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            IReadOnlyList<MidiDeviceInfo> devices;
            try
            {
                devices = provider.GetDevices();
            }
            catch (Exception ex)
            {
                CloseInput();
                SetState(MidiConnectionState.Disconnected, Texts.MidiListFailed(ex.Message));
                return;
            }

            if (_input is not null)
            {
                // Same name AND same port index: if the index changed (another MIDI device was added or removed) or the name
                // vanished, the open handle cannot be trusted; reopen.
                if (devices.Any(d => d.Name == ConnectedDeviceName && d.Index == _connectedIndex))
                {
                    return;
                }

                CloseInput();
                SetState(MidiConnectionState.Disconnected, Texts.MidiDisconnected(deviceNameFragment));
            }

            MidiDeviceInfo? target = devices.FirstOrDefault(d => d.Name.Contains(deviceNameFragment, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                SetState(MidiConnectionState.Disconnected, Texts.MidiNotFound(deviceNameFragment));
                return;
            }

            try
            {
                IMidiInput input = provider.Open(target);
                input.MessageReceived += ForwardMessage;
                input.Faulted += ForwardWarning;
                try
                {
                    input.Start();
                }
                catch
                {
                    input.MessageReceived -= ForwardMessage;
                    input.Faulted -= ForwardWarning;
                    input.Dispose();
                    throw;
                }

                _input = input;
                ConnectedDeviceName = target.Name;
                _connectedIndex = target.Index;
                SetState(MidiConnectionState.Connected, Texts.MidiConnected(target.Name));
            }
            catch (Exception ex)
            {
                SetState(MidiConnectionState.Disconnected,
                    Texts.MidiOpenFailed(target.Name, ex.Message));
            }
        }
    }

    /// <summary>
    /// Drops the current connection and opens the device again right away. For situations the device list cannot reveal:
    /// return from sleep, USB re-enumeration, a very short unplug, or the "reconnect" button.
    /// </summary>
    public void Reconnect()
    {
        lock (_gate)
        {
            CloseInput();
        }

        Poll();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            CloseInput();
            State = MidiConnectionState.Disconnected;
        }
    }

    private void ForwardMessage(MidiMessage message) => MessageReceived?.Invoke(message);

    private void ForwardWarning(string text) => Warning?.Invoke(text);

    private void CloseInput()
    {
        if (_input is null)
        {
            return;
        }

        _input.MessageReceived -= ForwardMessage;
        _input.Faulted -= ForwardWarning;
        try
        {
            _input.Stop();
        }
        catch (Exception)
        {
            // The device may already be gone; disposing is what matters.
        }

        try
        {
            _input.Dispose();
        }
        catch (Exception)
        {
        }

        _input = null;
        ConnectedDeviceName = null;
        _connectedIndex = -1;
    }

    private void SetState(MidiConnectionState state, string detail)
    {
        bool changed = state != State || detail != _lastDetail;
        State = state;
        _lastDetail = detail;
        if (changed)
        {
            StateChanged?.Invoke(state, detail);
        }
    }
}
