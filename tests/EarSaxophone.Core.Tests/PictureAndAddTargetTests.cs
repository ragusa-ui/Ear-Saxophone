using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

/// <summary>LED areas of the keys in the picture (mute state) and the "Hinzufügen" choices of the editor (program list, file).</summary>
public sealed class PictureAndAddTargetTests : IDisposable
{
    private static readonly ControlId Fader = ViewModelHarness.Cc(3);
    private readonly ViewModelHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private static MappingEditorViewModel Editor()
    {
        var editor = new MappingEditorViewModel();
        editor.Load(Fader, ControlShape.Continuous, null);
        return editor;
    }

    [Fact]
    public void OnlyKeysWithAnLedOnTheDevice_HaveAnLedArea()
    {
        MainViewModel vm = _harness.Create();

        Assert.True(ViewModelHarness.Element(vm, "M 1").HasLed);
        Assert.True(ViewModelHarness.Element(vm, "Play").HasLed);
        Assert.True(ViewModelHarness.Element(vm, "Cycle").HasLed);
        Assert.False(ViewModelHarness.Element(vm, "Track ◄").HasLed);
        Assert.False(ViewModelHarness.Element(vm, "Marker Set").HasLed);
        Assert.False(ViewModelHarness.Element(vm, "Fader 1").HasLed);
        Assert.Equal(24 + 6, vm.Elements.Count(e => e.HasLed));
    }

    [Fact]
    public void MuteKey_IsLitInThePicture_WhileItsTargetIsMuted_AlsoWhenMutedElsewhere()
    {
        _harness.SaveProfile(new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Speakers)));
        MainViewModel vm = _harness.Create();
        ControllerElementViewModel m1 = ViewModelHarness.Element(vm, "M 1");
        ControllerElementViewModel s1 = ViewModelHarness.Element(vm, "S 1");
        Assert.False(m1.IsMuteLit);

        _harness.Send(vm, 48, 127); // press: toggles the speakers to muted
        _harness.Send(vm, 48, 0);
        Assert.True(_harness.Speakers.Muted);
        Assert.True(m1.IsMuteLit);
        Assert.False(s1.IsMuteLit);

        _harness.Speakers.Muted = false; // unmuted in the Windows mixer: the next tick follows
        vm.Tick();
        Assert.False(m1.IsMuteLit);

        _harness.Speakers.Muted = true;
        vm.Tick();
        Assert.True(m1.IsMuteLit);
    }

    [Fact]
    public void RemovingTheMuteMapping_SwitchesTheLedAreaOff()
    {
        _harness.SaveProfile(new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Speakers)));
        _harness.Speakers.Muted = true;
        MainViewModel vm = _harness.Create();
        ControllerElementViewModel m1 = ViewModelHarness.Element(vm, "M 1");
        Assert.True(m1.IsMuteLit);

        vm.Select(m1);
        vm.RemoveSelectedMapping();

        Assert.False(m1.IsMuteLit);
    }

    [Fact]
    public void AddProgramFile_TakesTheFileNameAsProgramIdentity()
    {
        MappingEditorViewModel editor = Editor();

        Assert.True(editor.AddProgramFile(@"C:\Program Files\Some Game\Game.exe"));
        Assert.True(editor.AddProgramFile("/home/x/Other.EXE"));

        Assert.Equal(["Game.exe", "Other.EXE"], editor.Targets.Select(t => t.Name));
        Assert.Equal(AudioTarget.Application("Game.exe"), editor.Targets[0].Target);
        Assert.Null(editor.Targets[0].Target.App!.ExePath); // the name alone, so the program still matches after an update moves it
        Assert.True(editor.TryBuild(out ControlMapping? mapping, out _));
        Assert.Equal([new SetVolumeAction(AudioTarget.Application("Game.exe")), new SetVolumeAction(AudioTarget.Application("Other.EXE"))], mapping!.Actions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Games\")]
    [InlineData(@"C:\Games\readme.txt")]
    [InlineData(@"C:\Games\.exe")]
    public void AddProgramFile_RefusesWhatIsNoProgramFile(string? path)
    {
        MappingEditorViewModel editor = Editor();

        Assert.False(editor.AddProgramFile(path));

        Assert.Empty(editor.Targets);
        Assert.NotNull(editor.ValidationMessage);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void AddProgramFile_DoesNotListAProgramTwice()
    {
        MappingEditorViewModel editor = Editor();
        Assert.True(editor.AddApplication("Discord.exe"));

        Assert.False(editor.AddProgramFile(@"D:\Apps\discord.exe"));

        Assert.Single(editor.Targets);
        Assert.Contains("bereits", editor.ValidationMessage);
    }

    [Fact]
    public void ProgramChoices_OfferOnlyProgramsThatAreNotListedYet()
    {
        MappingEditorViewModel editor = Editor();
        editor.SetAvailablePrograms([new ProgramInfo("Discord.exe"), new ProgramInfo("Spotify.exe")]);
        Assert.Equal(["Discord.exe", "Spotify.exe"], editor.ProgramChoices);

        editor.AddAppCommand.Execute("Discord.exe");
        Assert.Equal(["Spotify.exe"], editor.ProgramChoices);
        Assert.True(editor.HasProgramChoices);

        editor.AddApplication("spotify");
        Assert.Empty(editor.ProgramChoices);
        Assert.False(editor.HasProgramChoices);

        editor.RemoveTarget(editor.Targets[0]);
        Assert.Equal(["Discord.exe"], editor.ProgramChoices);
    }

    [Fact]
    public void ProgramChoice_OpensWithItsCommand_StaysOpenWhileAdding_AndClosesWithAnotherControl()
    {
        MappingEditorViewModel editor = Editor();
        Assert.False(editor.IsChoosingProgram);

        editor.ShowProgramChoiceCommand.Execute(null);
        Assert.True(editor.IsChoosingProgram);
        Assert.False(editor.IsDirty); // opening the choice changes nothing yet

        editor.AddApplication("game1.exe");
        Assert.True(editor.IsChoosingProgram); // several games in a row
        editor.HideProgramChoiceCommand.Execute(null);
        Assert.False(editor.IsChoosingProgram);

        editor.ShowProgramChoiceCommand.Execute(null);
        editor.Load(ViewModelHarness.Cc(4), ControlShape.Continuous, null);
        Assert.False(editor.IsChoosingProgram);
    }

    [Fact]
    public void OpeningTheProgramChoice_RefreshesTheListOfProgramsWithSound()
    {
        MainViewModel vm = _harness.Create();
        vm.Select(ViewModelHarness.Element(vm, "Fader 1"));
        _harness.Audio.AddSession(@"C:\Games\NewGame.exe", "spk");
        Assert.DoesNotContain("NewGame.exe", vm.Editor.ProgramChoices);

        vm.Editor.ShowProgramChoiceCommand.Execute(null);

        Assert.Contains("NewGame.exe", vm.Editor.ProgramChoices);
        Assert.Contains("Discord.exe", vm.Editor.ProgramChoices);
    }

    [Fact]
    public void AddMenuEntries_AddTheDefaultDevices()
    {
        MappingEditorViewModel editor = Editor();

        editor.AddSpeakersCommand.Execute(null);
        editor.AddMicrophoneCommand.Execute(null);

        Assert.Equal([TargetSymbol.Speakers, TargetSymbol.Microphone], editor.Targets.Select(t => t.Symbol));
    }
}
