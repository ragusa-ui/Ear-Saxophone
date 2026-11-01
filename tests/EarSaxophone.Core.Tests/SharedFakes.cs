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

/// <summary>
/// A clock that only moves when the test says so. Its timers fire only inside <see cref="Advance"/>, on the test thread, in the
/// order of their due times (with the clock set to that time), so timer-driven code is deterministic.
/// </summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <summary>Timers that are armed right now (not disposed and with a due time).</summary>
    public int ActiveTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(t => t.Due is not null);
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        DateTimeOffset end;
        lock (_gate)
        {
            end = _now + by;
        }

        for (int guard = 0; guard < 100_000; guard++)
        {
            ManualTimer? next;
            lock (_gate)
            {
                next = _timers.Where(t => t.Due is { } due && due <= end).OrderBy(t => t.Due).FirstOrDefault();
                if (next is null)
                {
                    break;
                }

                if (next.Due > _now)
                {
                    _now = next.Due!.Value;
                }

                next.Due = next.Period is { } period && period > TimeSpan.Zero ? _now + period : null;
            }

            next.Fire();
        }

        lock (_gate)
        {
            _now = end;
        }
    }

    public void AdvanceMs(int milliseconds) => Advance(TimeSpan.FromMilliseconds(milliseconds));

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; set; }

        public TimeSpan? Period { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                if (!clock._timers.Contains(this))
                {
                    return false;
                }

                Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + (dueTime < TimeSpan.Zero ? TimeSpan.Zero : dueTime);
                Period = period == Timeout.InfiniteTimeSpan ? null : period;
                return true;
            }
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (clock._gate)
            {
                clock._timers.Remove(this);
                Due = null;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
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
