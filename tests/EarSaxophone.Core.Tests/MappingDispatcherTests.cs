using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class MappingDispatcherTests
{
    private static readonly ControlId Fader = new(ControlKind.ControlChange, 0, 0);
    private static readonly ControlId Button = new(ControlKind.ControlChange, 0, 32);

    private static MidiMessage Cc(ControlId id, byte value) =>
        new(MidiMessageKind.ControlChange, id.Channel, id.Number, value);

    private static MappingDispatcher Create() => new(new Profile("p",
    [
        new ControlMapping(Fader, new SetVolumeAction(AudioTarget.Speakers), new ValueScaling(Invert: false)),
        new ControlMapping(Button, new MediaAction(MediaCommand.PlayPause)),
    ]));

    [Fact]
    public void UnmappedControl_ProducesNothing() =>
        Assert.Null(Create().Dispatch(Cc(new ControlId(ControlKind.ControlChange, 0, 99), 100)));

    [Fact]
    public void ContinuousAction_FiresOnEveryMessageWithScaledLevel()
    {
        MappingDispatcher dispatcher = Create();
        Assert.Equal(1f, dispatcher.Dispatch(Cc(Fader, 127))!.Level, 5);
        Assert.Equal(0f, dispatcher.Dispatch(Cc(Fader, 0))!.Level, 5);
        Assert.Equal(1f, dispatcher.Dispatch(Cc(Fader, 127))!.Level, 5);
    }

    [Fact]
    public void ContinuousAction_UsesInvertedScaling()
    {
        var dispatcher = new MappingDispatcher(new Profile("p",
            [new ControlMapping(Fader, new SetVolumeAction(AudioTarget.Microphone), new ValueScaling(Invert: true))]));
        Assert.Equal(0f, dispatcher.Dispatch(Cc(Fader, 127))!.Level, 5);
    }

    [Fact]
    public void TriggerAction_FiresOnceOnPress_NotOnRepeatOrRelease()
    {
        MappingDispatcher dispatcher = Create();
        Assert.NotNull(dispatcher.Dispatch(Cc(Button, 127)));
        Assert.Null(dispatcher.Dispatch(Cc(Button, 127)));
        Assert.Null(dispatcher.Dispatch(Cc(Button, 0)));
        Assert.NotNull(dispatcher.Dispatch(Cc(Button, 127)));
    }

    [Fact]
    public void SetProfile_ResetsPressedState()
    {
        MappingDispatcher dispatcher = Create();
        dispatcher.Dispatch(Cc(Button, 127));
        dispatcher.SetProfile(new Profile("p2", [new ControlMapping(Button, new MediaAction(MediaCommand.Next))]));
        Assert.NotNull(dispatcher.Dispatch(Cc(Button, 127)));
    }
}
