using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

/// <summary>Shapes of controls (stepless or key) and which actions fit them.</summary>
public class ControlShapeTests
{
    private static readonly ActionDefinition Volume = new SetVolumeAction(AudioTarget.Speakers);

    private static readonly ActionDefinition[] KeyActions =
    [
        new MuteAction(AudioTarget.Microphone),
        new MuteAction(AudioTarget.Microphone, MuteMode.MuteWhileHeld),
        new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld),
        new MediaAction(MediaCommand.Next),
        new HotkeyAction("Ctrl+M"),
        new SwitchDeviceAction(),
    ];

    private static MidiMessage Cc(byte number, byte value, byte channel = 3) => new(MidiMessageKind.ControlChange, channel, number, value);

    [Fact]
    public void Layout_KnowsTheShapeOfAll51Controls()
    {
        Assert.Equal(51, Nanokontrol2Layout.Elements.Count);
        Assert.Equal(16, Nanokontrol2Layout.Elements.Count(e => e.Shape == ControlShape.Continuous));
        Assert.Equal(35, Nanokontrol2Layout.Elements.Count(e => e.Shape == ControlShape.Button));
        Assert.All(Nanokontrol2Layout.Elements, e => Assert.Equal(e.Shape, Nanokontrol2Layout.ShapeOf(e.Control)));
        Assert.All(Nanokontrol2Layout.Elements.Where(e => e.Kind is ElementKind.Knob or ElementKind.Fader),
            e => Assert.Equal(ControlShape.Continuous, e.Shape));
    }

    [Theory]
    [InlineData(0, ControlShape.Continuous)]   // Fader 1
    [InlineData(7, ControlShape.Continuous)]   // Fader 8
    [InlineData(16, ControlShape.Continuous)]  // Drehregler 1
    [InlineData(23, ControlShape.Continuous)]  // Drehregler 8
    [InlineData(32, ControlShape.Button)]      // S 1
    [InlineData(48, ControlShape.Button)]      // M 1
    [InlineData(71, ControlShape.Button)]      // R 8
    [InlineData(41, ControlShape.Button)]      // Play
    [InlineData(45, ControlShape.Button)]      // Aufnahme
    [InlineData(58, ControlShape.Button)]      // Track ◄
    [InlineData(100, ControlShape.Unknown)]
    public void Layout_ShapeOf(byte number, ControlShape expected) =>
        Assert.Equal(expected, Nanokontrol2Layout.ShapeOf(new ControlId(ControlKind.ControlChange, 0, number)));

    [Fact]
    public void Layout_OtherChannelOrNote_IsUnknown()
    {
        Assert.Equal(ControlShape.Unknown, Nanokontrol2Layout.ShapeOf(new ControlId(ControlKind.ControlChange, 1, 0)));
        Assert.Equal(ControlShape.Unknown, Nanokontrol2Layout.ShapeOf(new ControlId(ControlKind.Note, 0, 41)));
    }

    [Fact]
    public void Rules_SteplessControlsTakeOnlyVolume()
    {
        Assert.True(ActionRules.IsAllowed(Volume, ControlShape.Continuous));
        Assert.All(KeyActions, a => Assert.False(ActionRules.IsAllowed(a, ControlShape.Continuous)));
    }

    [Fact]
    public void Rules_KeysTakeEverythingButVolume()
    {
        Assert.False(ActionRules.IsAllowed(Volume, ControlShape.Button));
        Assert.All(KeyActions, a => Assert.True(ActionRules.IsAllowed(a, ControlShape.Button)));
    }

    [Fact]
    public void Rules_UnknownControlsTakeEverything()
    {
        Assert.True(ActionRules.IsAllowed(Volume, ControlShape.Unknown));
        Assert.All(KeyActions, a => Assert.True(ActionRules.IsAllowed(a, ControlShape.Unknown)));
    }

    [Fact]
    public void Rules_ForAMapping_RequireEveryAction()
    {
        var id = new ControlId(ControlKind.ControlChange, 0, 0);
        var mixed = new ControlMapping(id, [Volume, new MediaAction(MediaCommand.Next)]);
        Assert.False(ActionRules.IsAllowed(mixed, ControlShape.Continuous));
        Assert.False(ActionRules.IsAllowed(mixed, ControlShape.Button));
        Assert.True(ActionRules.IsAllowed(mixed, ControlShape.Unknown));
        Assert.True(ActionRules.IsAllowed(new ControlMapping(id, [Volume, Volume]), ControlShape.Continuous));
    }

    [Fact]
    public void Rules_Phases()
    {
        Assert.True(ActionRules.RespondsTo(Volume, ActionPhase.Changed));
        Assert.False(ActionRules.RespondsTo(Volume, ActionPhase.Pressed));
        Assert.False(ActionRules.RespondsTo(Volume, ActionPhase.Released));
        Assert.All(KeyActions, a => Assert.True(ActionRules.RespondsTo(a, ActionPhase.Pressed)));
        Assert.All(KeyActions, a => Assert.False(ActionRules.RespondsTo(a, ActionPhase.Changed)));
        Assert.Equal(
            [false, true, true, false, false, false],
            KeyActions.Select(a => ActionRules.RespondsTo(a, ActionPhase.Released)));
    }

    [Fact]
    public void Tracker_ValueInBetween_MakesAControlStepless_ForGood()
    {
        var tracker = new ControlShapeTracker();
        ControlId id = Cc(20, 0).Control;

        Assert.Equal(ControlShape.Unknown, tracker.ShapeOf(id));
        Assert.Equal(ControlShape.Unknown, tracker.Observe(Cc(20, 0)));
        Assert.Equal(ControlShape.Continuous, tracker.Observe(Cc(20, 1)));
        Assert.Equal(ControlShape.Continuous, tracker.Observe(Cc(20, 127)));
        Assert.Equal(ControlShape.Continuous, tracker.Observe(Cc(20, 0)));
        Assert.Equal(ControlShape.Continuous, tracker.ShapeOf(id));
    }

    [Fact]
    public void Tracker_PressAndRelease_MakeAKey_UntilAValueInBetweenShowsUp()
    {
        var tracker = new ControlShapeTracker();

        Assert.Equal(ControlShape.Unknown, tracker.Observe(Cc(21, 127)));     // a single value is not yet clear
        Assert.Equal(ControlShape.Unknown, tracker.Observe(Cc(21, 127)));
        Assert.Equal(ControlShape.Button, tracker.Observe(Cc(21, 0)));
        Assert.Equal(ControlShape.Button, tracker.Observe(Cc(21, 127)));
        Assert.Equal(ControlShape.Continuous, tracker.Observe(Cc(21, 90)));   // it was a fader moved very fast
    }

    [Fact]
    public void Tracker_KeepsControlsApart_AndUsesTheKnownLayoutFirst()
    {
        var tracker = new ControlShapeTracker(Nanokontrol2Layout.ShapeOf);

        tracker.Observe(Cc(22, 50));
        tracker.Observe(Cc(41, 50, channel: 0));                               // Play: a key, whatever it sends

        Assert.Equal(ControlShape.Continuous, tracker.ShapeOf(Cc(22, 0).Control));
        Assert.Equal(ControlShape.Unknown, tracker.ShapeOf(Cc(23, 0).Control));
        Assert.Equal(ControlShape.Unknown, tracker.ShapeOf(Cc(22, 0, channel: 4).Control));
        Assert.Equal(ControlShape.Button, tracker.ShapeOf(new ControlId(ControlKind.ControlChange, 0, 41)));
        Assert.Equal(ControlShape.Continuous, tracker.ShapeOf(new ControlId(ControlKind.ControlChange, 0, 0)));
    }

    [Fact]
    public void Tracker_NotesAreKeys()
    {
        var tracker = new ControlShapeTracker();
        Assert.Equal(ControlShape.Button, tracker.Observe(new MidiMessage(MidiMessageKind.NoteOn, 0, 60, 87)));
        Assert.Equal(ControlShape.Button, tracker.ShapeOf(new ControlId(ControlKind.Note, 0, 61)));
    }

    [Fact]
    public void Tracker_IsSafeAcrossThreads()
    {
        var tracker = new ControlShapeTracker();
        Parallel.For(0, 2000, i => tracker.Observe(Cc((byte)(i % 50), (byte)(i % 3 == 0 ? 0 : 127))));
        Assert.All(Enumerable.Range(0, 50), n => Assert.Equal(ControlShape.Button, tracker.ShapeOf(Cc((byte)n, 0).Control)));
    }
}
