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
        router.ActionsRequested += actions.AddRange;
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
        router.ActionsRequested += actions.AddRange;
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
        router.ActionsRequested += actions.AddRange;

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
        router.ActionsRequested += actions.AddRange;

        router.SetProfile(new Profile("empty", []));
        router.Handle(Cc(0, 50));

        Assert.Empty(actions);
    }

    [Fact]
    public void SeveralActions_ArriveTogether_InOneEvent()
    {
        var router = new ControlRouter(new Profile("p",
            [new ControlMapping(Fader, [new SetVolumeAction(AudioTarget.Speakers), new SetVolumeAction(AudioTarget.Microphone)])]));
        var events = new List<IReadOnlyList<ActionInvocation>>();
        router.ActionsRequested += events.Add;

        router.Handle(Cc(0, 50));
        router.Handle(Cc(9, 50));

        IReadOnlyList<ActionInvocation> single = Assert.Single(events);
        Assert.Equal(2, single.Count);
        Assert.All(single, i => Assert.Equal(50 / 127f, i.Level, 5));
    }

    [Fact]
    public void KeyRelease_OfAToggle_RaisesNoEvent_ButPushToTalkDoes()
    {
        var toggle = new ControlId(ControlKind.ControlChange, 0, 48);
        var ptt = new ControlId(ControlKind.ControlChange, 0, 49);
        var router = new ControlRouter(new Profile("p",
        [
            new ControlMapping(toggle, new MuteAction(AudioTarget.Speakers)),
            new ControlMapping(ptt, new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld)),
        ]));
        var events = new List<IReadOnlyList<ActionInvocation>>();
        router.ActionsRequested += events.Add;

        router.Handle(Cc(48, 127));
        router.Handle(Cc(48, 0));
        router.Handle(Cc(49, 127));
        router.Handle(Cc(49, 0));

        Assert.Equal([ActionPhase.Pressed, ActionPhase.Pressed, ActionPhase.Released], events.Select(e => Assert.Single(e).Phase));
    }
}
