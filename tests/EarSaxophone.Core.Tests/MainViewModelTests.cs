using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Input;
using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "earsax-vm-" + Guid.NewGuid().ToString("N"));
    private readonly FakeMidiProvider _provider = new();
    private readonly FakeAudioSystem _audio = new();
    private readonly RecordingKeyboard _keyboard = new();
    private readonly FakeAutostart _autostart = new();
    private readonly List<IDisposable> _disposables = [];
    private FakeSession _discord = null!;
    private FakeEndpoint _speakers = null!;

    public MainViewModelTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (IDisposable d in _disposables)
        {
            d.Dispose();
        }

        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string ProfilePath => Path.Combine(_dir, "profiles.json");

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    private MainViewModel Create(bool withMic = true, bool deviceConnected = true)
    {
        _speakers = _audio.AddEndpoint("spk", "Lautsprecher", DeviceFlow.Render);
        if (withMic)
        {
            _audio.AddEndpoint("mic", "Mikrofon", DeviceFlow.Capture);
        }

        _discord = _audio.AddSession(@"C:\Users\x\AppData\Local\Discord\app-1\Discord.exe", "spk");
        if (deviceConnected)
        {
            _provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        }

        var midi = new MidiConnectionManager(_provider);
        var executor = new ActionExecutor(new AudioController(_audio), _keyboard);
        var vm = new MainViewModel(new ProfileStore(ProfilePath), midi, executor, _audio, new ImmediateUi(),
            new SettingsStore(SettingsPath), _autostart);
        _disposables.Add(midi);
        _disposables.Add(vm);
        vm.Tick();
        return vm;
    }

    /// <summary>Sends a MIDI message from the fake device and waits until the view model has processed it (it works on a worker thread).</summary>
    private void Send(MainViewModel vm, byte cc, byte value)
    {
        _provider.Ports[0].Send(cc, value);
        Assert.True(vm.WaitUntilIdle(TimeSpan.FromSeconds(10)), "MIDI messages were not processed in time");
    }

    private static ControllerElementViewModel Element(MainViewModel vm, string label) =>
        vm.Elements.Concat(vm.ExtraElements).Single(e => e.Label == label);

    private void Assign(MainViewModel vm, string label, Action<MappingEditorViewModel> configure)
    {
        vm.Select(Element(vm, label));
        configure(vm.Editor);
        vm.ApplyEditor();
    }

    [Fact]
    public void Startup_ShowsCompleteStandardLayout_WithNothingAssigned()
    {
        MainViewModel vm = Create();

        Assert.Equal(51, vm.Elements.Count);
        Assert.All(vm.Elements, e => Assert.False(e.HasMapping));
        Assert.Empty(vm.MappedElements);
        Assert.Equal("Default", vm.ActiveProfileName);
        Assert.False(vm.HasSelection);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void Elements_KnowTheirPlaceInThePicture_AndFollowTheirValue()
    {
        MainViewModel vm = Create();
        ControllerElementViewModel fader = Element(vm, "Fader 1");
        ControllerElementViewModel knob = Element(vm, "Drehregler 1");
        ControllerElementViewModel play = Element(vm, "Play");

        Assert.Equal(Core.Devices.ElementKind.Fader, fader.Kind);
        Assert.True(fader.Height > fader.Width && fader.X > Element(vm, "Stopp").X);

        Send(vm, 0, 127);
        Send(vm, 16, 0);
        Send(vm, 41, 127);
        Assert.Equal(0, fader.CapOffset, 3);                        // fader at maximum: cap at the top
        Assert.Equal(-135, knob.KnobAngle, 3);                      // knob at minimum
        Assert.True(play.IsActive);                                 // key held down

        Send(vm, 0, 0);
        Send(vm, 16, 127);
        Send(vm, 41, 0);
        Assert.Equal(ControllerElementViewModel.FaderTravel, fader.CapOffset, 3);
        Assert.Equal(135, knob.KnobAngle, 3);
        Assert.False(play.IsActive);
    }

    [Fact]
    public void MappedElements_ListsExactlyTheAssignedControls_InPictureOrder()
    {
        MainViewModel vm = Create();
        Assign(vm, "Fader 2", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Speakers; });
        Assign(vm, "Play", e => { e.Kind = ActionKind.Media; e.Media = MediaCommand.PlayPause; });

        Assert.Equal(["Play", "Fader 2"], vm.MappedElements.Select(e => e.Label));
        Assert.Contains("Lautstärke", Element(vm, "Fader 2").TooltipText);
        Assert.Equal("Fader 2 · Lautstärke: Lautsprecher", Element(vm, "Fader 2").ListText);

        vm.Select(Element(vm, "Play"));
        vm.RemoveSelectedMapping();
        Assert.Equal(["Fader 2"], vm.MappedElements.Select(e => e.Label));
    }

    [Fact]
    public void ConnectionText_FollowsTheDevice()
    {
        MainViewModel vm = Create(deviceConnected: false);
        Assert.False(vm.IsConnected);
        Assert.Contains("nicht gefunden", vm.ConnectionText);

        _provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        vm.Tick();
        Assert.True(vm.IsConnected);
        Assert.Contains("Verbunden", vm.ConnectionText);

        _provider.Devices.Clear();
        vm.Tick();
        Assert.False(vm.IsConnected);
    }

    [Fact]
    public void ReconnectCommand_OpensTheDeviceAgain_AndTheOldPortGoesSilent()
    {
        MainViewModel vm = Create();
        FakeMidiPort stale = _provider.Ports[0];
        Assign(vm, "Fader 1", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Speakers; });

        vm.ReconnectCommand.Execute(null);
        stale.Send(cc: 0, value: 127);
        Assert.True(vm.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        Assert.Equal(0.5f, _speakers.Volume, 5);

        Assert.Equal(2, _provider.Ports.Count);
        _provider.Ports[1].Send(cc: 0, value: 127); // the new port
        Assert.True(vm.WaitUntilIdle(TimeSpan.FromSeconds(5)));
        Assert.Equal(1f, _speakers.Volume, 5);
        Assert.True(vm.IsConnected);
    }

    [Fact]
    public void MidiDriverWarning_IsShownInTheStatusLine()
    {
        MainViewModel vm = Create();

        _provider.Ports[0].RaiseFault("Der MIDI-Treiber meldete eine ungültige Nachricht (0x00000001).");

        Assert.Contains("MIDI-Treiber", vm.StatusMessage);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void MidiLearn_SelectsTheTouchedStandardElement_WithoutRunningAnyAction()
    {
        MainViewModel vm = Create();
        Assign(vm, "Fader 1", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Speakers; });
        vm.Select(null);
        _speakers.Volume = 0.5f;

        vm.StartLearning();
        Assert.True(vm.IsLearning);
        Send(vm, 0, 120);

        Assert.False(vm.IsLearning);
        Assert.Equal("Fader 1", vm.SelectedElement?.Label);
        Assert.Contains("Regler erkannt", vm.StatusMessage);
        Assert.Equal(0.5f, _speakers.Volume, 5); // learning consumed the message
    }

    [Fact]
    public void MidiLearn_UnknownControl_BecomesExtraElement()
    {
        MainViewModel vm = Create();

        vm.StartLearning();
        Send(vm, 100, 90);

        Assert.True(vm.HasExtraElements);
        Assert.Equal(vm.ExtraElements[0], vm.SelectedElement);
        Assert.Contains("CC 100", vm.SelectedElement!.Label);
    }

    [Fact]
    public void CancelLearning_StopsLearning()
    {
        MainViewModel vm = Create();
        vm.StartLearning();
        vm.CancelLearning();
        Send(vm, 0, 100);

        Assert.False(vm.IsLearning);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void AssignedFader_ControlsApplicationVolume_UpdatesTile_AndSurvivesRestart()
    {
        MainViewModel vm = Create();
        Assert.Contains("Discord.exe", vm.Editor.AvailableApps);

        Assign(vm, "Fader 1", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Application; e.AppName = "Discord.exe"; });

        ControllerElementViewModel tile = Element(vm, "Fader 1");
        Assert.Equal("Lautstärke: Discord.exe", tile.Summary);
        Assert.True(tile.HasMapping && tile.IsContinuous);
        Send(vm, 0, 127);
        Assert.Equal(1f, _discord.Volume, 5);
        Assert.Equal(127, tile.LastValue);
        Send(vm, 0, 0);
        Assert.Equal(0f, _discord.Volume, 5);

        // A new program start reads the saved profile.
        ProfileLoadResult reloaded = new ProfileStore(ProfilePath).Load();
        Assert.Null(reloaded.Warning);
        Assert.IsType<SetVolumeAction>(reloaded.Document.Profiles[0].Find(tile.Control)!.Action);
    }

    [Fact]
    public void MicrophoneMuteButton_TogglesTheRecordingDevice_NotTheSpeakers()
    {
        MainViewModel vm = Create();
        Assign(vm, "M 1", e => { e.Kind = ActionKind.ToggleMute; e.Target = TargetKind.Microphone; });

        Send(vm, 48, 127);
        Send(vm, 48, 0);

        FakeEndpoint mic = _audio.Endpoints.Single(e => e.Flow == DeviceFlow.Capture);
        Assert.True(mic.Muted);
        Assert.False(_speakers.Muted);
    }

    [Fact]
    public void MediaButton_SendsTheCommandOncePerPress_AndShowsGermanStatus()
    {
        MainViewModel vm = Create();
        Assign(vm, "Play", e => { e.Kind = ActionKind.Media; e.Media = MediaCommand.PlayPause; });

        Send(vm, 41, 127);
        Send(vm, 41, 127);
        Send(vm, 41, 0);

        Assert.Equal([MediaCommand.PlayPause], _keyboard.Media);
        Assert.Contains("Wiedergabe/Pause", vm.StatusMessage);
    }

    [Fact]
    public void Hotkey_IsStoredInCanonicalForm_AndInvalidInputIsRejectedWithoutSaving()
    {
        MainViewModel vm = Create();
        vm.Select(Element(vm, "Cycle"));
        vm.Editor.Kind = ActionKind.Hotkey;

        vm.Editor.HotkeyText = "Ctrl+Banana";
        vm.ApplyEditor();
        Assert.Contains("Banana", vm.Editor.ValidationMessage);
        Assert.Null(vm.Current.Find(Element(vm, "Cycle").Control));
        Assert.False(File.Exists(ProfilePath));

        vm.Editor.HotkeyText = "strg+alt+m";
        vm.ApplyEditor();
        Assert.Equal("Tasten: Ctrl+Alt+M", Element(vm, "Cycle").Summary);
        Send(vm, 46, 127);
        Assert.Equal(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, 0x4D), _keyboard.Hotkeys.Single());
    }

    [Fact]
    public void ApplicationTarget_WithoutName_IsRejected()
    {
        MainViewModel vm = Create();
        vm.Select(Element(vm, "Fader 2"));
        vm.Editor.Kind = ActionKind.SetVolume;
        vm.Editor.Target = TargetKind.Application;

        vm.ApplyEditor();

        Assert.Contains("Anwendung", vm.Editor.ValidationMessage);
        Assert.False(Element(vm, "Fader 2").HasMapping);
    }

    [Fact]
    public void VolumeRange_AndInvert_AreAppliedToTheFader()
    {
        MainViewModel vm = Create();
        Assign(vm, "Fader 3", e =>
        {
            e.Kind = ActionKind.SetVolume;
            e.Target = TargetKind.Speakers;
            e.Invert = true;
            e.MinPercent = 20;
            e.MaxPercent = 80;
        });

        Send(vm, 2, 0);
        Assert.Equal(0.8f, _speakers.Volume, 3);
        Send(vm, 2, 127);
        Assert.Equal(0.2f, _speakers.Volume, 3);

        vm.Select(Element(vm, "Fader 3"));
        Assert.True(vm.Editor.Invert);
        Assert.Equal(20, vm.Editor.MinPercent);
        Assert.Equal(80, vm.Editor.MaxPercent);

        vm.Editor.MinPercent = 90;
        vm.ApplyEditor();
        Assert.Contains("Minimum", vm.Editor.ValidationMessage);
    }

    [Fact]
    public void RemoveMapping_ClearsTileAndStopsTheAction()
    {
        MainViewModel vm = Create();
        Assign(vm, "Fader 1", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Speakers; });

        vm.Select(Element(vm, "Fader 1"));
        vm.RemoveSelectedMapping();

        Assert.False(Element(vm, "Fader 1").HasMapping);
        _speakers.Volume = 0.3f;
        Send(vm, 0, 127);
        Assert.Equal(0.3f, _speakers.Volume, 5);
    }

    [Fact]
    public void ChoosingNoAction_RemovesAnExistingMapping()
    {
        MainViewModel vm = Create();
        Assign(vm, "Stopp", e => { e.Kind = ActionKind.Media; e.Media = MediaCommand.Stop; });

        Assign(vm, "Stopp", e => e.Kind = ActionKind.None);

        Assert.False(Element(vm, "Stopp").HasMapping);
        Assert.Contains("entfernt", vm.StatusMessage);
    }

    [Fact]
    public void TestButton_RunsTheActionOnce_WithoutSaving()
    {
        MainViewModel vm = Create();
        vm.Select(Element(vm, "Marker ►"));
        vm.Editor.Kind = ActionKind.Media;
        vm.Editor.Media = MediaCommand.Next;

        vm.TestEditor();

        Assert.Equal([MediaCommand.Next], _keyboard.Media);
        Assert.False(Element(vm, "Marker ►").HasMapping);
        Assert.False(File.Exists(ProfilePath));
    }

    [Fact]
    public void FailingAction_ShowsReadableError_UntilDismissedOrReplacedBySuccess()
    {
        MainViewModel vm = Create(withMic: false);
        Assign(vm, "Fader 4", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Microphone; });
        Assign(vm, "Fader 5", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Speakers; });

        Send(vm, 3, 100);
        Assert.True(vm.HasError);
        Assert.Contains("Fader 4", vm.ErrorMessage);
        Assert.Contains("Aufnahmegerät", vm.ErrorMessage);

        vm.DismissErrorCommand.Execute(null);
        Assert.False(vm.HasError);

        Send(vm, 3, 100);
        Assert.True(vm.HasError);
        Send(vm, 4, 100);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void Profiles_CanBeCreated_Switched_Renamed_AndDeleted()
    {
        MainViewModel vm = Create();
        Assign(vm, "Fader 1", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Speakers; });

        vm.NewProfile();
        Assert.Equal("Profil 2", vm.ActiveProfileName);
        Assert.Equal(["Default", "Profil 2"], vm.ProfileNames);
        Assert.False(Element(vm, "Fader 1").HasMapping);
        _speakers.Volume = 0.3f;
        Send(vm, 0, 127);
        Assert.Equal(0.3f, _speakers.Volume, 5); // new profile has no mapping

        vm.ActiveProfileName = "Default";
        Assert.True(Element(vm, "Fader 1").HasMapping);
        Send(vm, 0, 127);
        Assert.Equal(1f, _speakers.Volume, 5);

        vm.RenameActive("Streaming");
        Assert.Equal("Streaming", vm.ActiveProfileName);
        Assert.Equal(["Streaming", "Profil 2"], vm.ProfileNames);

        ProfileDocument saved = new ProfileStore(ProfilePath).Load().Document;
        Assert.Equal("Streaming", saved.ActiveProfile);
        Assert.Equal(2, saved.Profiles.Count);

        vm.DeleteProfile();
        Assert.Equal(["Profil 2"], vm.ProfileNames);
        Assert.Equal("Profil 2", vm.ActiveProfileName);
    }

    [Fact]
    public void Profiles_InvalidRenameAndLastDelete_ShowErrors()
    {
        MainViewModel vm = Create();
        vm.NewProfile();

        vm.RenameActive("Default");
        Assert.Contains("bereits ein Profil", vm.ErrorMessage);
        Assert.Equal("Profil 2", vm.ActiveProfileName);

        vm.RenameActive("   ");
        Assert.Contains("nicht leer", vm.ErrorMessage);

        vm.DeleteProfile();
        vm.DeleteProfile();
        Assert.Contains("letzte Profil", vm.ErrorMessage);
        Assert.Single(vm.ProfileNames);
    }

    [Fact]
    public void CorruptProfileFile_StartsWithDefaults_AndExplainsWhy()
    {
        File.WriteAllText(ProfilePath, "{ kaputt");

        MainViewModel vm = Create();

        Assert.True(vm.HasError);
        Assert.Contains("konnten nicht geladen werden", vm.ErrorMessage);
        Assert.True(File.Exists(ProfilePath + ".corrupt"));
        Assert.Equal("Default", vm.ActiveProfileName);
    }

    [Fact]
    public void SaveFailure_IsReported_NotThrown()
    {
        Directory.CreateDirectory(ProfilePath); // a folder where the file should be
        MainViewModel vm = Create();

        Assign(vm, "Fader 1", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Speakers; });

        Assert.Contains("nicht gespeichert", vm.ErrorMessage);
    }

    private void AddMoreDevices()
    {
        _audio.AddEndpoint("hs", "Kopfhörer", DeviceFlow.Render, makeDefault: false);
        _audio.AddEndpoint("hdmi", "Monitor (HDMI)", DeviceFlow.Render, makeDefault: false);
        _audio.AddEndpoint("mic2", "Headset-Mikrofon", DeviceFlow.Capture, makeDefault: false);
    }

    [Fact]
    public void SwitchKey_IsConfiguredInTheEditor_AndSwitchesTheOutputDevice()
    {
        MainViewModel vm = Create();
        AddMoreDevices();
        vm.RefreshApps();

        vm.Select(Element(vm, "Track ◄"));
        vm.Editor.Kind = ActionKind.SwitchDevice;
        Assert.Equal(["Kopfhörer", "Lautsprecher", "Monitor (HDMI)"], vm.Editor.DeviceChoices.Select(c => c.Name));
        vm.Editor.DeviceChoices.Single(c => c.Id == "hs").IsSelected = true;
        vm.Editor.DeviceChoices.Single(c => c.Id == "spk").IsSelected = true;
        vm.ApplyEditor();

        Assert.Equal("Wiedergabegerät wechseln (2 ausgewählt)", Element(vm, "Track ◄").Summary);
        var osd = new List<OsdMessage>();
        vm.OsdRequested += osd.Add;
        Send(vm, 58, 127);
        Send(vm, 58, 0);
        Assert.Equal("Kopfhörer", _audio.GetDefaultEndpoint(DeviceFlow.Render, DeviceRole.Console)!.Name);
        Send(vm, 58, 127);
        Assert.Equal("Lautsprecher", _audio.GetDefaultEndpoint(DeviceFlow.Render, DeviceRole.Console)!.Name);
        Assert.Equal("Wiedergabegerät", osd[0].Title);
        Assert.Equal("Kopfhörer", osd[0].Detail);
        Assert.Null(osd[0].Level);
    }

    [Fact]
    public void SwitchKey_WithoutTickedDevices_RotatesThroughAllDevices_AndTheMicrophoneListIsSeparate()
    {
        MainViewModel vm = Create();
        AddMoreDevices();
        vm.RefreshApps();
        vm.Select(Element(vm, "Track ►"));
        vm.Editor.Kind = ActionKind.SwitchDevice;
        vm.Editor.SwitchFlow = DeviceFlow.Capture;

        Assert.Equal(["Headset-Mikrofon", "Mikrofon"], vm.Editor.DeviceChoices.Select(c => c.Name));
        vm.ApplyEditor();

        var action = Assert.IsType<SwitchDeviceAction>(vm.Current.Find(Element(vm, "Track ►").Control)!.Action);
        Assert.Equal(DeviceFlow.Capture, action.Flow);
        Assert.Empty(action.SelectedIds);
        Assert.Equal("Aufnahmegerät wechseln", Element(vm, "Track ►").Summary);

        Send(vm, 59, 127);
        Assert.Equal("Headset-Mikrofon", _audio.GetDefaultEndpoint(DeviceFlow.Capture, DeviceRole.Console)!.Name);
        Assert.Equal("Lautsprecher", _audio.GetDefaultEndpoint(DeviceFlow.Render, DeviceRole.Console)!.Name);
    }

    [Fact]
    public void TickedDevices_SurviveARefresh_AndAnUnpluggedDeviceStaysInTheRotation()
    {
        MainViewModel vm = Create();
        AddMoreDevices();
        vm.RefreshApps();
        vm.Select(Element(vm, "Cycle"));
        vm.Editor.Kind = ActionKind.SwitchDevice;
        vm.Editor.DeviceChoices.Single(c => c.Id == "hs").IsSelected = true;

        vm.RefreshApps();                       // e.g. the "Aktualisieren" button
        Assert.True(vm.Editor.DeviceChoices.Single(c => c.Id == "hs").IsSelected);

        vm.ApplyEditor();
        _audio.Endpoints.RemoveAll(e => e.Id == "hs");   // headphones unplugged
        vm.RefreshApps();
        vm.Select(Element(vm, "Cycle"));
        Assert.DoesNotContain(vm.Editor.DeviceChoices, c => c.Id == "hs");
        vm.ApplyEditor();

        var action = Assert.IsType<SwitchDeviceAction>(vm.Current.Find(Element(vm, "Cycle").Control)!.Action);
        Assert.Contains("hs", action.SelectedIds);
    }

    [Fact]
    public void OnScreenDisplay_ShowsLevelTargetAndMuteState_ForVolumeKeys()
    {
        MainViewModel vm = Create();
        Assign(vm, "Fader 1", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Application; e.AppName = "Discord.exe"; });
        Assign(vm, "Fader 2", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Speakers; });
        Assign(vm, "M 1", e => { e.Kind = ActionKind.ToggleMute; e.Target = TargetKind.Microphone; });
        Assign(vm, "Play", e => { e.Kind = ActionKind.Media; e.Media = MediaCommand.PlayPause; });
        Assign(vm, "Cycle", e => { e.Kind = ActionKind.Hotkey; e.HotkeyText = "Ctrl+Alt+M"; });
        var shown = new List<OsdMessage>();
        vm.OsdRequested += shown.Add;

        Send(vm, 0, 127);
        Send(vm, 1, 64);
        Send(vm, 48, 127);   // mute
        Send(vm, 48, 0);     // release: no action
        Send(vm, 48, 127);   // unmute
        Send(vm, 41, 127);
        Send(vm, 46, 127);   // shortcut: no display

        Assert.Equal(5, shown.Count);
        Assert.Equal(("Discord.exe", 1.0), (shown[0].Title, shown[0].Level));
        Assert.Equal("Lautsprecher", shown[1].Title);
        Assert.Equal(64 / 127.0, shown[1].Level!.Value, 3);
        Assert.Equal(("Mikrofon", "Stumm", true), (shown[2].Title, shown[2].Detail, shown[2].Muted));
        Assert.Equal(("Ton an", false), (shown[3].Detail, shown[3].Muted));
        Assert.Equal(("Medien", "Wiedergabe/Pause"), (shown[4].Title, shown[4].Detail));
    }

    [Fact]
    public void OnScreenDisplay_IsSilentWhenSwitchedOff_OrWhenTheActionFailed_AndMarksInactiveApps()
    {
        MainViewModel vm = Create(withMic: false);
        Assign(vm, "Fader 3", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Microphone; });
        Assign(vm, "Fader 4", e => { e.Kind = ActionKind.SetVolume; e.Target = TargetKind.Application; e.AppName = "Spotify.exe"; });
        var shown = new List<OsdMessage>();
        vm.OsdRequested += shown.Add;

        Send(vm, 2, 100);   // no microphone: failure, no display
        Assert.Empty(shown);

        Send(vm, 3, 100);   // Spotify is not running: remembered, shown as inactive
        Assert.Equal("nicht aktiv", Assert.Single(shown).Detail);

        shown.Clear();
        vm.ShowOsd = false;
        Send(vm, 3, 50);
        Assert.Empty(shown);
    }

    [Fact]
    public void OnScreenDisplaySettings_ArePersisted_AndTheTestButtonShowsASample()
    {
        MainViewModel vm = Create();
        Assert.True(vm.ShowOsd);
        Assert.Equal(OsdPosition.BottomRight, vm.OsdPosition);
        Assert.Equal(6, vm.OsdPositionOptions.Count);
        Assert.Contains(vm.OsdPositionOptions, o => o.Text == "Oben links" && o.Value == OsdPosition.TopLeft);

        vm.OsdPosition = OsdPosition.TopLeft;
        vm.ShowOsd = false;

        AppSettings stored = new SettingsStore(SettingsPath).Load();
        Assert.Equal(OsdPosition.TopLeft, stored.OsdPosition);
        Assert.False(stored.ShowOsd);
        Assert.Contains("\"osdPosition\": \"topLeft\"", File.ReadAllText(SettingsPath));

        var shown = new List<OsdMessage>();
        vm.OsdRequested += shown.Add;
        vm.TestOsdCommand.Execute(null);
        Assert.Single(shown);
    }

    [Fact]
    public void OldSettingsFileWithoutOsdFields_LoadsWithDefaults()
    {
        File.WriteAllText(SettingsPath, "{ \"keepInTray\": false }");

        AppSettings loaded = new SettingsStore(SettingsPath).Load();

        Assert.False(loaded.KeepInTray);
        Assert.True(loaded.ShowOsd);
        Assert.Equal(OsdPosition.BottomRight, loaded.OsdPosition);
    }

    [Fact]
    public void Settings_ArePersisted_AndAutostartIsSwitched()
    {
        MainViewModel vm = Create();
        Assert.True(vm.KeepInTray);
        Assert.False(vm.StartWithWindows);

        vm.StartWithWindows = true;
        vm.KeepInTray = false;
        vm.StartMinimized = true;

        Assert.True(_autostart.IsEnabled);
        AppSettings stored = new SettingsStore(SettingsPath).Load();
        Assert.Equal(new AppSettings(true, false, true), stored); // OSD options keep their defaults
    }

    [Fact]
    public void AutostartFailure_RevertsTheSwitch_AndShowsError()
    {
        MainViewModel vm = Create();
        _autostart.Failure = new UnauthorizedAccessException("Zugriff verweigert");

        vm.StartWithWindows = true;

        Assert.False(vm.StartWithWindows);
        Assert.Contains("Autostart", vm.ErrorMessage);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void AutostartState_ComesFromWindows_NotFromTheSettingsFile()
    {
        new SettingsStore(SettingsPath).Save(new AppSettings(StartWithWindows: true));

        MainViewModel vm = Create(); // FakeAutostart says: not enabled

        Assert.False(vm.StartWithWindows);
    }
}
