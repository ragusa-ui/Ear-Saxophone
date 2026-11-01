using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.ViewModels;
using static EarSaxophone.Core.Tests.ViewModelHarness;

namespace EarSaxophone.Core.Tests;

/// <summary>Drag and drop in the picture: <see cref="MainViewModel.TransferMapping(Midi.ControlId, Midi.ControlId, bool)"/>.</summary>
public class TransferMappingViewModelTests : IDisposable
{
    private readonly ViewModelHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static ControlMapping Volume(int cc, AudioTarget target) => new(Cc(cc), new SetVolumeAction(target));

    [Fact]
    public void Move_ToAFreeFader_MovesSavesSelectsAndWorksAtOnce()
    {
        _h.SaveProfile(Volume(0, AudioTarget.Speakers));
        MainViewModel vm = _h.Create();

        string message = vm.TransferMapping(Cc(0), Cc(1), copy: false);

        Assert.Equal("Belegung von Fader 1 nach Fader 2 verschoben.", message);
        Assert.Equal(message, vm.StatusMessage);
        Assert.Null(vm.Current.Find(Cc(0)));
        Assert.Equal(Volume(1, AudioTarget.Speakers), _h.SavedProfile().Find(Cc(1)));
        Assert.Equal("Fader 2", vm.SelectedElement?.Label);
        Assert.False(Element(vm, "Fader 1").HasMapping);
        Assert.True(Element(vm, "Fader 2").HasMapping);

        _h.Send(vm, 1, 127);
        Assert.Equal(1f, _h.Speakers.Volume, 5);
    }

    [Fact]
    public void Move_OntoAMappedFader_Swaps_AndCopyWithCtrl_Copies()
    {
        _h.SaveProfile(Volume(0, AudioTarget.Speakers), Volume(1, AudioTarget.Microphone));
        MainViewModel vm = _h.Create();

        Assert.Equal("Belegungen von Fader 1 und Fader 2 getauscht.", vm.TransferMapping(Element(vm, "Fader 1"), Element(vm, "Fader 2"), copy: false));
        Assert.Equal(Volume(0, AudioTarget.Microphone), vm.Current.Find(Cc(0)));
        Assert.Equal(Volume(1, AudioTarget.Speakers), vm.Current.Find(Cc(1)));

        Assert.Equal("Belegung von Fader 1 nach Fader 3 kopiert.", vm.TransferMapping(Cc(0), Cc(2), copy: true));
        Assert.Equal(Volume(0, AudioTarget.Microphone), vm.Current.Find(Cc(0)));
        Assert.Equal(Volume(2, AudioTarget.Microphone), _h.SavedProfile().Find(Cc(2)));
    }

    [Fact]
    public void AMappingThatDoesNotFit_IsRefusedWithoutChange()
    {
        _h.SaveProfile(Volume(0, AudioTarget.Speakers));
        MainViewModel vm = _h.Create();

        Assert.False(vm.CanTransfer(Cc(0), Cc(48), copy: false));
        Assert.True(vm.CanTransfer(Cc(0), Cc(16), copy: true)); // fader onto knob: both stepless
        string message = vm.TransferMapping(Cc(0), Cc(48), copy: false);

        Assert.Contains("passt nicht", message);
        Assert.Contains("Nichts geändert", message);
        Assert.Equal(Volume(0, AudioTarget.Speakers), vm.Current.Find(Cc(0)));
        Assert.Null(vm.Current.Find(Cc(48)));
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void NothingToMove_SaysSo()
    {
        MainViewModel vm = _h.Create();

        Assert.Contains("nichts zu verschieben", vm.TransferMapping(Cc(0), Cc(1), copy: false));
        Assert.False(vm.CanTransfer(Cc(0), Cc(0), copy: true));
        Assert.False(File.Exists(_h.ProfilePath));
    }

    [Fact]
    public void UnsavedValidChanges_AreAppliedBeforeTheTransfer()
    {
        MainViewModel vm = _h.Create();
        vm.Select(Element(vm, "Fader 1"));
        vm.Editor.AddApplication("Discord.exe");

        vm.TransferMapping(Cc(0), Cc(5), copy: false);

        Assert.Equal(Volume(5, AudioTarget.Application("Discord.exe")), _h.SavedProfile().Find(Cc(5)));
        Assert.Equal("Fader 6", vm.SelectedElement?.Label);
        Assert.Equal(["Discord.exe"], vm.Editor.Targets.Select(t => t.Name));
        Assert.False(vm.Editor.IsDirty);
    }
}
