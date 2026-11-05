using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

/// <summary>What the window shows about muted targets and about the own labels of controls.</summary>
public sealed class MuteVisibilityTests : IDisposable
{
    private readonly ViewModelHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void FaderWithMutedTarget_IsMarkedMuted_AndTheHeaderNamesIt()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(0), new SetVolumeAction(AudioTarget.Speakers)));
        MainViewModel vm = _h.Create();
        ControllerElementViewModel fader = ViewModelHarness.Element(vm, "Fader 1");
        Assert.False(fader.IsTargetMuted);
        Assert.False(vm.HasMuted);

        _h.Speakers.Muted = true;
        vm.RefreshMuteStates();

        Assert.True(fader.IsTargetMuted);
        Assert.True(vm.HasMuted);
        Assert.StartsWith("Stumm: ", vm.MutedSummary);

        _h.Speakers.Muted = false;
        vm.RefreshMuteStates();

        Assert.False(fader.IsTargetMuted);
        Assert.False(vm.HasMuted);
        Assert.Equal(string.Empty, vm.MutedSummary);
    }

    [Fact]
    public void MutedSummary_UsesTheOwnLabel_AndListsEachNameOnce()
    {
        _h.SaveProfile(
            new ControlMapping(ViewModelHarness.Cc(0), [new SetVolumeAction(AudioTarget.Speakers)], Name: "Games"),
            new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Speakers), Name: "Games"));
        _h.Speakers.Muted = true;
        MainViewModel vm = _h.Create();
        vm.RefreshMuteStates();

        Assert.Equal("Stumm: Games", vm.MutedSummary);
    }

    [Fact]
    public void KeyWithoutOwnLabel_IsNamedByItsTarget()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Speakers)));
        _h.Speakers.Muted = true;
        MainViewModel vm = _h.Create();
        vm.RefreshMuteStates();

        Assert.True(ViewModelHarness.Element(vm, "M 1").IsMuteLit);
        Assert.Contains(EarSaxophone.Core.Localization.MappingDescriber.Describe(AudioTarget.Speakers), vm.MutedSummary);
    }

    [Fact]
    public void ControlWithoutVolumeAction_IsNeverMarkedMuted()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(1), new SetVolumeAction(AudioTarget.Microphone)));
        _h.Speakers.Muted = true;
        MainViewModel vm = _h.Create();
        vm.RefreshMuteStates();

        Assert.False(ViewModelHarness.Element(vm, "Fader 2").IsTargetMuted);
        Assert.False(ViewModelHarness.Element(vm, "Fader 3").IsTargetMuted);
    }

    [Fact]
    public void OwnLabel_ReplacesTheChannelNumberUnderTheFader_ElseTheNumberStays()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(3), [new SetVolumeAction(AudioTarget.Speakers)], Name: "  Games "));
        MainViewModel vm = _h.Create();

        ControllerElementViewModel named = ViewModelHarness.Element(vm, "Fader 4");
        ControllerElementViewModel plain = ViewModelHarness.Element(vm, "Fader 5");

        Assert.True(named.HasCustomName);
        Assert.Equal("Games", named.CustomName);
        Assert.Equal("Games", named.DisplayCaption);
        Assert.False(plain.HasCustomName);
        Assert.Equal("5", plain.DisplayCaption);
    }
}
