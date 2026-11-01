using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Mapping;

/// <summary>
/// Single entry point for incoming MIDI messages. While MIDI-Learn is active, messages are used to identify a
/// control and do not trigger actions; otherwise they are dispatched against the active profile.
/// Thread-safe: messages arrive on a driver thread while the UI starts/cancels learning.
/// </summary>
public sealed class ControlRouter
{
    private readonly object _gate = new();
    private readonly MappingDispatcher _dispatcher;
    private readonly MidiLearnSession _learn = new();

    public ControlRouter(Profile profile)
    {
        _dispatcher = new MappingDispatcher(profile);
    }

    /// <summary>
    /// A mapped control produced actions: all executions of one message (one mapping, never empty), so they can run
    /// together (group mute). Raised on the thread that delivered the message.
    /// </summary>
    public event Action<IReadOnlyList<ActionInvocation>>? ActionsRequested;

    /// <summary>MIDI-Learn identified a control.</summary>
    public event Action<ControlId>? ControlLearned;

    /// <summary>Every parsed message, including unmapped ones (for a live monitor in the UI).</summary>
    public event Action<MidiMessage>? MessageObserved;

    public bool IsLearning
    {
        get
        {
            lock (_gate)
            {
                return _learn.IsActive;
            }
        }
    }

    public void StartLearning()
    {
        lock (_gate)
        {
            _learn.Start();
        }
    }

    public void CancelLearning()
    {
        lock (_gate)
        {
            _learn.Cancel();
        }
    }

    public void SetProfile(Profile profile)
    {
        lock (_gate)
        {
            _dispatcher.SetProfile(profile);
        }
    }

    public void Handle(MidiMessage message)
    {
        ControlId? learned;
        IReadOnlyList<ActionInvocation> invocations = [];
        bool wasLearning;
        lock (_gate)
        {
            wasLearning = _learn.IsActive;
            learned = _learn.TryCapture(message);
            if (!wasLearning)
            {
                invocations = _dispatcher.Dispatch(message);
            }
        }

        MessageObserved?.Invoke(message);
        if (learned is { } control)
        {
            ControlLearned?.Invoke(control);
        }

        if (invocations.Count > 0)
        {
            ActionsRequested?.Invoke(invocations);
        }
    }
}
