using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;
using static EarSaxophone.Core.Tests.ViewModelHarness;

namespace EarSaxophone.Core.Tests;

/// <summary>The question for a new program as the main view model runs it, and the settings behind it.</summary>
public class ProgramPromptViewModelTests : IDisposable
{
    private readonly ViewModelHarness _h = new();

    public ProgramPromptViewModelTests()
    {
        _h.Discord.Activity = AudioSessionActivity.Inactive;
    }

    public void Dispose() => _h.Dispose();

    private FakeSession StartGame(string path = @"C:\Games\game.exe")
    {
        FakeSession game = _h.Audio.AddSession(path, "spk");
        game.Activity = AudioSessionActivity.Active;
        return game;
    }

    /// <summary>Two ticks of the one-second timer around the debounce time.</summary>
    private ProgramPromptViewModel? TickUntilPrompt(MainViewModel vm)
    {
        ProgramPromptViewModel? prompt = null;
        vm.ProgramPromptRequested += p => prompt = p;
        vm.Tick();
        _h.Clock.Advance(ProgramPromptService.MinimumActiveTime);
        vm.Tick();
        return prompt;
    }

    [Fact]
    public void ANewProgramWithSound_AsksWithTheFadersAndKnobsOfTheDevice()
    {
        MainViewModel vm = _h.Create();
        StartGame();

        ProgramPromptViewModel? prompt = TickUntilPrompt(vm);

        Assert.NotNull(prompt);
        Assert.Same(prompt, vm.ActivePrompt);
        Assert.Equal("game.exe spielt Ton", prompt.Title);
        Assert.Equal(@"C:\Games\game.exe", prompt.ExePath);
        Assert.Equal(16, prompt.Choices.Count);
        Assert.All(prompt.Choices, c => Assert.True(c.IsStepless));
        Assert.Equal(8, prompt.Choices.Count(c => c.Label.StartsWith("Fader")));
        Assert.Equal(8, prompt.Choices.Count(c => c.Label.StartsWith("Drehregler")));
        Assert.Equal("game.exe spielt Ton. Auf welchen Regler legen?", prompt.MiniQuestion);
        Assert.Equal(Enumerable.Range(1, 8), prompt.Slots.Select(sl => sl.Number));
        Assert.All(prompt.Slots, sl => Assert.Equal(($"Fader {sl.Number}", $"Drehregler {sl.Number}"), (sl.Fader!.Label, sl.Knob!.Label)));
        Assert.Equal((Texts.PromptNotNow, Texts.PromptNeverAsk), (prompt.NotNowText, prompt.NeverAskText));
        Assert.Equal(TimeSpan.FromSeconds(40), ProgramPromptViewModel.AutoDismissAfter);
        Assert.Equal(Texts.PromptHeader, prompt.Header);
        Assert.Equal(TimeSpan.FromSeconds(40), prompt.AutoDismissDuration);
        Assert.True(prompt.PlaySound);
    }

    [Fact]
    public void ChoosingAFader_AddsTheProgramAsFurtherTarget_AndConfirms()
    {
        _h.SaveProfile(new ControlMapping(Cc(3), new SetVolumeAction(AudioTarget.Application("other.exe")), Name: "Spiele"));
        MainViewModel vm = _h.Create();
        var osd = new List<OsdMessage>();
        vm.OsdRequested += osd.Add;
        StartGame();
        ProgramPromptViewModel prompt = TickUntilPrompt(vm)!;
        ProgramPromptAnswer? closed = null;
        prompt.Closed += a => closed = a;

        prompt.AssignCommand.Execute(Element(vm, "Fader 4"));

        Assert.Equal(ProgramPromptAnswer.Assigned, closed);
        Assert.Null(vm.ActivePrompt);
        Assert.False(prompt.IsOpen);
        Assert.Equal(
            new ControlMapping(Cc(3), [new SetVolumeAction(AudioTarget.Application("other.exe")), new SetVolumeAction(AudioTarget.Application("game.exe"))], Name: "Spiele"),
            _h.SavedProfile().Find(Cc(3)));
        Assert.Equal(2, Element(vm, "Fader 4").ActionCount);
        Assert.Contains("Fader 4", vm.StatusMessage);
        Assert.Equal(("game.exe", "jetzt auf Fader 4"), (osd.Single().Title, osd.Single().Detail));

        _h.Send(vm, 3, 0);
        Assert.Equal(0f, _h.Audio.Sessions.Last().Volume, 5); // the game follows the fader at once
    }

    [Fact]
    public void AKeyCannotBeChosen_OnlyFadersAndKnobs()
    {
        MainViewModel vm = _h.Create();
        StartGame();
        ProgramPromptViewModel prompt = TickUntilPrompt(vm)!;

        Assert.False(prompt.Assign(Cc(48)));

        Assert.True(prompt.IsOpen);
        Assert.Null(vm.Current.Find(Cc(48)));
    }

    [Fact]
    public void MovingAFaderAtTheDevice_AnswersTheQuestion()
    {
        MainViewModel vm = _h.Create();
        StartGame();
        ProgramPromptViewModel prompt = TickUntilPrompt(vm)!;

        _h.Send(vm, 45, 127);                 // a key does not answer it
        Assert.True(prompt.IsOpen);
        _h.Clock.AdvanceMs(1000);
        _h.Send(vm, 17, 90);                  // knob 2

        Assert.False(prompt.IsOpen);
        Assert.Equal(new ControlMapping(Cc(17), new SetVolumeAction(AudioTarget.Application("game.exe"))), vm.Current.Find(Cc(17)));
    }

    [Fact]
    public void NeverAsk_PutsTheProgramOnTheIgnoreList_InSettingsJson_UntilReset()
    {
        MainViewModel vm = _h.Create();
        StartGame();
        TickUntilPrompt(vm)!.NeverAskCommand.Execute(null);

        Assert.Equal(["game.exe"], vm.IgnoredPrograms);
        Assert.Equal(["game.exe"], new SettingsStore(_h.SettingsPath).Load().IgnoredProgramList);
        Assert.Contains("\"ignoredPrograms\"", File.ReadAllText(_h.SettingsPath));
        Assert.Null(vm.Current.Find(Cc(0)));

        // The next program run does not ask again.
        vm.Dispose();
        MainViewModel again = _h.Create();
        Assert.Null(TickUntilPrompt(again));

        again.ResetIgnoredProgramsCommand.Execute(null);
        Assert.Empty(again.IgnoredPrograms);
        Assert.False(again.HasIgnoredPrograms);
        Assert.Empty(new SettingsStore(_h.SettingsPath).Load().IgnoredProgramList);
    }

    [Fact]
    public void NotNow_ClosesWithoutChange_AndDoesNotAskAgainInThisRun()
    {
        MainViewModel vm = _h.Create();
        StartGame();
        ProgramPromptViewModel prompt = TickUntilPrompt(vm)!;

        prompt.NotNow();
        prompt.NeverAsk();                   // a second answer is ignored

        Assert.Empty(vm.IgnoredPrograms);
        Assert.Empty(vm.Current.Mappings);
        _h.Clock.AdvanceMs(60_000);
        Assert.Null(TickUntilPrompt(vm));
    }

    [Fact]
    public void WithoutReaction_TheQuestionCloses_AsNotNow()
    {
        MainViewModel vm = _h.Create();
        StartGame();
        ProgramPromptViewModel prompt = TickUntilPrompt(vm)!;
        ProgramPromptAnswer? closed = null;
        prompt.Closed += a => closed = a;

        _h.Clock.Advance(ProgramPromptViewModel.AutoDismissAfter - TimeSpan.FromSeconds(1));
        vm.Tick();
        Assert.True(prompt.IsOpen);
        _h.Clock.AdvanceMs(1000);
        vm.Tick();

        Assert.Equal(ProgramPromptAnswer.NotNow, closed);
        Assert.Null(vm.ActivePrompt);
        Assert.Empty(vm.IgnoredPrograms);
        _h.Clock.AdvanceMs(1000);
        _h.Send(vm, 0, 100);                  // a fader move afterwards does not put the program anywhere
        Assert.Empty(vm.Current.Mappings);
    }

    [Fact]
    public void WhenNoWindowListens_NobodyIsAsked_SoAFaderMoveCannotAssignSilently()
    {
        MainViewModel vm = _h.Create();
        StartGame();
        vm.Tick();
        _h.Clock.Advance(ProgramPromptService.MinimumActiveTime);
        vm.Tick();
        Assert.Null(vm.ActivePrompt);

        _h.Send(vm, 0, 100);
        Assert.Empty(vm.Current.Mappings);

        Assert.NotNull(TickUntilPrompt(vm)); // once a window listens, the program is still new
    }

    [Fact]
    public void SwitchedOff_NobodyIsAsked_AndTheChoiceIsSaved()
    {
        MainViewModel vm = _h.Create();
        Assert.True(vm.AskForNewPrograms);
        vm.AskForNewPrograms = false;
        StartGame();

        Assert.Null(TickUntilPrompt(vm));
        Assert.False(new SettingsStore(_h.SettingsPath).Load().AskForNewPrograms);
    }

    [Fact]
    public void PromptSound_IsOnByDefault_CanBeSwitchedOff_AndIsSaved()
    {
        MainViewModel vm = _h.Create();
        Assert.True(vm.PromptSound);
        vm.PromptSound = false;
        StartGame();

        ProgramPromptViewModel? prompt = TickUntilPrompt(vm);

        Assert.NotNull(prompt);
        Assert.False(prompt.PlaySound);
        Assert.False(new SettingsStore(_h.SettingsPath).Load().PromptSound);
        Assert.NotEqual(new AppSettings(), new AppSettings(PromptSound: false));
    }

    [Fact]
    public void AProgramThatIsAlreadyOnAControl_IsNotAsked()
    {
        _h.SaveProfile(new ControlMapping(Cc(48), new MuteAction(AudioTarget.Application("Game.exe"))));
        MainViewModel vm = _h.Create();
        StartGame();

        Assert.Null(TickUntilPrompt(vm));
    }

    [Fact]
    public void OldSettingsFile_WithoutTheNewFields_AsksAndIgnoresNothing()
    {
        File.WriteAllText(_h.SettingsPath, "{ \"keepInTray\": false, \"showOsd\": true, \"osdPosition\": \"topLeft\" }");

        AppSettings loaded = new SettingsStore(_h.SettingsPath).Load();

        Assert.False(loaded.KeepInTray);
        Assert.True(loaded.AskForNewPrograms);
        Assert.True(loaded.PromptSound);
        Assert.Null(loaded.IgnoredPrograms);
        Assert.Empty(loaded.IgnoredProgramList);
    }

    [Fact]
    public void Settings_CompareIgnoredProgramsByContent()
    {
        Assert.Equal(new AppSettings(IgnoredPrograms: ["a.exe"]), new AppSettings(IgnoredPrograms: new List<string> { "a.exe" }));
        Assert.Equal(new AppSettings(), new AppSettings(IgnoredPrograms: []));
        Assert.NotEqual(new AppSettings(IgnoredPrograms: ["a.exe"]), new AppSettings(IgnoredPrograms: ["b.exe"]));
        Assert.NotEqual(new AppSettings(), new AppSettings(AskForNewPrograms: false));

        var store = new SettingsStore(_h.SettingsPath);
        var settings = new AppSettings(ShowOsd: false, AskForNewPrograms: false, IgnoredPrograms: ["a.exe", "b.exe"]);
        Directory.CreateDirectory(_h.Dir);
        store.Save(settings);
        Assert.Equal(settings, store.Load());
    }
}
