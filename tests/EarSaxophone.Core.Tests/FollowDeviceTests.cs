using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.ViewModels;
using static EarSaxophone.Core.Tests.ViewModelHarness;

namespace EarSaxophone.Core.Tests;

/// <summary>Selection follows the device, and what happens to unsaved editor changes when the selection changes.</summary>
public class FollowDeviceTests : IDisposable
{
    private readonly ViewModelHarness _h = new();

    public void Dispose() => _h.Dispose();

    private void Quiet() => _h.Clock.AdvanceMs(1000);

    [Fact]
    public void MovingAFader_SelectsIt_WithoutLearnMode_AndRunsItsAction()
    {
        MainViewModel vm = _h.Create();
        Assign(vm, "Fader 2", e => e.AddTarget(AudioTarget.Speakers));
        vm.Select(null);
        Quiet();

        _h.Send(vm, 1, 100);

        Assert.Equal("Fader 2", vm.SelectedElement?.Label);
        Assert.False(vm.IsLearning);
        Assert.Equal(Element(vm, "Fader 2").Control, vm.Editor.Control);
        Assert.Equal(100 / 127f, _h.Speakers.Volume, 3); // selecting does not swallow the message
    }

    [Fact]
    public void PressingAKey_SelectsIt_ReleasingOneDoesNot()
    {
        MainViewModel vm = _h.Create();

        _h.Send(vm, 45, 0);          // a release after a quiet time
        Assert.False(vm.HasSelection);
        _h.Send(vm, 45, 127);
        Assert.Equal("Aufnahme", vm.SelectedElement?.Label);
    }

    [Fact]
    public void MovingTheSelectedFaderAgain_ChangesNothing_AndTheEditorKeepsItsInput()
    {
        MainViewModel vm = _h.Create();
        _h.Send(vm, 0, 10);
        vm.Editor.AddApplication("Discord.exe");
        Assert.True(vm.Editor.IsDirty);
        Quiet();

        _h.Send(vm, 0, 90);

        Assert.Equal("Fader 1", vm.SelectedElement?.Label);
        Assert.True(vm.Editor.IsDirty);
        Assert.Equal(["Discord.exe"], vm.Editor.Targets.Select(t => t.Name));
        Assert.False(vm.HasPendingSelection);
    }

    [Fact]
    public void WhileOneControlIsOperated_TheSelectionDoesNotJump_ButFollowsWhenTheOtherKeepsMoving()
    {
        MainViewModel vm = _h.Create();
        _h.Send(vm, 0, 10);                       // Fader 1 starts
        _h.Clock.AdvanceMs(100);
        _h.Send(vm, 1, 50);                       // Fader 2 touched while Fader 1 is operated
        _h.Clock.AdvanceMs(300);
        _h.Send(vm, 1, 52);
        Assert.Equal("Fader 1", vm.SelectedElement?.Label);

        _h.Clock.AdvanceMs(300);                  // Fader 1 rests for 700 ms, Fader 2 still moves
        _h.Send(vm, 1, 54);

        Assert.Equal("Fader 2", vm.SelectedElement?.Label);
    }

    [Fact]
    public void AJitteringFader_DoesNotStealTheSelection_ARealMoveDoes()
    {
        MainViewModel vm = _h.Create();
        _h.Send(vm, 0, 64);
        vm.Select(Element(vm, "Play"));
        Quiet();

        _h.Send(vm, 0, 65);                       // one step: noise
        Assert.Equal("Play", vm.SelectedElement?.Label);
        _h.Clock.AdvanceMs(50);
        _h.Send(vm, 0, 66);                       // two steps away from where it rested
        Assert.Equal("Fader 1", vm.SelectedElement?.Label);
    }

    [Fact]
    public void Pinned_TheDeviceDoesNotSelect_AClickStillDoes()
    {
        MainViewModel vm = _h.Create();
        vm.FollowDevice = false;

        _h.Send(vm, 0, 100);
        Assert.False(vm.HasSelection);

        vm.SelectCommand.Execute(Element(vm, "Fader 3"));
        Assert.Equal("Fader 3", vm.SelectedElement?.Label);
    }

    [Fact]
    public void UnsavedChanges_TheDeviceOnlyPointsAtTheOtherControl_NothingIsAppliedOrLost()
    {
        MainViewModel vm = _h.Create();
        vm.Select(Element(vm, "Fader 1"));
        vm.Editor.AddTarget(AudioTarget.Speakers);
        Quiet();

        _h.Send(vm, 1, 80);

        Assert.Equal("Fader 1", vm.SelectedElement?.Label);
        Assert.True(vm.Editor.IsDirty);
        Assert.Equal("Fader 2", vm.PendingElement?.Label);
        Assert.Contains("Fader 2", vm.StatusMessage);
        Assert.Contains("Fader 1", vm.StatusMessage);
        Assert.Null(vm.Current.Find(Cc(0)));      // not applied behind the user's back

        vm.SelectPendingCommand.Execute(null);   // an explicit choice applies the valid change and goes there

        Assert.Equal("Fader 2", vm.SelectedElement?.Label);
        Assert.False(vm.HasPendingSelection);
        Assert.Equal(new ControlMapping(Cc(0), new SetVolumeAction(AudioTarget.Speakers)), vm.Current.Find(Cc(0)));
        Assert.Equal(new ControlMapping(Cc(0), new SetVolumeAction(AudioTarget.Speakers)), _h.SavedProfile().Find(Cc(0)));
    }

    [Fact]
    public void ClickingAnotherControl_AppliesValidChangesAutomatically()
    {
        MainViewModel vm = _h.Create();
        vm.Select(Element(vm, "M 1"));
        vm.Editor.AddTarget(AudioTarget.Microphone);

        vm.Select(Element(vm, "M 2"));

        Assert.Equal(new ControlMapping(Cc(48), new MuteAction(AudioTarget.Microphone)), _h.SavedProfile().Find(Cc(48)));
        Assert.Contains("automatisch übernommen", vm.StatusMessage);
        Assert.False(vm.Editor.IsDirty);
        Assert.Equal(Cc(49), vm.Editor.Control);
    }

    [Fact]
    public void ClickingAnotherControl_ThrowsInvalidChangesAway_WithANote()
    {
        MainViewModel vm = _h.Create();
        vm.Select(Element(vm, "Cycle"));
        vm.Editor.Kind = ActionKind.Hotkey;
        vm.Editor.HotkeyText = "Ctrl+Banana";

        vm.Select(Element(vm, "Play"));

        Assert.Equal("Play", vm.SelectedElement?.Label);
        Assert.Null(vm.Current.Find(Cc(46)));
        Assert.Contains("verworfen", vm.StatusMessage);
        Assert.Contains("Banana", vm.StatusMessage);
    }

    [Fact]
    public void WhenAutomaticApplyCannotSave_TheSelectionStays_AndTheChangeIsKept()
    {
        MainViewModel vm = _h.Create();
        Directory.CreateDirectory(_h.ProfilePath); // a folder where the file should be
        vm.Select(Element(vm, "Fader 1"));
        vm.Editor.AddTarget(AudioTarget.Speakers);

        vm.Select(Element(vm, "Fader 2"));

        Assert.Equal("Fader 1", vm.SelectedElement?.Label);
        Assert.True(vm.Editor.IsDirty);
        Assert.Contains("nicht gespeichert", vm.ErrorMessage);
        Assert.Null(vm.Current.Find(Cc(0)));      // memory stays what is on disk
    }

    [Fact]
    public void ApplyAndDiscardCommands_FollowTheDirtyState()
    {
        MainViewModel vm = _h.Create();
        vm.Select(Element(vm, "Fader 1"));
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.DiscardCommand.CanExecute(null));
        int raised = 0;
        vm.ApplyCommand.CanExecuteChanged += (_, _) => raised++;

        vm.Editor.AddTarget(AudioTarget.Speakers);
        Assert.True(vm.ApplyCommand.CanExecute(null));
        Assert.True(vm.DiscardCommand.CanExecute(null));
        Assert.True(raised > 0);

        vm.DiscardCommand.Execute(null);
        Assert.False(vm.Editor.IsDirty);
        Assert.Empty(vm.Editor.Targets);
        Assert.Contains("verworfen", vm.StatusMessage);

        vm.Editor.AddTarget(AudioTarget.Speakers);
        vm.ApplyCommand.Execute(null);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.True(vm.RemoveCommand.CanExecute(null));
        Assert.Contains("gespeichert", vm.StatusMessage);
    }

    [Fact]
    public void SwitchingTheProfile_AppliesValidChangesToTheProfileTheyWereMadeIn()
    {
        MainViewModel vm = _h.Create();
        vm.NewProfile();
        vm.Select(Element(vm, "Fader 1"));
        vm.Editor.AddTarget(AudioTarget.Speakers);

        vm.ActiveProfileName = "Default";

        Assert.Null(vm.Current.Find(Cc(0)));
        vm.ActiveProfileName = "Profil 2";
        Assert.NotNull(vm.Current.Find(Cc(0)));
    }

    [Fact]
    public void AnUnknownControl_IsAddedAndSelected_WhenItIsOperated()
    {
        MainViewModel vm = _h.Create();

        _h.Send(vm, 100, 127);

        ControllerElementViewModel extra = Assert.Single(vm.ExtraElements);
        Assert.Equal(extra, vm.SelectedElement);
        Assert.Equal(ControlShape.Unknown, extra.Shape);
        Quiet();
        _h.Send(vm, 100, 0);
        Assert.Equal(ControlShape.Button, extra.Shape);
    }

    [Fact]
    public void Elements_KnowTheirShape()
    {
        MainViewModel vm = _h.Create();

        Assert.Equal(ControlShape.Continuous, Element(vm, "Drehregler 1").Shape);
        Assert.True(Element(vm, "Fader 8").IsStepless);
        Assert.Equal(ControlShape.Button, Element(vm, "R 8").Shape);
        Assert.Equal(16, vm.Elements.Count(e => e.IsStepless));
    }
}
