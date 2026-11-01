using EarSaxophone.Core.Localization;

namespace EarSaxophone.Core.Midi;

/// <summary>
/// Keeps the MIDI output port of the device open while it is wanted (the input is connected and LED feedback is on). It is
/// completely separate from <see cref="MidiConnectionManager"/>: nothing that goes wrong here can touch the input. Problems are
/// reported through <see cref="StatusChanged"/>, each text only once in a row, never as an exception. Closing sends nothing.
/// </summary>
public sealed class MidiOutputConnection(IMidiOutputProvider provider, string portFragment) : IDisposable
{
    private readonly object _gate = new();
    private IMidiOutput? _output;
    private int _index = -1;
    private string? _lastStatus;
    private string? _failedPort;
    private bool _disposed;

    public string PortFragment { get; } = portFragment;

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                return _output is not null;
            }
        }
    }

    public string? PortName { get; private set; }

    /// <summary>Counts successful opens; a new value means the device forgot what it showed and needs everything again.</summary>
    public int Generation { get; private set; }

    /// <summary>A text for the status line (not an error): port opened, not found, could not be opened, sending failed.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// Opens the port when <paramref name="wanted"/> and it is not open (or its name/index vanished from the list), closes it
    /// when not wanted. Call it about once per second; it never throws.
    /// </summary>
    public void Poll(bool wanted)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (!wanted)
            {
                CloseLocked();
                _lastStatus = null;
                _failedPort = null;
                return;
            }

            IReadOnlyList<MidiDeviceInfo> devices;
            try
            {
                devices = provider.GetDevices();
            }
            catch (Exception ex)
            {
                CloseLocked();
                Report(Texts.LedOutputListFailed(ex.Message));
                return;
            }

            if (_output is not null)
            {
                if (devices.Any(d => d.Name == PortName && d.Index == _index))
                {
                    return;
                }

                CloseLocked();
            }

            MidiDeviceInfo? target = devices.FirstOrDefault(d => d.Name.Contains(PortFragment, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                _failedPort = null;
                Report(Texts.LedOutputNotFound(PortFragment));
                return;
            }

            try
            {
                _output = provider.Open(target);
            }
            catch (Exception ex)
            {
                Report(Texts.LedOutputOpenFailed(target.Name, ex.Message));
                return;
            }

            PortName = target.Name;
            _index = target.Index;
            Generation++;
            if (_failedPort != target.Name)
            {
                Report(Texts.LedOutputOpened(target.Name)); // after a send failure the reopen stays quiet, so a flaky port cannot flood the status line
            }
        }
    }

    /// <summary>
    /// Sends one LED value (Control-Change for CC controls, Note-On for note controls). Returns false when the port is not open
    /// or sending failed; a failed port is closed and opened again by the next <see cref="Poll"/>.
    /// </summary>
    public bool TrySend(ControlId control, byte value)
    {
        lock (_gate)
        {
            if (_output is null)
            {
                return false;
            }

            try
            {
                if (control.Kind == ControlKind.ControlChange)
                {
                    _output.SendControlChange(control.Channel, control.Number, value);
                }
                else
                {
                    _output.SendNoteOn(control.Channel, control.Number, value);
                }

                return true;
            }
            catch (Exception ex)
            {
                string port = PortName ?? PortFragment;
                CloseLocked();
                _failedPort = port;
                Report(Texts.LedSendFailed(port, ex.Message));
                return false;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            CloseLocked();
        }
    }

    private void CloseLocked()
    {
        if (_output is null)
        {
            return;
        }

        try
        {
            _output.Dispose();
        }
        catch (Exception)
        {
            // The device may already be gone.
        }

        _output = null;
        PortName = null;
        _index = -1;
    }

    private void Report(string text)
    {
        if (text == _lastStatus)
        {
            return;
        }

        _lastStatus = text;
        try
        {
            StatusChanged?.Invoke(text);
        }
        catch (Exception)
        {
            // A status text is never worth breaking the LED connection.
        }
    }
}
