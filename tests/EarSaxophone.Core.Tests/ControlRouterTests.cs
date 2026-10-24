using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class ControlRouterTests
{
    private static readonly ControlId Fader = new(ControlKind.ControlChange, 0, 0);

    private static MidiMessage Cc(byte number, byte value) => new(MidiMessageKind.ControlChange, 0, number, value);

    private static ControlRouter Create() => new(new Profile("p",
        [new ControlMapping(Fader, new SetVolumeAction(AudioTarget.Speakers))]));

    [Fact]
    public void MappedMessage_RaisesAction_AndIsObserved()
    {
        ControlRouter router = Create();
        var actions = new List<ActionInvocation>();
        var observed = new List<MidiMessage>();
        router.ActionRequested += actions.Add;
        router.MessageObserved += observed.Add;

        router.Handle(Cc(0, 127));
        router.Handle(Cc(9, 10)); // unmapped, observed only

        Assert.Single(actions);
        Assert.Equal(1f, actions[0].Level, 5);
        Assert.Equal(2, observed.Count);
    }

    [Fact]
    public void WhileLearning_MessageIdentifiesControl_AndDoesNotTriggerAction()
    {
        ControlRouter router = Create();
        var actions = new List<ActionInvocation>();
        var learned = new List<ControlId>();
        router.ActionRequested += actions.Add;
        router.ControlLearned += learned.Add;

        router.StartLearning();
        Assert.True(router.IsLearning);
        router.Handle(Cc(0, 100)); // mapped control, but learning wins
        router.Handle(Cc(0, 101)); // learning is over, now it is an action

        Assert.Equal([Fader], learned);
        Assert.False(router.IsLearning);
        Assert.Single(actions);
    }

    [Fact]
    public void CancelLearning_RestoresNormalDispatch()
    {
        ControlRouter router = Create();
        var actions = new List<ActionInvocation>();
        router.ActionRequested += actions.Add;

        router.StartLearning();
        router.CancelLearning();
        router.Handle(Cc(0, 50));

        Assert.Single(actions);
    }

    [Fact]
    public void SetProfile_ChangesMappings()
    {
        ControlRouter router = Create();
        var actions = new List<ActionInvocation>();
        router.ActionRequested += actions.Add;

        router.SetProfile(new Profile("empty", []));
        router.Handle(Cc(0, 50));

        Assert.Empty(actions);
    }
}
