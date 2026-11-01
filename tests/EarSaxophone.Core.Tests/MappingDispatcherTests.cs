using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class MappingDispatcherTests
{
    private static readonly ControlId Fader = new(ControlKind.ControlChange, 0, 0);
    private static readonly ControlId Button = new(ControlKind.ControlChange, 0, 32);
    private static readonly ControlId Talk = new(ControlKind.ControlChange, 0, 48);
    private static readonly ControlId Silence = new(ControlKind.ControlChange, 0, 49);

    private static MidiMessage Cc(ControlId id, byte value) =>
        new(MidiMessageKind.ControlChange, id.Channel, id.Number, value);

    private static MappingDispatcher Create() => new(new Profile("p",
    [
        new ControlMapping(Fader, new SetVolumeAction(AudioTarget.Speakers), new ValueScaling(Invert: false)),
        new ControlMapping(Button, new MediaAction(MediaCommand.PlayPause)),
        new ControlMapping(Talk, new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld)),
        new ControlMapping(Silence, new MuteAction(AudioTarget.Speakers, MuteMode.MuteWhileHeld)),
    ]));

    [Fact]
    public void UnmappedControl_ProducesNothing() =>
        Assert.Empty(Create().Dispatch(Cc(new ControlId(ControlKind.ControlChange, 0, 99), 100)));

    [Fact]
    public void ContinuousAction_FiresOnEveryMessageWithScaledLevel_AndPhaseChanged()
    {
        MappingDispatcher dispatcher = Create();
        foreach ((byte value, float level) in new[] { ((byte)127, 1f), ((byte)0, 0f), ((byte)127, 1f), ((byte)127, 1f), ((byte)64, 64 / 127f) })
        {
            ActionInvocation invocation = Assert.Single(dispatcher.Dispatch(Cc(Fader, value)));
            Assert.Equal(level, invocation.Level, 5);
            Assert.Equal(ActionPhase.Changed, invocation.Phase);
            Assert.IsType<SetVolumeAction>(invocation.Action);
            Assert.Equal(Fader, invocation.Mapping.Control);
        }
    }

    [Fact]
    public void ContinuousAction_UsesInvertedScaling()
    {
        var dispatcher = new MappingDispatcher(new Profile("p",
            [new ControlMapping(Fader, new SetVolumeAction(AudioTarget.Microphone), new ValueScaling(Invert: true))]));
        Assert.Equal(0f, Assert.Single(dispatcher.Dispatch(Cc(Fader, 127))).Level, 5);
    }

    [Fact]
    public void TriggerAction_FiresOnceOnPress_NotOnRepeatOrRelease()
    {
        MappingDispatcher dispatcher = Create();
        ActionInvocation press = Assert.Single(dispatcher.Dispatch(Cc(Button, 127)));
        Assert.Equal(ActionPhase.Pressed, press.Phase);
        Assert.Empty(dispatcher.Dispatch(Cc(Button, 127)));
        Assert.Empty(dispatcher.Dispatch(Cc(Button, 0)));
        Assert.Empty(dispatcher.Dispatch(Cc(Button, 0)));
        Assert.Single(dispatcher.Dispatch(Cc(Button, 127)));
    }

    [Theory]
    [InlineData(63, false)]
    [InlineData(64, true)]
    [InlineData(100, true)]
    public void PressThreshold_Is64(byte value, bool pressed) =>
        Assert.Equal(pressed, Create().Dispatch(Cc(Button, value)).Count == 1);

    [Fact]
    public void WhileHeldMute_GetsPressedAndReleased_OnceEach()
    {
        MappingDispatcher dispatcher = Create();

        ActionInvocation down = Assert.Single(dispatcher.Dispatch(Cc(Talk, 127)));
        Assert.Empty(dispatcher.Dispatch(Cc(Talk, 127)));                // repeat while held
        ActionInvocation up = Assert.Single(dispatcher.Dispatch(Cc(Talk, 0)));
        Assert.Empty(dispatcher.Dispatch(Cc(Talk, 0)));                  // repeat while released

        Assert.Equal(ActionPhase.Pressed, down.Phase);
        Assert.Equal(1f, down.Level);
        Assert.Equal(ActionPhase.Released, up.Phase);
        Assert.Equal(0f, up.Level);

        Assert.Equal(ActionPhase.Pressed, Assert.Single(dispatcher.Dispatch(Cc(Silence, 127))).Phase);
        Assert.Equal(ActionPhase.Released, Assert.Single(dispatcher.Dispatch(Cc(Silence, 0))).Phase);
    }

    [Fact]
    public void ReleaseWithoutPress_DoesNothing()
    {
        MappingDispatcher dispatcher = Create();
        Assert.Empty(dispatcher.Dispatch(Cc(Talk, 0)));                  // e.g. key released right after start
    }

    [Fact]
    public void Released_ReachesOnlyWhileHeldMuteActions()
    {
        var control = new ControlId(ControlKind.ControlChange, 0, 41);
        var dispatcher = new MappingDispatcher(new Profile("p",
        [
            new ControlMapping(control,
            [
                new MediaAction(MediaCommand.PlayPause),
                new MuteAction(AudioTarget.Speakers),
                new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld),
                new HotkeyAction("Ctrl+M"),
                new SwitchDeviceAction(),
                new MuteAction(AudioTarget.Application("Discord.exe"), MuteMode.MuteWhileHeld),
            ]),
        ]));

        IReadOnlyList<ActionInvocation> pressed = dispatcher.Dispatch(Cc(control, 127));
        IReadOnlyList<ActionInvocation> released = dispatcher.Dispatch(Cc(control, 0));

        Assert.Equal(6, pressed.Count);
        Assert.All(pressed, i => Assert.Equal(ActionPhase.Pressed, i.Phase));
        Assert.Equal(new ActionDefinition[]
        {
            new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld),
            new MuteAction(AudioTarget.Application("Discord.exe"), MuteMode.MuteWhileHeld),
        }, released.Select(i => i.Action));
        Assert.All(released, i => Assert.Equal(ActionPhase.Released, i.Phase));
    }

    [Fact]
    public void SeveralVolumeTargets_OnOneFader_EachGetTheSameLevel_InOrder()
    {
        ActionDefinition[] actions =
        [
            new SetVolumeAction(AudioTarget.Application("game1.exe")),
            new SetVolumeAction(AudioTarget.Application("game2.exe")),
            new SetVolumeAction(AudioTarget.Application("game3.exe")),
        ];
        var dispatcher = new MappingDispatcher(new Profile("p",
            [new ControlMapping(Fader, actions, new ValueScaling(0.2f, 0.6f), "Spiele")]));

        IReadOnlyList<ActionInvocation> result = dispatcher.Dispatch(Cc(Fader, 127));

        Assert.Equal(actions, result.Select(i => i.Action));
        Assert.All(result, i => Assert.Equal(0.6f, i.Level, 5));
        Assert.All(result, i => Assert.Equal("Spiele", i.Mapping.Name));
    }

    [Fact]
    public void MixedMapping_OnUnknownControl_RunsVolumeAlways_AndKeyActionsOnEdges()
    {
        var control = new ControlId(ControlKind.ControlChange, 3, 100);
        var dispatcher = new MappingDispatcher(new Profile("p",
            [new ControlMapping(control, [new SetVolumeAction(AudioTarget.Speakers), new MediaAction(MediaCommand.Next)])]));

        Assert.Equal(2, dispatcher.Dispatch(Cc(control, 127)).Count);   // press: volume + media
        Assert.Single(dispatcher.Dispatch(Cc(control, 127)));            // held: volume only
        Assert.Single(dispatcher.Dispatch(Cc(control, 0)));              // release: volume only
    }

    [Fact]
    public void EmptyMapping_ProducesNothing()
    {
        var dispatcher = new MappingDispatcher(new Profile("p", [new ControlMapping(Button, Array.Empty<ActionDefinition>())]));
        Assert.Empty(dispatcher.Dispatch(Cc(Button, 127)));
    }

    [Fact]
    public void SetProfile_ResetsPressedState()
    {
        MappingDispatcher dispatcher = Create();
        dispatcher.Dispatch(Cc(Button, 127));
        dispatcher.SetProfile(new Profile("p2", [new ControlMapping(Button, new MediaAction(MediaCommand.Next))]));
        Assert.Single(dispatcher.Dispatch(Cc(Button, 127)));
    }

    [Fact]
    public void NoteControls_UseTheSameEdges()
    {
        var note = new ControlId(ControlKind.Note, 0, 60);
        var dispatcher = new MappingDispatcher(new Profile("p",
            [new ControlMapping(note, new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld))]));

        Assert.Equal(ActionPhase.Pressed, Assert.Single(dispatcher.Dispatch(new MidiMessage(MidiMessageKind.NoteOn, 0, 60, 90))).Phase);
        Assert.Equal(ActionPhase.Released, Assert.Single(dispatcher.Dispatch(new MidiMessage(MidiMessageKind.NoteOff, 0, 60, 0))).Phase);
    }
}
