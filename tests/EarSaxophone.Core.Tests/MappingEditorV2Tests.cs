using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

/// <summary>The editor of design V2: kinds by shape, a list of targets, mute modes, own name, dirty state, kept actions.</summary>
public class MappingEditorV2Tests
{
    private static readonly ControlId Fader = ViewModelHarness.Cc(3);
    private static readonly ControlId Key = ViewModelHarness.Cc(48);
    private static readonly ControlId Extra = ViewModelHarness.Cc(100);

    private static MappingEditorViewModel Editor(ControlId control, ControlShape shape, ControlMapping? mapping = null)
    {
        var editor = new MappingEditorViewModel();
        editor.Load(control, shape, mapping);
        return editor;
    }

    private static ControlMapping? Built(MappingEditorViewModel editor)
    {
        Assert.True(editor.TryBuild(out ControlMapping? mapping, out string? error), error);
        return mapping;
    }

    private static AudioTarget App(string exe) => AudioTarget.Application(exe);

    [Fact]
    public void KindOptions_OnlyOfferWhatFitsTheShape()
    {
        Assert.Equal([ActionKind.SetVolume], Editor(Fader, ControlShape.Continuous).KindOptions.Select(o => o.Value));
        Assert.Equal(
            [ActionKind.None, ActionKind.Mute, ActionKind.Media, ActionKind.SwitchDevice, ActionKind.Hotkey],
            Editor(Key, ControlShape.Button).KindOptions.Select(o => o.Value));
        Assert.Equal(6, Editor(Extra, ControlShape.Unknown).KindOptions.Count);
        Assert.False(Editor(Fader, ControlShape.Continuous).HasKindChoice);
        Assert.True(Editor(Key, ControlShape.Button).HasKindChoice);
        Assert.All(Editor(Key, ControlShape.Button).KindOptions, o => Assert.False(string.IsNullOrWhiteSpace(o.Text)));
    }

    [Theory]
    [InlineData(ControlShape.Continuous, ActionKind.Mute, false)]
    [InlineData(ControlShape.Continuous, ActionKind.Hotkey, false)]
    [InlineData(ControlShape.Continuous, ActionKind.SetVolume, true)]
    [InlineData(ControlShape.Button, ActionKind.SetVolume, false)]
    [InlineData(ControlShape.Button, ActionKind.SwitchDevice, true)]
    [InlineData(ControlShape.Unknown, ActionKind.SetVolume, true)]
    [InlineData(ControlShape.Unknown, ActionKind.Hotkey, true)]
    public void AKindThatDoesNotFit_IsRefused(ControlShape shape, ActionKind kind, bool allowed)
    {
        MappingEditorViewModel editor = Editor(Extra, shape);
        ActionKind before = editor.Kind;

        editor.Kind = kind;

        Assert.Equal(allowed, editor.IsKindAllowed(kind));
        Assert.Equal(allowed ? kind : before, editor.Kind);
    }

    [Fact]
    public void UnmappedFader_StartsWithVolume_IsClean_AndBecomesDirtyWithATarget()
    {
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous);
        Assert.Equal(ActionKind.SetVolume, editor.Kind);
        Assert.False(editor.IsDirty);
        Assert.Null(Built(editor));

        Assert.True(editor.AddTarget(AudioTarget.Speakers));

        Assert.True(editor.IsDirty && editor.IsValid && editor.CanApply);
        Assert.Equal(new ControlMapping(Fader, new SetVolumeAction(AudioTarget.Speakers)), Built(editor));
    }

    [Fact]
    public void TargetList_AddRemoveReplaceAndReorder_DecidesTheOrderOfTheActions()
    {
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous);
        editor.AddApplication("game1.exe");
        editor.AddApplication(@"C:\Games\game2.exe"); // a path is reduced to the program name
        editor.AddTarget(AudioTarget.Speakers);

        Assert.Equal(["game1.exe", "game2.exe", "Lautsprecher"], editor.Targets.Select(t => t.Name));
        Assert.Equal([TargetSymbol.Application, TargetSymbol.Application, TargetSymbol.Speakers], editor.Targets.Select(t => t.Symbol));

        Assert.True(editor.MoveTarget(2, 0));
        Assert.True(editor.ReplaceTarget(1, App("game3.exe")));
        Assert.True(editor.RemoveTarget(editor.Targets[2]));
        Assert.False(editor.MoveTarget(0, 5));

        Assert.Equal([new SetVolumeAction(AudioTarget.Speakers), new SetVolumeAction(App("game3.exe"))], Built(editor)!.Actions);

        editor.MoveTargetDownCommand.Execute(editor.Targets[0]);
        Assert.Equal(["game3.exe", "Lautsprecher"], editor.Targets.Select(t => t.Name));
        editor.RemoveTargetCommand.Execute(editor.Targets[1]);
        Assert.Equal(["game3.exe"], editor.Targets.Select(t => t.Name));
    }

    [Fact]
    public void TheSameTargetIsNotListedTwice_ProgramNamesIgnoreCaseAndExe()
    {
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous);
        Assert.True(editor.AddApplication("Discord.exe"));

        Assert.False(editor.AddApplication("discord"));
        Assert.Contains("bereits", editor.ValidationMessage);
        Assert.True(editor.AddTarget(AudioTarget.Microphone));
        Assert.False(editor.AddTarget(AudioTarget.Microphone));
        Assert.False(editor.ReplaceTarget(1, App("DISCORD.EXE")));
        Assert.False(editor.AddApplication("  "));
        Assert.False(editor.AddTarget(new AudioTarget(AudioTargetKind.SpecificDevice)));

        Assert.Equal(2, editor.Targets.Count);
    }

    [Fact]
    public void AddAppCommand_TakesTheTypedName_AndClearsTheField()
    {
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous);
        editor.NewAppName = "Spotify.exe";

        editor.AddAppCommand.Execute(null);
        editor.AddAppCommand.Execute("Discord.exe");

        Assert.Equal(["Spotify.exe", "Discord.exe"], editor.Targets.Select(t => t.Name));
        Assert.Equal(string.Empty, editor.NewAppName);
    }

    [Fact]
    public void RemovingEveryTarget_MeansNoMapping()
    {
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous, new ControlMapping(Fader, new SetVolumeAction(AudioTarget.Speakers)));

        editor.RemoveTarget(editor.Targets.Single());

        Assert.True(editor.IsDirty && editor.CanApply);
        Assert.Null(Built(editor));
    }

    [Fact]
    public void ChangingSomethingBack_IsNotDirty_AndRevertRestoresTheLoadedMapping()
    {
        var mapping = new ControlMapping(Fader, [new SetVolumeAction(App("a.exe")), new SetVolumeAction(App("b.exe"))], new ValueScaling(0.1f, 0.9f, true), "Spiele");
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous, mapping);
        Assert.False(editor.IsDirty);

        editor.MaxPercent = 50;
        Assert.True(editor.IsDirty);
        editor.MaxPercent = 90;
        Assert.False(editor.IsDirty);

        editor.MoveTarget(0, 1);
        editor.Name = "Andere";
        editor.Invert = false;
        Assert.True(editor.IsDirty);

        editor.Revert();

        Assert.False(editor.IsDirty);
        Assert.Equal(mapping, Built(editor));
        Assert.Equal(["a.exe", "b.exe"], editor.Targets.Select(t => t.Name));
    }

    [Fact]
    public void InvalidInput_IsDirtyButCannotBeApplied()
    {
        MappingEditorViewModel editor = Editor(Key, ControlShape.Button);
        editor.Kind = ActionKind.Hotkey;
        editor.HotkeyText = "Ctrl+Banana";

        Assert.True(editor.IsDirty);
        Assert.False(editor.IsValid);
        Assert.False(editor.CanApply);
        Assert.Contains("Banana", editor.ValidationMessage);

        editor.HotkeyText = "strg+alt+m";
        Assert.True(editor.CanApply);
        Assert.Null(editor.ValidationMessage);
        Assert.Equal(new HotkeyAction("Ctrl+Alt+M"), Built(editor)!.Actions.Single());

        MappingEditorViewModel fader = Editor(Fader, ControlShape.Continuous);
        fader.AddTarget(AudioTarget.Speakers);
        fader.MinPercent = 80;
        fader.MaxPercent = 20;
        Assert.False(fader.CanApply);
        Assert.Contains("Minimum", fader.ValidationMessage);
    }

    [Fact]
    public void MuteKey_WithSeveralTargetsAndAMode_BuildsOneMuteActionPerTarget()
    {
        MappingEditorViewModel editor = Editor(Key, ControlShape.Button);

        editor.AddTarget(AudioTarget.Microphone); // a target on a key without action starts "mute"
        Assert.Equal(ActionKind.Mute, editor.Kind);
        editor.AddApplication("Discord.exe");
        editor.MuteMode = MuteMode.UnmuteWhileHeld;

        Assert.Equal(3, editor.MuteModeOptions.Count);
        Assert.Equal(
            [new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld), new MuteAction(App("Discord.exe"), MuteMode.UnmuteWhileHeld)],
            Built(editor)!.Actions);
    }

    [Fact]
    public void OwnName_IsOptional_AndTrimmed()
    {
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous);
        editor.AddApplication("game1.exe");

        editor.Name = "   ";
        Assert.Null(Built(editor)!.Name);
        editor.Name = "  Spiele ";
        Assert.Equal("Spiele", Built(editor)!.Name);
    }

    [Fact]
    public void LoadedMappings_BuildBackUnchanged_SoNothingIsLostByOpeningThem()
    {
        ControlMapping[] mappings =
        [
            new(Fader, [new SetVolumeAction(new AudioTarget(AudioTargetKind.Application, App: new AppSelector("game.exe", @"C:\Games\game.exe"))),
                new SetVolumeAction(new AudioTarget(AudioTargetKind.SpecificDevice, DeviceId: "hs"))], new ValueScaling(0.2f, 0.8f, false), "Spiele"),
            new(Key, [new MuteAction(AudioTarget.Speakers), new MuteAction(App("Discord.exe"))]),
            new(Key, new MuteAction(new AudioTarget(AudioTargetKind.DefaultDevice, DeviceFlow.Capture, DeviceRole.Communications), MuteMode.MuteWhileHeld), Name: "Funk"),
            new(Key, new SwitchDeviceAction(DeviceFlow.Capture, ["gone", "mic"])),
            new(Key, new MediaAction(MediaCommand.Stop)),
            new(Key, new HotkeyAction("Ctrl+Alt+M")),
            new(Key, [new MuteAction(AudioTarget.Speakers), new MediaAction(MediaCommand.Next), new MuteAction(AudioTarget.Microphone, MuteMode.MuteWhileHeld)]),
        ];

        foreach (ControlMapping mapping in mappings)
        {
            MappingEditorViewModel editor = Editor(mapping.Control, Devices.Nanokontrol2Layout.ShapeOf(mapping.Control), mapping);
            Assert.False(editor.IsDirty, MappingDescriberText(mapping));
            Assert.Equal(mapping, Built(editor));
        }
    }

    [Fact]
    public void ActionsTheEditorCannotShow_AreKeptUntilDropped()
    {
        var mapping = new ControlMapping(Key, [new MuteAction(AudioTarget.Speakers), new MediaAction(MediaCommand.Next), new MuteAction(AudioTarget.Microphone, MuteMode.MuteWhileHeld)]);
        MappingEditorViewModel editor = Editor(Key, ControlShape.Button, mapping);

        Assert.Equal(["Lautsprecher"], editor.Targets.Select(t => t.Name));
        Assert.True(editor.HasKeptActions);
        Assert.Equal(2, editor.KeptActions.Count);
        Assert.Contains("2 weitere Aktionen", editor.KeptActionsText);

        editor.AddTarget(AudioTarget.Microphone);
        Assert.Equal(
            [new MuteAction(AudioTarget.Speakers), new MuteAction(AudioTarget.Microphone), new MediaAction(MediaCommand.Next), new MuteAction(AudioTarget.Microphone, MuteMode.MuteWhileHeld)],
            Built(editor)!.Actions);

        editor.DropKeptActionsCommand.Execute(null);

        Assert.False(editor.HasKeptActions);
        Assert.Equal([new MuteAction(AudioTarget.Speakers), new MuteAction(AudioTarget.Microphone)], Built(editor)!.Actions);
    }

    [Fact]
    public void AStoredMappingThatDoesNotFitTheControl_IsShownAsInvalid_NotSilentlyChanged()
    {
        var wrong = new ControlMapping(Key, new SetVolumeAction(AudioTarget.Speakers)); // volume on a key (hand-edited file)
        MappingEditorViewModel editor = Editor(Key, ControlShape.Button, wrong);

        Assert.False(editor.IsDirty);
        Assert.False(editor.IsValid);
        Assert.Contains("passt nicht zu einer Taste", editor.ValidationMessage);
        Assert.Equal(ActionKind.None, editor.Kind);

        editor.DropKeptActions();
        Assert.True(editor.IsDirty && editor.CanApply);
        Assert.Null(Built(editor));
    }

    [Fact]
    public void DeviceRotation_TickingMakesDirty_UntickingMakesCleanAgain()
    {
        MappingEditorViewModel editor = Editor(Key, ControlShape.Button, new ControlMapping(Key, new SwitchDeviceAction(DeviceFlow.Render, ["spk"])));
        editor.SetAvailableDevices([("spk", "Lautsprecher", DeviceFlow.Render), ("hs", "Kopfhörer", DeviceFlow.Render), ("mic", "Mikrofon", DeviceFlow.Capture)]);
        Assert.False(editor.IsDirty);

        DeviceChoice headset = editor.DeviceChoices.Single(c => c.Id == "hs");
        headset.IsSelected = true;
        Assert.True(editor.IsDirty);
        Assert.Equal(["spk", "hs"], ((SwitchDeviceAction)Built(editor)!.Actions[0]).SelectedIds);

        headset.IsSelected = false;
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void SpecificDeviceTarget_ShowsTheDeviceName_AndKeepsTheDevice()
    {
        var target = new AudioTarget(AudioTargetKind.SpecificDevice, DeviceId: "hs");
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous, new ControlMapping(Fader, new SetVolumeAction(target)));
        Assert.Equal(("Gerät", TargetSymbol.Device), (editor.Targets[0].Name, editor.Targets[0].Symbol));

        editor.SetAvailableDevices([("hs", "Kopfhörer", DeviceFlow.Render)]);

        Assert.Equal("Kopfhörer", editor.Targets[0].Name);
        Assert.Equal(target, editor.Targets[0].Target);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void KnownPrograms_GiveTheirPathForTheIcon()
    {
        MappingEditorViewModel editor = Editor(Fader, ControlShape.Continuous);
        editor.SetAvailablePrograms([new ProgramInfo("Discord.exe", @"C:\Discord\Discord.exe")]);

        editor.AddApplication("discord.exe");

        Assert.Equal(@"C:\Discord\Discord.exe", editor.Targets[0].ExePath);
        Assert.Equal(["Discord.exe"], editor.AvailableApps);
    }

    [Fact]
    public void TheEditorKnowsNoTestCommand()
    {
        Assert.Null(typeof(MappingEditorViewModel).GetProperty("TestCommand"));
    }

    private static string MappingDescriberText(ControlMapping mapping) => Localization.MappingDescriber.Describe(mapping);
}
