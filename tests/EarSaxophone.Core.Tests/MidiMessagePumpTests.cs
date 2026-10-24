using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class MidiMessagePumpTests
{
    private static MidiMessage Cc(byte number, byte value) => new(MidiMessageKind.ControlChange, 0, number, value);

    /// <summary>A handler that records messages and can be held on the first message to build a backlog.</summary>
    private sealed class Recorder
    {
        private readonly ManualResetEventSlim _release = new(initialState: true);
        private readonly ManualResetEventSlim _entered = new(initialState: false);

        public List<MidiMessage> Seen { get; } = [];

        public Exception? ThrowOnce { get; set; }

        public void Hold() => _release.Reset();

        public void Release() => _release.Set();

        public void WaitUntilHandlerEntered() => Assert.True(_entered.Wait(TimeSpan.FromSeconds(5)));

        public void Handle(MidiMessage message)
        {
            lock (Seen)
            {
                Seen.Add(message);
            }

            _entered.Set();
            _release.Wait(TimeSpan.FromSeconds(10));
            if (ThrowOnce is { } ex)
            {
                ThrowOnce = null;
                throw ex;
            }
        }

        public MidiMessage[] Snapshot()
        {
            lock (Seen)
            {
                return Seen.ToArray();
            }
        }
    }

    [Fact]
    public void MessagesOfDifferentControls_AreDeliveredOnceAndInOrder()
    {
        var recorder = new Recorder();
        using var pump = new MidiMessagePump(recorder.Handle);

        pump.Post(Cc(1, 10));
        pump.Post(Cc(2, 20));
        pump.Post(Cc(3, 30));

        Assert.True(pump.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        Assert.Equal([Cc(1, 10), Cc(2, 20), Cc(3, 30)], recorder.Snapshot());
    }

    [Fact]
    public void FaderFlood_WhileTheHandlerIsBusy_KeepsOnlyTheFirstAndTheNewestValue()
    {
        var recorder = new Recorder();
        recorder.Hold();
        using var pump = new MidiMessagePump(recorder.Handle);
        pump.Post(Cc(0, 1)); // the worker takes this and blocks
        recorder.WaitUntilHandlerEntered();

        for (byte v = 2; v < 60; v++)
        {
            pump.Post(Cc(0, v)); // all below the press threshold: replace each other
        }

        recorder.Release();

        Assert.True(pump.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        Assert.Equal([Cc(0, 1), Cc(0, 59)], recorder.Snapshot());
    }

    [Fact]
    public void ButtonPressAndRelease_AreNeverMergedAway()
    {
        var recorder = new Recorder();
        recorder.Hold();
        using var pump = new MidiMessagePump(recorder.Handle);
        pump.Post(Cc(9, 5)); // busy
        recorder.WaitUntilHandlerEntered();

        pump.Post(Cc(41, 127));
        pump.Post(Cc(41, 0));
        pump.Post(Cc(41, 127));
        recorder.Release();

        Assert.True(pump.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        Assert.Equal([Cc(9, 5), Cc(41, 127), Cc(41, 0), Cc(41, 127)], recorder.Snapshot());
    }

    [Fact]
    public void FaderCrossingThePressThreshold_KeepsOneMessageOnEachSide()
    {
        var recorder = new Recorder();
        recorder.Hold();
        using var pump = new MidiMessagePump(recorder.Handle);
        pump.Post(Cc(9, 5));
        recorder.WaitUntilHandlerEntered();

        pump.Post(Cc(0, 10));
        pump.Post(Cc(0, 20));
        pump.Post(Cc(0, 100));
        pump.Post(Cc(0, 110));
        recorder.Release();

        Assert.True(pump.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        Assert.Equal([Cc(9, 5), Cc(0, 20), Cc(0, 110)], recorder.Snapshot());
    }

    [Fact]
    public void ExceptionInTheHandler_IsReported_AndTheWorkerKeepsRunning()
    {
        var recorder = new Recorder { ThrowOnce = new InvalidOperationException("boom") };
        var errors = new List<Exception>();
        using var pump = new MidiMessagePump(recorder.Handle, errors.Add);

        pump.Post(Cc(1, 10));
        Assert.True(pump.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        pump.Post(Cc(2, 20));
        Assert.True(pump.WaitUntilIdle(TimeSpan.FromSeconds(5)));

        Assert.Equal("boom", Assert.Single(errors).Message);
        Assert.Equal([Cc(1, 10), Cc(2, 20)], recorder.Snapshot());
    }

    [Fact]
    public void FailingErrorCallback_DoesNotStopTheWorker()
    {
        var recorder = new Recorder { ThrowOnce = new InvalidOperationException("boom") };
        using var pump = new MidiMessagePump(recorder.Handle, _ => throw new InvalidOperationException("also broken"));

        pump.Post(Cc(1, 10));
        Assert.True(pump.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        pump.Post(Cc(2, 20));

        Assert.True(pump.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, recorder.Snapshot().Length);
    }

    [Fact]
    public void WaitUntilIdle_TimesOut_WhileTheHandlerIsStuck()
    {
        var recorder = new Recorder();
        recorder.Hold();
        var pump = new MidiMessagePump(recorder.Handle);
        pump.Post(Cc(1, 1));
        recorder.WaitUntilHandlerEntered();

        Assert.False(pump.WaitUntilIdle(TimeSpan.FromMilliseconds(100)));

        recorder.Release();
        pump.Dispose();
    }

    [Fact]
    public void AfterDispose_NothingIsDelivered()
    {
        var recorder = new Recorder();
        var pump = new MidiMessagePump(recorder.Handle);
        pump.Dispose();

        pump.Post(Cc(1, 10));
        Thread.Sleep(100);

        Assert.Empty(recorder.Snapshot());
    }
}
