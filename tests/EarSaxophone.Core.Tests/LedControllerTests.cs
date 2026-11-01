using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

internal sealed class FakeMidiOutput : IMidiOutput
{
    public List<(ControlKind Kind, byte Channel, byte Number, byte Value)> Sent { get; } = [];

    public bool Disposed { get; private set; }

    public Exception? Failure { get; set; }

    public void SendControlChange(byte channel, byte number, byte value) => Record(ControlKind.ControlChange, channel, number, value);

    public void SendNoteOn(byte channel, byte number, byte velocity) => Record(ControlKind.Note, channel, number, velocity);

    public void Dispose() => Disposed = true;

    private void Record(ControlKind kind, byte channel, byte number, byte value)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        ObjectDisposedException.ThrowIf(Disposed, this);
        Sent.Add((kind, channel, number, value));
    }
}

internal sealed class FakeMidiOutputProvider : IMidiOutputProvider
{
    public List<MidiDeviceInfo> Devices { get; } = [new MidiDeviceInfo(0, "Microsoft GS Wavetable Synth"), new MidiDeviceInfo(1, "nanoKONTROL2")];

    public List<FakeMidiOutput> Opened { get; } = [];

    public List<string> OpenedNames { get; } = [];

    public bool FailOpen { get; set; }

    public bool FailList { get; set; }

    public FakeMidiOutput? Current => Opened.LastOrDefault(o => !o.Disposed);

    /// <summary>Everything sent on all ports so far.</summary>
    public List<(ControlKind Kind, byte Channel, byte Number, byte Value)> AllSent => Opened.SelectMany(o => o.Sent).ToList();

    public IReadOnlyList<MidiDeviceInfo> GetDevices() => FailList ? throw new InvalidOperationException("driver error") : Devices.ToList();

    public IMidiOutput Open(MidiDeviceInfo device)
    {
        if (FailOpen)
        {
            throw new InvalidOperationException("port in use");
        }

        var output = new FakeMidiOutput();
        Opened.Add(output);
        OpenedNames.Add(device.Name);
        return output;
    }
}

public class LedControllerTests
{
    private static readonly ControlId S1 = Cc(32);
    private static readonly ControlId M1 = Cc(48);
    private static readonly ControlId M2 = Cc(49);
    private static readonly ControlId R1 = Cc(64);
    private static readonly ControlId Play = Cc(41);
    private static readonly ControlId Cycle = Cc(46);
    private static readonly ControlId TrackBack = Cc(58);

    private readonly FakeAudioSystem _audio = new();
    private readonly FakeEndpoint _speakers;
    private readonly FakeEndpoint _mic;
    private readonly AudioController _controller;
    private readonly FakeMidiOutputProvider _provider = new();
    private readonly ManualClock _clock = new();
    private readonly List<string> _status = [];

    public LedControllerTests()
    {
        _speakers = _audio.AddEndpoint("spk", "Lautsprecher", DeviceFlow.Render);
        _mic = _audio.AddEndpoint("mic", "Mikrofon", DeviceFlow.Capture);
        _controller = new AudioController(_audio);
    }

    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    private LedController Create(Profile profile, LedOptions? options = null)
    {
        var leds = new LedController(new MidiOutputConnection(_provider, "nanokontrol"), _controller.ReadGroupMuted, options, _clock);
        leds.StatusChanged += _status.Add;
        leds.SetProfile(profile);
        return leds;
    }

    private static Profile ProfileOf(params ControlMapping[] mappings) => new("p", mappings);

    private static byte ValueOf(FakeMidiOutput output, ControlId control) =>
        output.Sent.Last(m => m.Number == control.Number && m.Channel == control.Channel).Value;

    [Fact]
    public void LedKeys_AreTheSmrAndTransportKeysOfTheLayout()
    {
        Assert.Equal(30, Nanokontrol2Leds.Controls.Count);
        Assert.Contains(S1, Nanokontrol2Leds.Controls);
        Assert.Contains(Cc(71), Nanokontrol2Leds.Controls);
        Assert.Contains(Cycle, Nanokontrol2Leds.Controls);
        Assert.DoesNotContain(TrackBack, Nanokontrol2Leds.Controls);
        Assert.DoesNotContain(Cc(60), Nanokontrol2Leds.Controls); // Marker Set
        Assert.DoesNotContain(Cc(0), Nanokontrol2Leds.Controls); // fader
        Assert.All(Nanokontrol2Leds.Controls, c => Assert.Equal(ElementKind.Button, Nanokontrol2Layout.Find(c)!.Kind));
    }

    [Fact]
    public void Start_SendsEveryLedKeyOnce_LitOnlyWhereTheMuteTargetIsMuted()
    {
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(
            new ControlMapping(M1, new MuteAction(AudioTarget.Speakers)),
            new ControlMapping(R1, new MuteAction(AudioTarget.Microphone)),
            new ControlMapping(Play, new MediaAction(MediaCommand.PlayPause)),
            new ControlMapping(TrackBack, new MuteAction(AudioTarget.Speakers))));

        leds.Poll(inputConnected: true, inputGeneration: 1);

        FakeMidiOutput output = Assert.Single(_provider.Opened);
        Assert.Equal("nanoKONTROL2", Assert.Single(_provider.OpenedNames));
        Assert.Equal(30, output.Sent.Count);
        Assert.Equal(30, output.Sent.Select(m => m.Number).Distinct().Count());
        Assert.All(output.Sent, m => Assert.Equal(ControlKind.ControlChange, m.Kind));
        Assert.All(output.Sent, m => Assert.Equal(0, m.Channel));
        Assert.Equal(127, ValueOf(output, M1));
        Assert.Equal(0, ValueOf(output, R1)); // microphone audible
        Assert.Equal(0, ValueOf(output, Play)); // no mute action: never lit
        Assert.Single(output.Sent, m => m.Value == 127);
        Assert.DoesNotContain(output.Sent, m => m.Number == TrackBack.Number); // no LED on Track keys
    }

    [Fact]
    public void OnlyChangesAreSent_AndMuteChangesMadeElsewhereAreFollowed()
    {
        using LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));
        leds.Poll(true, 1);
        FakeMidiOutput output = _provider.Current!;
        output.Sent.Clear();

        leds.Poll(true, 1);
        leds.Refresh();
        Assert.Empty(output.Sent);

        _speakers.Muted = true; // muted in the Windows mixer
        leds.Poll(true, 1);
        Assert.Equal([(ControlKind.ControlChange, (byte)0, (byte)48, (byte)127)], output.Sent);

        output.Sent.Clear();
        _speakers.Muted = false;
        leds.Refresh();
        Assert.Equal([(ControlKind.ControlChange, (byte)0, (byte)48, (byte)0)], output.Sent);
        Assert.Single(_provider.Opened);
    }

    [Fact]
    public void GroupState_IsLitOnlyWhenEveryReadableTargetIsMuted()
    {
        var group = new ControlMapping(M2, [new MuteAction(AudioTarget.Speakers), new MuteAction(AudioTarget.Microphone)]);
        using LedController leds = Create(ProfileOf(group));
        leds.Poll(true, 1);
        FakeMidiOutput output = _provider.Current!;

        _speakers.Muted = true;
        leds.Refresh();
        Assert.Equal(0, ValueOf(output, M2)); // microphone still audible

        _mic.Muted = true;
        leds.Refresh();
        Assert.Equal(127, ValueOf(output, M2));

        _audio.Defaults[DeviceFlow.Capture] = null; // microphone unplugged: it no longer takes part
        _mic.Muted = false;
        leds.Refresh();
        Assert.Equal(127, ValueOf(output, M2));

        // The group toggle unmutes a group that the LED shows as muted.
        _controller.ToggleMute([AudioTarget.Speakers, AudioTarget.Microphone]);
        leds.Refresh();
        Assert.False(_speakers.Muted);
        Assert.Equal(0, ValueOf(output, M2));
    }

    [Fact]
    public void IsLit_ApplicationTargets_UseSessionsOrTheRememberedState()
    {
        FakeSession spotify = _audio.AddSession(@"C:\Apps\Spotify.exe", "spk");
        var mapping = new ControlMapping(S1, [new MuteAction(AudioTarget.Application("Spotify.exe")), new MuteAction(AudioTarget.Application("Game.exe"))]);
        Profile profile = ProfileOf(mapping);

        Assert.False(LedController.IsLit(profile, S1, _controller.ReadGroupMuted));
        spotify.Muted = true;
        Assert.False(LedController.IsLit(profile, S1, _controller.ReadGroupMuted)); // Game.exe (not running, never set) counts as audible

        _controller.SetMute(AudioTarget.Application("Game.exe"), true); // remembered for when the game starts
        Assert.True(LedController.IsLit(profile, S1, _controller.ReadGroupMuted));

        Assert.False(LedController.IsLit(profile, M1, _ => true)); // no mapping
        Assert.False(LedController.IsLit(ProfileOf(new ControlMapping(M1, new HotkeyAction("Ctrl+M"))), M1, _ => true));
        Assert.False(LedController.IsLit(profile, S1, _ => null)); // unknown state stays dark
    }

    [Fact]
    public void ReadGroupMuted_DoesNotRememberApplications()
    {
        Assert.False(_controller.ReadGroupMuted([AudioTarget.Application("Game.exe")]));

        // Nothing was remembered: a session that appears later is not touched by Reconcile.
        FakeSession game = _audio.AddSession(@"C:\Game.exe", "spk");
        game.Muted = true;
        _controller.Reconcile();
        Assert.True(game.Muted);
        Assert.Null(_controller.ReadGroupMuted([]));
    }

    [Fact]
    public void Reconnect_ReopensTheOutputAndSendsEverythingAgain()
    {
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));
        leds.Poll(true, 1);
        FakeMidiOutput first = _provider.Opened[0];

        leds.Poll(true, 2); // the input was reopened (sleep, reconnect button)

        Assert.True(first.Disposed);
        Assert.Equal(2, _provider.Opened.Count);
        Assert.Equal(30, _provider.Opened[1].Sent.Count);
        Assert.Equal(127, ValueOf(_provider.Opened[1], M1));
    }

    [Fact]
    public void Disconnect_SendsNothing_AndReplugSendsEverythingAgain()
    {
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));
        leds.Poll(true, 1);
        FakeMidiOutput first = _provider.Opened[0];
        int sentBefore = first.Sent.Count;

        leds.Poll(inputConnected: false, inputGeneration: 1);
        _speakers.Muted = false;
        leds.Poll(false, 1);

        Assert.True(first.Disposed);
        Assert.Equal(sentBefore, first.Sent.Count);
        Assert.Single(_provider.Opened);

        leds.Poll(true, 2);
        Assert.Equal(30, _provider.Opened[1].Sent.Count);
        Assert.Equal(0, ValueOf(_provider.Opened[1], M1));
    }

    [Fact]
    public void OutputVanishingFromTheList_IsReopenedAndResynced()
    {
        using LedController leds = Create(ProfileOf());
        leds.Poll(true, 1);

        _provider.Devices.RemoveAt(1);
        leds.Poll(true, 1);
        Assert.True(_provider.Opened[0].Disposed);

        _provider.Devices.Add(new MidiDeviceInfo(1, "nanoKONTROL2"));
        leds.Poll(true, 1);
        Assert.Equal(30, _provider.Opened[1].Sent.Count);
    }

    [Fact]
    public void ProfileChange_SendsEveryLedKeyAgainWithTheNewMappings()
    {
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));
        leds.Poll(true, 1);
        FakeMidiOutput output = _provider.Current!;
        output.Sent.Clear();

        leds.SetProfile(ProfileOf(new ControlMapping(Cycle, new MuteAction(AudioTarget.Speakers))));
        Assert.Empty(output.Sent); // the profile change itself does no I/O
        leds.Refresh();

        Assert.Equal(30, output.Sent.Count);
        Assert.Equal(0, ValueOf(output, M1));
        Assert.Equal(127, ValueOf(output, Cycle));
    }

    [Fact]
    public void Disabled_OpensNoPortAndSendsNothing()
    {
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));
        leds.Enabled = false;

        leds.Poll(true, 1);
        leds.Refresh();
        Assert.True(leds.IsOwnEcho(new MidiMessage(MidiMessageKind.ControlChange, 0, 48, 127)) is false);

        Assert.Empty(_provider.Opened);
        Assert.Empty(_status);
    }

    [Fact]
    public void SwitchingOff_TurnsOnlyTheLitLedsOff_ThenSendsNothing_AndSwitchingOnResyncs()
    {
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(
            new ControlMapping(M1, new MuteAction(AudioTarget.Speakers)),
            new ControlMapping(M2, new MuteAction(AudioTarget.Speakers))));
        leds.Poll(true, 1);
        FakeMidiOutput output = _provider.Current!;
        output.Sent.Clear();

        leds.Enabled = false;

        Assert.Equal(2, output.Sent.Count);
        Assert.All(output.Sent, m => Assert.Equal(0, m.Value));
        Assert.True(output.Disposed);

        _speakers.Muted = false;
        leds.Poll(true, 1);
        leds.Refresh();
        Assert.Single(_provider.Opened);

        leds.Enabled = true;
        leds.Poll(true, 1);
        Assert.Equal(30, _provider.Opened[1].Sent.Count);
    }

    [Fact]
    public void MissingOutput_IsNoError_AndReportedOnlyOnce()
    {
        _provider.Devices.RemoveAt(1);
        using LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));

        leds.Poll(true, 1);
        leds.Poll(true, 1);
        leds.Refresh();

        Assert.Empty(_provider.Opened);
        Assert.Single(_status);
        Assert.Contains("nanokontrol", _status[0]);
    }

    [Fact]
    public void NoInput_NoOutputIsOpened()
    {
        using LedController leds = Create(ProfileOf());
        leds.Poll(inputConnected: false, inputGeneration: 0);
        Assert.Empty(_provider.Opened);
        Assert.Empty(_status);
    }

    [Fact]
    public void OpenAndListFailures_AreNoErrors_AndReportedOncePerProblem()
    {
        using LedController leds = Create(ProfileOf());
        _provider.FailOpen = true;
        leds.Poll(true, 1);
        leds.Poll(true, 1);
        Assert.Single(_status);

        _provider.FailList = true;
        leds.Poll(true, 1);
        leds.Poll(true, 1);
        Assert.Equal(2, _status.Count);

        _provider.FailList = false;
        _provider.FailOpen = false;
        leds.Poll(true, 1);
        Assert.Equal(3, _status.Count); // "opened"
        Assert.Equal(30, _provider.Current!.Sent.Count);
    }

    [Fact]
    public void SendFailure_ClosesThePort_ReopensQuietlyAndResyncs()
    {
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));
        _provider.Devices.Clear();
        _provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
        leds.Poll(true, 1);
        FakeMidiOutput first = _provider.Opened[0];
        first.Failure = new InvalidOperationException("device gone");
        _speakers.Muted = false;

        leds.Refresh();

        Assert.True(first.Disposed);
        int statusCount = _status.Count;

        leds.Poll(true, 1);
        leds.Poll(true, 1);
        Assert.Equal(statusCount, _status.Count); // the reopen of the same port does not flood the status line
        Assert.Equal(30, _provider.Opened[1].Sent.Count);
        Assert.Equal(0, ValueOf(_provider.Opened[1], M1));
    }

    [Fact]
    public void OwnOutput_IsRecognisedAsEchoOnceAndOnlyWithinTheWindow()
    {
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));
        leds.Poll(true, 1);
        var echo = new MidiMessage(MidiMessageKind.ControlChange, 0, 48, 127);

        Assert.False(leds.IsOwnEcho(echo with { Value = 100 }));
        Assert.False(leds.IsOwnEcho(echo with { Number = 49 }));
        Assert.True(leds.IsOwnEcho(echo));
        Assert.False(leds.IsOwnEcho(echo)); // a second identical message is a real press

        _clock.AdvanceMs(29);
        Assert.True(leds.IsOwnEcho(echo with { Number = 49, Value = 0 }));
        _clock.AdvanceMs(2);
        Assert.False(leds.IsOwnEcho(echo with { Number = 50, Value = 0 })); // older than the window
    }

    [Fact]
    public void EchoFilter_CanBeSwitchedOff_AndValuesAreConfigurable()
    {
        _speakers.Muted = true;
        using LedController leds = Create(
            ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))),
            new LedOptions { OnValue = 100, OffValue = 1, EchoWindow = TimeSpan.Zero, Controls = [M1, M2] });
        leds.Poll(true, 1);

        FakeMidiOutput output = _provider.Current!;
        Assert.Equal(2, output.Sent.Count);
        Assert.Equal(100, ValueOf(output, M1));
        Assert.Equal(1, ValueOf(output, M2));
        Assert.False(leds.IsOwnEcho(new MidiMessage(MidiMessageKind.ControlChange, 0, 48, 100)));
    }

    [Fact]
    public void NoteControls_AreSentAsNoteOn()
    {
        var note = new ControlId(ControlKind.Note, 2, 60);
        _speakers.Muted = true;
        using LedController leds = Create(ProfileOf(new ControlMapping(note, new MuteAction(AudioTarget.Speakers))), new LedOptions { Controls = [note] });
        leds.Poll(true, 1);

        Assert.Equal([(ControlKind.Note, (byte)2, (byte)60, (byte)127)], _provider.Current!.Sent);
        Assert.True(leds.IsOwnEcho(new MidiMessage(MidiMessageKind.NoteOn, 2, 60, 127)));
    }

    [Fact]
    public void Dispose_TurnsLitLedsOffAndClosesThePort_LaterCallsDoNothing()
    {
        _speakers.Muted = true;
        LedController leds = Create(ProfileOf(new ControlMapping(M1, new MuteAction(AudioTarget.Speakers))));
        leds.Poll(true, 1);
        FakeMidiOutput output = _provider.Current!;
        output.Sent.Clear();

        leds.Dispose();
        leds.Poll(true, 2);
        leds.Refresh();
        leds.Enabled = false;
        leds.Dispose();

        Assert.Equal([(ControlKind.ControlChange, (byte)0, (byte)48, (byte)0)], output.Sent);
        Assert.True(output.Disposed);
        Assert.Single(_provider.Opened);
    }

    [Fact]
    public void ShortMessages_RoundTripThroughTheParser()
    {
        Assert.True(MidiMessageParser.TryParse(MidiShortMessage.ControlChange(3, 48, 127), out MidiMessage cc));
        Assert.Equal(new MidiMessage(MidiMessageKind.ControlChange, 3, 48, 127), cc);
        Assert.Equal(0x7F30B0, MidiShortMessage.ControlChange(0, 48, 127));
        Assert.True(MidiMessageParser.TryParse(MidiShortMessage.NoteOn(15, 60, 1), out MidiMessage note));
        Assert.Equal(new MidiMessage(MidiMessageKind.NoteOn, 15, 60, 1), note);
        Assert.Throws<ArgumentOutOfRangeException>(() => MidiShortMessage.ControlChange(16, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MidiShortMessage.ControlChange(0, 128, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MidiShortMessage.NoteOn(0, 0, 128));
    }

    [Fact]
    public void Settings_LedFeedbackIsOnByDefault_AndSaved()
    {
        string dir = Path.Combine(Path.GetTempPath(), "earsax-led-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            Assert.True(store.Load().LedFeedback);
            Assert.NotEqual(new AppSettings(), new AppSettings(LedFeedback: false));
            store.Save(new AppSettings(LedFeedback: false));
            Assert.False(store.Load().LedFeedback);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}

/// <summary>The LED feedback wired into the main view model (fake device with an output port).</summary>
public sealed class LedFeedbackViewModelTests : IDisposable
{
    private readonly ViewModelHarness _h = new();
    private readonly FakeMidiOutputProvider _outputs = new();
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (IDisposable d in _disposables)
        {
            d.Dispose();
        }

        _h.Dispose();
    }

    private MainViewModel Create()
    {
        var midi = new MidiConnectionManager(_h.Provider);
        var vm = new MainViewModel(new ProfileStore(_h.ProfilePath), midi, new ActionExecutor(new AudioController(_h.Audio), _h.Keyboard),
            _h.Audio, new ImmediateUi(), new SettingsStore(_h.SettingsPath), new FakeAutostart(), _h.Clock, midiOutput: _outputs);
        _disposables.Add(vm);
        _disposables.Add(midi);
        vm.Tick();
        return vm;
    }

    private byte LastValue(int cc) => _outputs.AllSent.Last(m => m.Number == cc).Value;

    [Fact]
    public void PressingAMuteKey_LightsItAtOnce_AndTheEchoIsNoKeyPress()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Speakers)));
        MainViewModel vm = Create();
        Assert.Equal(30, _outputs.AllSent.Count);
        Assert.Equal(0, LastValue(48));
        Assert.Contains("nanoKONTROL2", vm.StatusMessage);

        _h.Clock.Advance(TimeSpan.FromSeconds(1));
        _h.Send(vm, 48, 127);
        Assert.True(_h.Speakers.Muted);
        Assert.Equal(127, LastValue(48)); // without waiting for the next tick

        _h.Send(vm, 48, 0);
        _h.Send(vm, 48, 127); // looped back LED message: must not toggle again
        Assert.True(_h.Speakers.Muted);

        _h.Clock.Advance(TimeSpan.FromSeconds(1));
        _h.Send(vm, 48, 0);
        _h.Send(vm, 48, 127); // a real second press
        Assert.False(_h.Speakers.Muted);
        Assert.Equal(0, LastValue(48));
    }

    [Fact]
    public void Setting_SwitchesTheFeedbackOffAndOn_AndIsSaved()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Speakers)));
        _h.Speakers.Muted = true;
        MainViewModel vm = Create();
        Assert.True(vm.LedFeedback);
        Assert.Equal(127, LastValue(48));

        vm.LedFeedback = false;
        Assert.Equal(0, LastValue(48));
        Assert.False(new SettingsStore(_h.SettingsPath).Load().LedFeedback);
        int count = _outputs.AllSent.Count;
        vm.Tick();
        Assert.Equal(count, _outputs.AllSent.Count);

        vm.LedFeedback = true;
        vm.Tick();
        Assert.Equal(127, LastValue(48));
    }

    [Fact]
    public void ProfileEdits_UpdateTheLeds()
    {
        _h.Speakers.Muted = true;
        MainViewModel vm = Create();
        Assert.Equal(0, LastValue(49));

        ViewModelHarness.Assign(vm, "M 2", e => { e.Kind = ActionKind.Mute; e.AddTarget(AudioTarget.Speakers); });
        Assert.NotNull(vm.Current.Find(ViewModelHarness.Cc(49)));
        vm.Tick();
        Assert.Equal(127, LastValue(49));

        vm.RemoveSelectedMapping();
        vm.Tick();
        Assert.Equal(0, LastValue(49));
    }
}
