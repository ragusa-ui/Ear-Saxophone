using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class MidiLearnSessionTests
{
    private static MidiMessage Cc(byte number, byte value) => new(MidiMessageKind.ControlChange, 0, number, value);

    [Fact]
    public void Inactive_CapturesNothing()
    {
        var learn = new MidiLearnSession();
        Assert.Null(learn.TryCapture(Cc(1, 100)));
    }

    [Fact]
    public void FirstMeaningfulMessage_IsCaptured_AndSessionEnds()
    {
        var learn = new MidiLearnSession();
        ControlId? raised = null;
        learn.Captured += c => raised = c;
        learn.Start();

        ControlId? captured = learn.TryCapture(Cc(5, 90));

        Assert.Equal(new ControlId(ControlKind.ControlChange, 0, 5), captured);
        Assert.Equal(captured, raised);
        Assert.False(learn.IsActive);
        Assert.Null(learn.TryCapture(Cc(6, 90)));
    }

    [Fact]
    public void ReleaseMessages_AreIgnored()
    {
        var learn = new MidiLearnSession();
        learn.Start();
        Assert.Null(learn.TryCapture(Cc(5, 0)));
        Assert.Null(learn.TryCapture(new MidiMessage(MidiMessageKind.NoteOff, 0, 60, 0)));
        Assert.True(learn.IsActive);
    }

    [Fact]
    public void Cancel_StopsLearning()
    {
        var learn = new MidiLearnSession();
        learn.Start();
        learn.Cancel();
        Assert.Null(learn.TryCapture(Cc(5, 90)));
    }
}
