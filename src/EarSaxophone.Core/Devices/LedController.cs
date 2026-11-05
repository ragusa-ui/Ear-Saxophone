using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Devices;

/// <summary>
/// Shows mute states on the key LEDs of the device: a key whose mapping contains a mute action is "muted-lit" while its target
/// (with several targets: the group, see <see cref="IsLit"/>) is muted, every other LED key is dark. With <see cref="Blink"/> (the
/// default) muted-lit keys blink: all of them together, <see cref="LedOptions.BlinkHalfPeriod"/> on and as long off; the rhythm
/// restarts with the on phase whenever another key becomes muted, so a press shows at once. Only changes are sent (one message per
/// blinking key and phase change, from an own one-shot timer that runs only while something blinks); after the output was
/// (re)opened, after a profile change and after switching the feature on, all LED keys are sent once (the device in LED mode
/// "External" shows nothing by itself). Disconnecting sends nothing. Thread-safe: <see cref="Poll"/> runs on the tick,
/// <see cref="Refresh"/> after key actions on the MIDI worker, the blink timer on the thread pool.
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
    private readonly HashSet<ControlId> _muted = [];
    private readonly List<(ControlId Control, byte Value, DateTimeOffset At)> _recent = [];
    private Profile _profile = new(string.Empty, []);
    private bool _enabled = true;
    private bool _blink = true;
    private DateTimeOffset _blinkStart;
    private ITimer? _blinkTimer;
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
                _muted.Clear();
                StopBlinkLocked();
                _output.Poll(false);
            }
        }
    }

    /// <summary>
    /// Setting "LEDs blinken bei Stumm" (default on). Off: muted keys are lit steadily and the blink timer stops; on: they blink,
    /// starting with the on phase. Changes what the device shows at once (when the output is open).
    /// </summary>
    public bool Blink
    {
        get
        {
            lock (_gate)
            {
                return _blink;
            }
        }

        set
        {
            lock (_gate)
            {
                if (_disposed || _blink == value)
                {
                    return;
                }

                _blink = value;
                DateTimeOffset now = _time.GetUtcNow();
                _blinkStart = now;
                if (_enabled && _output.IsOpen && _output.Generation == _syncedGeneration && ApplyLocked(now, MutedControls()))
                {
                    ScheduleBlinkLocked(now);
                }
                else
                {
                    StopBlinkLocked();
                }
            }
        }
    }

    /// <summary>
    /// Whether the key is muted-lit for this profile (steady or blinking): its mapping has at least one (valid) mute action and the
    /// targets of all its mute actions are muted as a group (every readable target muted; the same rule a group toggle uses to
    /// decide to unmute). "Wie Fader" targets count with the fader's current targets (<see cref="StripTargets"/>); one that
    /// resolves to nothing does not count.
    /// </summary>
    public static bool IsLit(Profile profile, ControlId control, Func<IReadOnlyList<AudioTarget>, bool?> readGroupMuted)
    {
        if (profile.Find(control) is not { } stored)
        {
            return false;
        }

        ControlMapping mapping = StripTargets.Resolve(profile, stored);
        List<AudioTarget> targets = mapping.Actions.OfType<MuteAction>().Where(a => a.IsValid && !a.Target.IsStrip).Select(a => a.Target).Distinct().ToList();
        return targets.Count > 0 && readGroupMuted(targets) == true;
    }

    /// <summary>
    /// Whether the volume targets of a fader or knob are all muted right now (every readable target muted), for the picture.
    /// False when the control has no volume action; "Wie Fader" targets are resolved like in <see cref="IsLit"/>.
    /// </summary>
    public static bool IsVolumeMuted(Profile profile, ControlId control, Func<IReadOnlyList<AudioTarget>, bool?> readGroupMuted)
    {
        if (profile.Find(control) is not { } stored)
        {
            return false;
        }

        ControlMapping mapping = StripTargets.Resolve(profile, stored);
        List<AudioTarget> targets = mapping.Actions.OfType<SetVolumeAction>().Where(a => a.IsValid && !a.Target.IsStrip).Select(a => a.Target).Distinct().ToList();
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
            StopBlinkLocked();
            _output.Dispose();
        }
    }

    private void SyncLocked()
    {
        if (!_enabled || !_output.IsOpen)
        {
            StopBlinkLocked();
            return;
        }

        if (_output.Generation != _syncedGeneration || _resync)
        {
            _shown.Clear();
            _muted.Clear(); // everything counts as newly muted: the blink starts with the on phase again
            _syncedGeneration = _output.Generation;
            _resync = false;
        }

        DateTimeOffset now = _time.GetUtcNow();
        var readable = new List<ControlId>(_options.Controls.Count);
        bool newlyMuted = false;
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

            readable.Add(control);
            if (!lit)
            {
                _muted.Remove(control);
            }
            else if (_muted.Add(control))
            {
                newlyMuted = true;
            }
        }

        if (newlyMuted)
        {
            _blinkStart = now; // all blinking keys restart together with the on phase
        }

        if (ApplyLocked(now, readable))
        {
            ScheduleBlinkLocked(now);
        }
        else
        {
            StopBlinkLocked(); // port gone; the reopen gets a new generation and sends everything again
        }
    }

    /// <summary>Sends what the given keys should show now (muted and in an on phase, or muted without blinking); false when the port failed.</summary>
    private bool ApplyLocked(DateTimeOffset now, IEnumerable<ControlId> controls)
    {
        bool phaseOn = !_blink || BlinkPhase.IsOn(_blinkStart, now, _options.BlinkHalfPeriod);
        foreach (ControlId control in controls)
        {
            bool on = phaseOn && _muted.Contains(control);
            if (_shown.TryGetValue(control, out bool shown) && shown == on)
            {
                continue;
            }

            if (!SendLocked(control, on))
            {
                return false;
            }

            _shown[control] = on;
        }

        return true;
    }

    private List<ControlId> MutedControls() => _options.Controls.Where(_muted.Contains).ToList();

    /// <summary>Arms the one-shot blink timer for the next phase change while something blinks; stops it otherwise.</summary>
    private void ScheduleBlinkLocked(DateTimeOffset now)
    {
        if (_disposed || !_enabled || !_blink || _muted.Count == 0 || _options.BlinkHalfPeriod <= TimeSpan.Zero)
        {
            StopBlinkLocked();
            return;
        }

        TimeSpan due = BlinkPhase.UntilNextChange(_blinkStart, now, _options.BlinkHalfPeriod);
        if (_blinkTimer is null)
        {
            _blinkTimer = _time.CreateTimer(OnBlinkTimer, null, due, Timeout.InfiniteTimeSpan);
        }
        else
        {
            _blinkTimer.Change(due, Timeout.InfiniteTimeSpan);
        }
    }

    private void StopBlinkLocked()
    {
        _blinkTimer?.Dispose();
        _blinkTimer = null;
    }

    // Thread pool: only the phase changes here; mute states are read by Poll and Refresh.
    private void OnBlinkTimer(object? state)
    {
        lock (_gate)
        {
            if (_disposed || !_enabled || !_blink)
            {
                return;
            }

            if (!_output.IsOpen || _output.Generation != _syncedGeneration)
            {
                StopBlinkLocked(); // closed or reopened: the next Poll sends everything again and restarts the blinking
                return;
            }

            DateTimeOffset now = _time.GetUtcNow();
            if (ApplyLocked(now, MutedControls()))
            {
                ScheduleBlinkLocked(now);
            }
            else
            {
                StopBlinkLocked();
            }
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
