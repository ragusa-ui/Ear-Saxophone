namespace EarSaxophone.Core.Midi;

/// <summary>
/// Takes MIDI messages from the driver callback and hands them to the handler on a worker thread, so the driver thread
/// never waits for audio or keyboard calls and an exception in the handler cannot end the process.
/// While the worker is busy (a fader sends about 100 messages per second), a pending message of the same control is replaced by
/// the newer one when both lie on the same side of the press threshold. Faders therefore never build a backlog, while a
/// button press followed quickly by its release keeps both messages (so no press is ever lost).
/// </summary>
public sealed class MidiMessagePump : IDisposable
{
    private readonly object _gate = new();
    private readonly LinkedList<MidiMessage> _queue = new();
    private readonly Dictionary<ControlId, LinkedListNode<MidiMessage>> _pendingByControl = [];
    private readonly Action<MidiMessage> _handler;
    private readonly Action<Exception>? _onError;
    private readonly Thread _worker;
    private bool _busy;
    private bool _disposed;

    public MidiMessagePump(Action<MidiMessage> handler, Action<Exception>? onError = null)
    {
        _handler = handler;
        _onError = onError;
        _worker = new Thread(Run) { IsBackground = true, Name = "MidiMessagePump" };
        _worker.Start();
    }

    public void Post(MidiMessage message)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            ControlId control = message.Control;
            if (_pendingByControl.TryGetValue(control, out LinkedListNode<MidiMessage>? pending)
                && IsDown(pending.Value) == IsDown(message))
            {
                pending.Value = message;
                return;
            }

            _pendingByControl[control] = _queue.AddLast(message);
            Monitor.Pulse(_gate);
        }
    }

    /// <summary>Waits until everything posted so far was handled. Mainly for tests; returns false on timeout.</summary>
    public bool WaitUntilIdle(TimeSpan timeout)
    {
        DateTime end = DateTime.UtcNow + timeout;
        lock (_gate)
        {
            while (_queue.Count > 0 || _busy)
            {
                TimeSpan left = end - DateTime.UtcNow;
                if (left <= TimeSpan.Zero || _disposed)
                {
                    return false;
                }

                Monitor.Wait(_gate, left);
            }

            return true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _queue.Clear();
            _pendingByControl.Clear();
            Monitor.PulseAll(_gate);
        }
    }

    private static bool IsDown(MidiMessage message) => message.Value >= Mapping.MappingDispatcher.PressThreshold;

    private void Run()
    {
        while (true)
        {
            MidiMessage message;
            lock (_gate)
            {
                while (_queue.Count == 0)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    Monitor.Wait(_gate);
                }

                LinkedListNode<MidiMessage> first = _queue.First!;
                message = first.Value;
                _queue.RemoveFirst();
                if (_pendingByControl.TryGetValue(message.Control, out LinkedListNode<MidiMessage>? tracked) && tracked == first)
                {
                    _pendingByControl.Remove(message.Control);
                }

                _busy = true;
            }

            try
            {
                _handler(message);
            }
            catch (Exception ex)
            {
                try
                {
                    _onError?.Invoke(ex);
                }
                catch (Exception)
                {
                    // Reporting must never stop the worker.
                }
            }
            finally
            {
                lock (_gate)
                {
                    _busy = false;
                    Monitor.PulseAll(_gate);
                }
            }
        }
    }
}
