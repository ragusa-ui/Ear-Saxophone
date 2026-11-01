using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

/// <summary>Drag and drop in the picture: <see cref="Profile.TransferMapping"/>.</summary>
public class ProfileTransferTests
{
    private static readonly ControlId Fader1 = new(ControlKind.ControlChange, 0, 0);
    private static readonly ControlId Fader2 = new(ControlKind.ControlChange, 0, 1);
    private static readonly ControlId Knob1 = new(ControlKind.ControlChange, 0, 16);
    private static readonly ControlId Mute1 = new(ControlKind.ControlChange, 0, 48);
    private static readonly ControlId Mute2 = new(ControlKind.ControlChange, 0, 49);
    private static readonly ControlId Play = new(ControlKind.ControlChange, 0, 41);
    private static readonly ControlId Learned = new(ControlKind.ControlChange, 4, 100);

    private static readonly ShapeLookup Layout = Nanokontrol2Layout.ShapeOf;

    private static readonly ControlMapping Games = new(Fader1,
        [new SetVolumeAction(AudioTarget.Application("game1.exe")), new SetVolumeAction(AudioTarget.Application("game2.exe"))],
        new ValueScaling(0.1f, 0.9f, true), "Spiele");

    private static readonly ControlMapping Speakers = new(Fader2, new SetVolumeAction(AudioTarget.Speakers));

    private static readonly ControlMapping MicPtt = new(Mute1, new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld));

    private static readonly ControlMapping Media = new(Play, new MediaAction(MediaCommand.PlayPause));

    private static Profile Sample() => new("p", [Games, Speakers, MicPtt, Media]);

    [Fact]
    public void Move_ToFreeControl_MovesEverything_AndKeepsTheOthers()
    {
        TransferResult result = Sample().TransferMapping(Fader1, Knob1, TransferMode.Move, Layout);

        Assert.Equal(TransferOutcome.Moved, result.Outcome);
        Assert.True(result.Changed);
        Assert.Null(result.Profile.Find(Fader1));
        Assert.Equal(Games with { Control = Knob1 }, result.Profile.Find(Knob1));
        Assert.Equal("Spiele", result.Profile.Find(Knob1)!.Name);
        Assert.Equal(new ValueScaling(0.1f, 0.9f, true), result.Profile.Find(Knob1)!.Scaling);
        Assert.Equal(4, result.Profile.Mappings.Count);
        Assert.Equal(Speakers, result.Profile.Find(Fader2));
        Assert.Equal(MicPtt, result.Profile.Find(Mute1));
        Assert.Empty(result.Profile.Validate());
    }

    [Fact]
    public void Move_ToMappedControl_Swaps()
    {
        TransferResult result = Sample().TransferMapping(Fader1, Fader2, TransferMode.Move, Layout);

        Assert.Equal(TransferOutcome.Swapped, result.Outcome);
        Assert.Equal(Games with { Control = Fader2 }, result.Profile.Find(Fader2));
        Assert.Equal(Speakers with { Control = Fader1 }, result.Profile.Find(Fader1));
        Assert.Equal(4, result.Profile.Mappings.Count);
        Assert.Empty(result.Profile.Validate());
    }

    [Fact]
    public void Move_KeyToMappedKey_Swaps()
    {
        TransferResult result = Sample().TransferMapping(Mute1, Play, TransferMode.Move, Layout);

        Assert.Equal(TransferOutcome.Swapped, result.Outcome);
        Assert.IsType<MuteAction>(result.Profile.Find(Play)!.Actions[0]);
        Assert.IsType<MediaAction>(result.Profile.Find(Mute1)!.Actions[0]);
    }

    [Fact]
    public void Copy_ToFreeControl_KeepsTheSource()
    {
        TransferResult result = Sample().TransferMapping(Mute1, Mute2, TransferMode.Copy, Layout);

        Assert.Equal(TransferOutcome.Copied, result.Outcome);
        Assert.Equal(MicPtt, result.Profile.Find(Mute1));
        Assert.Equal(MicPtt with { Control = Mute2 }, result.Profile.Find(Mute2));
        Assert.Equal(5, result.Profile.Mappings.Count);
    }

    [Fact]
    public void Copy_ToMappedControl_ReplacesTheTarget_WithoutSwapping()
    {
        TransferResult result = Sample().TransferMapping(Fader1, Fader2, TransferMode.Copy, Layout);

        Assert.Equal(TransferOutcome.Copied, result.Outcome);
        Assert.Equal(Games, result.Profile.Find(Fader1));
        Assert.Equal(Games with { Control = Fader2 }, result.Profile.Find(Fader2));
        Assert.Equal(4, result.Profile.Mappings.Count);
        Assert.Single(result.Profile.Mappings, m => m.Control == Fader2);
        Assert.Empty(result.Profile.Validate());
    }

    [Theory]
    [InlineData(TransferMode.Move)]
    [InlineData(TransferMode.Copy)]
    public void VolumeOntoAKey_IsIncompatible_AndChangesNothing(TransferMode mode)
    {
        Profile profile = Sample();

        TransferResult result = profile.TransferMapping(Fader1, Mute2, mode, Layout);

        Assert.Equal(TransferOutcome.Incompatible, result.Outcome);
        Assert.False(result.Changed);
        Assert.Same(profile, result.Profile);
    }

    [Theory]
    [InlineData(TransferMode.Move)]
    [InlineData(TransferMode.Copy)]
    public void KeyActionOntoAFader_IsIncompatible(TransferMode mode) =>
        Assert.Equal(TransferOutcome.Incompatible, Sample().TransferMapping(Play, Knob1, mode, Layout).Outcome);

    [Fact]
    public void Swap_IsIncompatible_WhenTheTargetsMappingDoesNotFitTheSource()
    {
        // A learned control of unknown shape takes the media key, but the volume mapping of the fader would land on the key.
        var profile = new Profile("p", [Media, new ControlMapping(Learned, new SetVolumeAction(AudioTarget.Speakers))]);

        TransferResult result = profile.TransferMapping(Play, Learned, TransferMode.Move, Layout);

        Assert.Equal(TransferOutcome.Incompatible, result.Outcome);
        Assert.Same(profile, result.Profile);
    }

    [Fact]
    public void UnknownControls_AcceptEverything()
    {
        TransferResult result = Sample().TransferMapping(Fader1, Learned, TransferMode.Move, Layout);
        Assert.Equal(TransferOutcome.Moved, result.Outcome);

        TransferResult back = result.Profile.TransferMapping(Learned, Fader2, TransferMode.Move, Layout);
        Assert.Equal(TransferOutcome.Swapped, back.Outcome);
    }

    [Fact]
    public void TheShapeLookupDecides()
    {
        ShapeLookup allKeys = _ => ControlShape.Button;
        Assert.Equal(TransferOutcome.Incompatible, Sample().TransferMapping(Fader1, Learned, TransferMode.Move, allKeys).Outcome);
        Assert.Equal(TransferOutcome.Moved, Sample().TransferMapping(Mute1, Learned, TransferMode.Move, allKeys).Outcome);
    }

    [Theory]
    [InlineData(TransferMode.Move)]
    [InlineData(TransferMode.Copy)]
    public void SameControl_OrEmptySource_IsNothingToDo(TransferMode mode)
    {
        Profile profile = Sample();

        TransferResult same = profile.TransferMapping(Fader1, Fader1, mode, Layout);
        TransferResult empty = profile.TransferMapping(Knob1, Fader1, mode, Layout);

        Assert.Equal(TransferOutcome.NothingToDo, same.Outcome);
        Assert.Equal(TransferOutcome.NothingToDo, empty.Outcome);
        Assert.Same(profile, same.Profile);
        Assert.Same(profile, empty.Profile);
        Assert.False(empty.Changed);
    }

    [Fact]
    public void Move_KeepsThePositionInTheList()
    {
        TransferResult result = Sample().TransferMapping(Fader2, Knob1, TransferMode.Move, Layout);

        Assert.Equal([Fader1, Knob1, Mute1, Play], result.Profile.Mappings.Select(m => m.Control));
    }
}
