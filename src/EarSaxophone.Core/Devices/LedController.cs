using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Devices;

/// <summary>
/// Shows mute states on the key LEDs of the device: a key whose mapping contains a mute action is lit while its target (with
/// several targets: the group, see <see cref="IsLit"/>) is muted, every other LED key is dark. Only changes are sent; after the
/// output was (re)opened, after a profile change and after switching the feature on, all LED keys are sent once (the device in
/// LED mode "External" shows nothing by itself). Disconnecting sends nothing. Thread-safe: <see cref="Poll"/> runs on the timer,
/// <see cref="Refresh"/> after key actions on the MIDI worker.
/// </summary>
public sealed class LedController : IDisposable
{
    private readonly object _gate = new();
    private readonly object _echoGate = new();
    private readonly MidiOutputConnection _output;
    private readonly Func<IReadOnlyList<AudioTarget>, bool?> _readGroupMuted;
    private readonly LedOptions _options;
    private readonly TimeProvider _time;
    private readonly Dictionary<ControlId, bool> _shown = [];
    private readonly List<(ControlId Control, byte Value, DateTimeOffset At)> _recent = [];
    private Profile _profile = new(string.Empty, []);
    private bool _enabled = true;
    private bool _resync = true;
    private int _syncedGeneration = -1;
    private int? _inputGeneration;
    private bool _disposed;

    /// <param name="readGroupMuted">Mute state of a group of targets: true = all muted, false = something audible, null = unknown.</param>
    public LedController(MidiOutputConnection output, Func<IReadOnlyList<AudioTarget>, bool?> readGroupMuted, LedOptions? options = null, TimeProvider? time = null)
    {
        _output = output;
        _readGroupMuted = readGroupMuted;
        _options = options ?? LedOptions.Default;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Port problems and the opened port, for the status line.</summary>
    public event Action<string>? StatusChanged
    {
        add => _output.StatusChanged += value;
        remove => _output.StatusChanged -= value;
    }

    /// <summary>
    /// Setting "LEDs am Gerät anzeigen". Off: the LEDs this controller lit are switched off once, the port is closed and nothing
    /// is sent afterwards. On: the next <see cref="Poll"/> opens the port and sends every LED key.
    /// </summary>
    public bool Enabled
    {
        get
        {
            lock (_gate)
            {
                return _enabled;
            }
        }

        set
        {
            lock (_gate)
            {
                if (_disposed || _enabled == value)
                {
                    return;
                }

                _enabled = value;
                if (value)
                {
                    _resync = true;
                    return;
                }

                TurnOffLitLocked();
                _output.Poll(false);
            }
        }
    }

    /// <summary>
    /// Whether the key is lit for this profile: its mapping has at least one (valid) mute action and the targets of all its mute
    /// actions are muted as a group (every readable target muted; the same rule a group toggle uses to decide to unmute).
    /// </summary>
    public static bool IsLit(Profile profile, ControlId control, Func<IReadOnlyList<AudioTarget>, bool?> readGroupMuted)
    {
        if (profile.Find(control) is not { } mapping)
        {
            return false;
        }

        List<AudioTarget> targets = mapping.Actions.OfType<MuteAction>().Where(a => a.IsValid).Select(a => a.Target).Distinct().ToList();
        return targets.Count > 0 && readGroupMuted(targets) == true;
    }

    /// <summary>Uses the mappings of this profile from now on; every LED key is sent again with the next sync.</summary>
    public void SetProfile(Profile profile)
    {
        lock (_gate)
        {
            _profile = profile;
            _resync = true;
        }
    }

    /// <summary>
    /// Called about once per second: opens or closes the output next to the input (a new <paramref name="inputGeneration"/> means
    /// the input was reopened, so the output is reopened too) and brings the LEDs up to date, which also follows mute changes made
    /// elsewhere (Windows mixer). Never throws.
    /// </summary>
    public void Poll(bool inputConnected, int inputGeneration)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_inputGeneration != inputGeneration)
            {
                if (_inputGeneration is not null)
                {
                    _output.Poll(false); // the input was reopened (sleep, USB): do not trust the old output handle either
                }

                _inputGeneration = inputGeneration;
            }

            _output.Poll(inputConnected && _enabled);
            SyncLocked();
        }
    }

    /// <summary>Brings the LEDs up to date right away (after a key action) without touching the port.</summary>
    public void Refresh()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                SyncLocked();
            }
        }
    }

    /// <summary>
    /// True (once) when a message from the device equals an LED message this controller sent within <see cref="LedOptions.EchoWindow"/>;
    /// such a message is our own output looped back and must not be treated as a key press.
    /// </summary>
    public bool IsOwnEcho(MidiMessage message)
    {
        lock (_echoGate) // own lock: the driver thread never waits for a sync that reads audio states
        {
            if (_recent.Count == 0)
            {
                return false;
            }

            PruneLocked();
            int index = _recent.FindIndex(r => r.Control == message.Control && r.Value == message.Value);
            if (index < 0)
            {
                return false;
            }

            _recent.RemoveAt(index);
            return true;
        }
    }

    /// <summary>Switches the LEDs this controller lit off (best effort) and closes the port.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_enabled)
            {
                TurnOffLitLocked();
            }

            _disposed = true;
            _output.Dispose();
        }
    }

    private void SyncLocked()
    {
        if (!_enabled || !_output.IsOpen)
        {
            return;
        }

        if (_output.Generation != _syncedGeneration || _resync)
        {
            _shown.Clear();
            _syncedGeneration = _output.Generation;
            _resync = false;
        }

        foreach (ControlId control in _options.Controls)
        {
            bool lit;
            try
            {
                lit = IsLit(_profile, control, _readGroupMuted);
            }
            catch (Exception)
            {
                continue; // state unreadable right now: keep what the LED shows
            }

            if (_shown.TryGetValue(control, out bool shown) && shown == lit)
            {
                continue;
            }

            if (!SendLocked(control, lit))
            {
                return; // port gone; the reopen gets a new generation and sends everything again
            }

            _shown[control] = lit;
        }
    }

    private void TurnOffLitLocked()
    {
        foreach (ControlId control in _shown.Where(s => s.Value).Select(s => s.Key).ToList())
        {
            if (!SendLocked(control, false))
            {
                break;
            }
        }

        _shown.Clear();
    }

    private bool SendLocked(ControlId control, bool lit)
    {
        byte value = lit ? _options.OnValue : _options.OffValue;
        bool echoFilter = _options.EchoWindow > TimeSpan.Zero;
        var entry = (control, value, _time.GetUtcNow());
        if (echoFilter)
        {
            lock (_echoGate)
            {
                PruneLocked();
                _recent.Add(entry); // recorded before sending, so an echo can never arrive first
            }
        }

        if (_output.TrySend(control, value))
        {
            return true;
        }

        if (echoFilter)
        {
            lock (_echoGate)
            {
                _recent.Remove(entry);
            }
        }

        return false;
    }

    // Call under _echoGate.
    private void PruneLocked()
    {
        DateTimeOffset oldest = _time.GetUtcNow() - _options.EchoWindow;
        _recent.RemoveAll(r => r.At < oldest);
    }
}
