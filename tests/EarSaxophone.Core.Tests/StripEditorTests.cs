using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

/// <summary>The editor preselects "Wie Fader n" for "Stumm" on a key of a strip; the main view model shows resolved targets.</summary>
public class StripEditorTests
{
    private static readonly ControlId Fader4 = Cc(3);
    private static readonly ControlId Knob4 = Cc(19);
    private static readonly ControlId M4 = Cc(51);
    private static readonly ControlId Play = Cc(41);

    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    private static StripContext Strip4(params string[] exes) => new(Fader4, "Fader 4", exes.Select(AudioTarget.Application).ToList());

    private static MappingEditorViewModel Editor(ControlId control, ControlShape shape, ControlMapping? mapping, StripContext? strip)
    {
        var editor = new MappingEditorViewModel();
        editor.Load(control, shape, mapping, strip);
        return editor;
    }

    private static ControlMapping? Built(MappingEditorViewModel editor)
    {
        Assert.True(editor.TryBuild(out ControlMapping? mapping, out string? error), error);
        return mapping;
    }

    [Fact]
    public void ChoosingMute_OnAKeyOfAStripWithFaderTargets_PreselectsWieFader()
    {
        MappingEditorViewModel editor = Editor(M4, ControlShape.Button, null, Strip4("opera.exe", "b.exe"));
        Assert.Empty(editor.Targets);
        Assert.False(editor.IsDirty);

        editor.Kind = ActionKind.Mute;

        TargetEntry entry = Assert.Single(editor.Targets);
        Assert.True(entry.Target.IsStrip);
        Assert.Equal(TargetSymbol.Strip, entry.Symbol);
        Assert.Equal("Wie Fader 4 (opera.exe + 1)", entry.Name);
        Assert.True(editor.CanApply);
        Assert.False(editor.CanAddStripTarget); // already listed
        Assert.Equal(new ControlMapping(M4, new MuteAction(AudioTarget.Strip)), Built(editor));
    }

    [Fact]
    public void Preselection_CanBeRemoved_OwnTargetsChosen_AndWieFaderAddedAgain()
    {
        MappingEditorViewModel editor = Editor(M4, ControlShape.Button, null, Strip4("opera.exe"));
        editor.Kind = ActionKind.Mute;
        editor.MuteMode = MuteMode.MuteWhileHeld;

        Assert.True(editor.RemoveTarget(editor.Targets[0]));
        Assert.True(editor.CanAddStripTarget);
        editor.AddMicrophoneCommand.Execute(null);
        Assert.Equal(new ControlMapping(M4, new MuteAction(AudioTarget.Microphone, MuteMode.MuteWhileHeld)), Built(editor));

        editor.AddStripTargetCommand.Execute(null);
        Assert.Equal("Wie Fader 4 (opera.exe)", editor.Targets[1].Name);
        Assert.False(editor.AddTarget(AudioTarget.Strip)); // not twice
        Assert.Equal([new MuteAction(AudioTarget.Microphone, MuteMode.MuteWhileHeld), new MuteAction(AudioTarget.Strip, MuteMode.MuteWhileHeld)],
            Built(editor)!.Actions);
    }

    [Fact]
    public void NoPreselection_WhenTheFaderHasNoTargets_OrTargetsAreListed_OrTheKeyWasStartedByAddingATarget()
    {
        MappingEditorViewModel empty = Editor(M4, ControlShape.Button, null, Strip4());
        empty.Kind = ActionKind.Mute;
        Assert.Empty(empty.Targets);
        Assert.True(empty.CanAddStripTarget); // still choosable by hand
        Assert.Equal("Wie Fader 4 (noch ohne Ziel)", empty.StripTargetText);

        MappingEditorViewModel byAdding = Editor(M4, ControlShape.Button, null, Strip4("opera.exe"));
        byAdding.AddTarget(AudioTarget.Microphone); // starts "Stumm" by itself
        Assert.Equal(ActionKind.Mute, byAdding.Kind);
        Assert.Equal([TargetSymbol.Microphone], byAdding.Targets.Select(t => t.Symbol));

        MappingEditorViewModel existing = Editor(M4, ControlShape.Button, new ControlMapping(M4, new MuteAction(AudioTarget.Microphone)), Strip4("opera.exe"));
        existing.Kind = ActionKind.Media;
        existing.Kind = ActionKind.Mute;
        Assert.Equal([TargetSymbol.Microphone], existing.Targets.Select(t => t.Symbol));
        Assert.False(existing.IsDirty);
    }

    [Fact]
    public void Knob_GetsNoAutomaticTarget_ButMayChooseWieFader_AndTheFaderItselfCannot()
    {
        MappingEditorViewModel knob = Editor(Knob4, ControlShape.Continuous, null, Strip4("opera.exe"));
        Assert.Equal(ActionKind.SetVolume, knob.Kind);
        Assert.Empty(knob.Targets);
        Assert.True(knob.CanAddStripTarget);
        knob.AddStripTargetCommand.Execute(null);
        Assert.Equal(new ControlMapping(Knob4, new SetVolumeAction(AudioTarget.Strip)), Built(knob));

        MappingEditorViewModel fader = Editor(Fader4, ControlShape.Continuous, null, null);
        Assert.False(fader.CanAddStripTarget);
        Assert.False(fader.AddTarget(AudioTarget.Strip));
        Assert.Equal(Texts.UiStripTargetNotHere, fader.ValidationMessage);
    }

    [Fact]
    public void LoadedStripMapping_IsShownClean_AndOutsideAStripItCannotBeApplied()
    {
        var stored = new ControlMapping(M4, new MuteAction(AudioTarget.Strip, MuteMode.UnmuteWhileHeld));
        MappingEditorViewModel editor = Editor(M4, ControlShape.Button, stored, Strip4("x.exe"));
        Assert.Equal("Wie Fader 4 (x.exe)", Assert.Single(editor.Targets).Name);
        Assert.Equal(MuteMode.UnmuteWhileHeld, editor.MuteMode);
        Assert.False(editor.IsDirty);
        Assert.Equal(stored, Built(editor));

        MappingEditorViewModel play = Editor(Play, ControlShape.Button, stored with { Control = Play }, null);
        Assert.False(play.IsValid);
        Assert.Equal(Texts.UiStripTargetNotHere, play.ValidationMessage);
        Assert.Equal(Texts.TargetStripEmptyShort, Assert.Single(play.Targets).Name);
    }
}

/// <summary>"Wie Fader" through the main view model: editor context, summaries, badges, execution and drag and drop.</summary>
public sealed class StripMainViewModelTests : IDisposable
{
    private readonly ViewModelHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static ControllerElementViewModel E(MainViewModel vm, string label) => ViewModelHarness.Element(vm, label);

    [Fact]
    public void ChoosingMuteOnM4_PreselectsTheFader_AndM4FollowsAProgramAddedToTheFaderLater()
    {
        FakeSession a = _h.Audio.AddSession(@"C:\Games\a.exe", "spk");
        FakeSession b = _h.Audio.AddSession(@"C:\Games\b.exe", "spk");
        FakeSession c = _h.Audio.AddSession(@"C:\Games\c.exe", "spk");
        MainViewModel vm = _h.Create();
        ViewModelHarness.Assign(vm, "Fader 4", e => { e.AddApplication("a.exe"); e.AddApplication("b.exe"); });

        vm.Select(E(vm, "M 4"));
        Assert.Equal("Fader 4", vm.Editor.Strip!.FaderLabel);
        vm.Editor.Kind = ActionKind.Mute;
        Assert.Equal("Wie Fader 4 (a.exe + 1)", Assert.Single(vm.Editor.Targets).Name);
        vm.ApplyEditor();

        Assert.Equal(new ControlMapping(ViewModelHarness.Cc(51), new MuteAction(AudioTarget.Strip)), _h.SavedProfile().Find(ViewModelHarness.Cc(51)));
        Assert.Contains("a.exe", vm.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("Stumm schalten: a.exe (+1)", E(vm, "M 4").Summary);
        Assert.Equal(2, E(vm, "M 4").ActionCount);

        ViewModelHarness.Assign(vm, "Fader 4", e => e.AddApplication("c.exe")); // only the fader gets the third game
        Assert.Equal("Stumm schalten: a.exe (+2)", E(vm, "M 4").Summary);
        Assert.Equal(3, E(vm, "M 4").ActionCount);

        _h.Send(vm, 51, 127);
        Assert.True(a.Muted && b.Muted && c.Muted);
        Assert.False(_h.Speakers.Muted);
        Assert.True(E(vm, "M 4").IsMuteLit);

        vm.Select(E(vm, "Drehregler 4"));
        Assert.Empty(vm.Editor.Targets); // the knob is the special case: no automatic target
        vm.Select(E(vm, "Fader 4"));
        Assert.Null(vm.Editor.Strip);
    }

    [Fact]
    public void EmptyFader_PressingM_ShowsWhyNothingHappened()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Strip)));
        MainViewModel vm = _h.Create();
        Assert.Contains("wie Fader", E(vm, "M 1").Summary, StringComparison.Ordinal);

        _h.Send(vm, 48, 127);

        Assert.Contains("Fader 1", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.False(_h.Speakers.Muted);
        Assert.False(E(vm, "M 1").IsMuteLit);
    }

    [Fact]
    public void Drag_MovesWieFaderToAnotherStrip_WhereItMeansThatStripsFader()
    {
        _h.SaveProfile(
            new ControlMapping(ViewModelHarness.Cc(0), new SetVolumeAction(AudioTarget.Application("opera.exe"))),
            new ControlMapping(ViewModelHarness.Cc(2), new SetVolumeAction(AudioTarget.Application("game.exe"))),
            new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Strip)));
        MainViewModel vm = _h.Create();
        Assert.Equal("Stumm schalten: opera.exe", E(vm, "M 1").Summary);

        vm.TransferMapping(E(vm, "M 1"), E(vm, "M 3"), copy: false);
        Assert.Equal("Stumm schalten: game.exe", E(vm, "M 3").Summary);
        Assert.False(E(vm, "M 1").HasMapping);

        vm.TransferMapping(E(vm, "M 3"), E(vm, "Play"), copy: true);
        Assert.Equal([new MuteAction(AudioTarget.Application("game.exe"))], _h.SavedProfile().Find(ViewModelHarness.Cc(41))!.Actions);
    }
}
