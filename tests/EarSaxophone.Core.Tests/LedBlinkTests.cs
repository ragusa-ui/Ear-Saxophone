using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

/// <summary>Muted keys blink: 600 ms on, 600 ms off, all in step, only phase changes are sent (fake clock with manual timers).</summary>
public class LedBlinkTests
{
    private static readonly ControlId M1 = Cc(48);
    private static readonly ControlId M2 = Cc(49);

    private readonly FakeAudioSystem _audio = new();
    private readonly FakeEndpoint _speakers;
    private readonly FakeEndpoint _mic;
    private readonly AudioController _controller;
    private readonly FakeMidiOutputProvider _provider = new();
    private readonly ManualClock _clock = new();

    public LedBlinkTests()
    {
        _speakers = _audio.AddEndpoint("spk", "Lautsprecher", DeviceFlow.Render);
        _mic = _audio.AddEndpoint("mic", "Mikrofon", DeviceFlow.Capture);
        _controller = new AudioController(_audio);
    }

    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    private static (ControlKind, byte, byte, byte) Msg(ControlId control, int value) => (ControlKind.ControlChange, (byte)0, control.Number, (byte)value);

    private LedController Create(LedOptions? options = null, params ControlMapping[] mappings)
    {
        var leds = new LedController(new MidiOutputConnection(_provider, "nanokontrol"), _controller.ReadGroupMuted, options, _clock);
        leds.SetProfile(new Profile("p", mappings));
        return leds;
    }

    private LedController CreateM1M2() => Create(null,
        new ControlMapping(M1, new MuteAction(AudioTarget.Speakers)),
        new ControlMapping(M2, new MuteAction(AudioTarget.Microphone)));

    private List<(ControlKind, byte, byte, byte)> Sent => _provider.Current!.Sent;

    [Fact]
    public void BlinkIsOnByDefault_600On600Off_OnlyPhaseChangesAreSent()
    {
        _speakers.Muted = true;
        using LedController leds = CreateM1M2();
        Assert.True(leds.Blink);
        leds.Poll(true, 1);
        Assert.Equal(127, Sent.Last(m => m.Item3 == 48).Item4); // a newly muted key starts lit
        Sent.Clear();

        _clock.AdvanceMs(599);
        Assert.Empty(Sent);
        _clock.AdvanceMs(1);
        Assert.Equal([Msg(M1, 0)], Sent);
        _clock.AdvanceMs(600);
        _clock.AdvanceMs(600);
        _clock.AdvanceMs(600);
        Assert.Equal([Msg(M1, 0), Msg(M1, 127), Msg(M1, 0), Msg(M1, 127)], Sent);

        leds.Poll(true, 1); // the once-per-second poll sends nothing extra
        leds.Refresh();
        Assert.Equal(4, Sent.Count);
    }

    [Fact]
    public void AllBlinkingKeysAreInStep_AndANewlyMutedKeyRestartsTheRhythmLit()
    {
        _speakers.Muted = true;
        using LedController leds = CreateM1M2();
        leds.Poll(true, 1);
        Sent.Clear();

        _clock.AdvanceMs(300);
        _mic.Muted = true;
        leds.Refresh();
        Assert.Equal([Msg(M2, 127)], Sent); // M1 is lit already

        _clock.AdvanceMs(599); // the old rhythm would have switched M1 off at 600
        Assert.Single(Sent);
        _clock.AdvanceMs(1);
        Assert.Equal([Msg(M2, 127), Msg(M1, 0), Msg(M2, 0)], Sent);
        _clock.AdvanceMs(600);
        Assert.Equal([Msg(M1, 127), Msg(M2, 127)], Sent.Skip(3));
    }

    [Fact]
    public void BlinkOff_IsASteadyLight_SwitchingInTheDarkPhaseLightsAtOnce_AndStopsTheTimer()
    {
        _speakers.Muted = true;
        using LedController leds = CreateM1M2();
        leds.Poll(true, 1);
        _clock.AdvanceMs(600);
        Sent.Clear();

        leds.Blink = false;
        Assert.Equal([Msg(M1, 127)], Sent);
        Assert.Equal(0, _clock.ActiveTimers);
        _clock.AdvanceMs(5000);
        leds.Poll(true, 1);
        Assert.Single(Sent);

        leds.Blink = true; // starts with the on phase: nothing to send now
        Assert.Single(Sent);
        Assert.Equal(1, _clock.ActiveTimers);
        _clock.AdvanceMs(600);
        Assert.Equal(Msg(M1, 0), Sent[^1]);
    }

    [Fact]
    public void NothingMuted_NoTimerAndNoTraffic_UnmutingStopsTheBlinking()
    {
        using LedController leds = CreateM1M2();
        leds.Poll(true, 1);
        Sent.Clear();
        Assert.Equal(0, _clock.ActiveTimers);
        for (int i = 0; i < 5; i++)
        {
            _clock.AdvanceMs(1000);
            leds.Poll(true, 1);
        }

        Assert.Empty(Sent);

        _speakers.Muted = true;
        leds.Refresh();
        _clock.AdvanceMs(600); // dark phase
        _speakers.Muted = false;
        leds.Refresh();
        Assert.Equal([Msg(M1, 127), Msg(M1, 0)], Sent); // already dark: unmuting sends nothing more
        Assert.Equal(0, _clock.ActiveTimers);
    }

    [Fact]
    public void SwitchingTheFeedbackOff_LeavesEverythingDark_AndNothingIsSentAfterwards()
    {
        _speakers.Muted = true;
        _mic.Muted = true;
        using LedController leds = CreateM1M2();
        leds.Poll(true, 1);
        FakeMidiOutput output = _provider.Current!;
        output.Sent.Clear();

        leds.Enabled = false;
        Assert.Equal([Msg(M1, 0), Msg(M2, 0)], output.Sent);
        Assert.Equal(0, _clock.ActiveTimers);
        _clock.AdvanceMs(3000);
        Assert.Equal(2, output.Sent.Count);

        leds.Enabled = true;
        leds.Poll(true, 1);
        _clock.AdvanceMs(600);
        Assert.Equal(0, _provider.Current!.Sent.Last(m => m.Number == 48).Value);
    }

    [Fact]
    public void Reconnect_WhileBlinking_SendsEverythingAgain_StartingLit_WithOneTimerOnly()
    {
        _speakers.Muted = true;
        using LedController leds = CreateM1M2();
        leds.Poll(true, 1);
        _clock.AdvanceMs(600); // dark phase on the old port

        leds.Poll(true, 2);
        FakeMidiOutput second = _provider.Opened[1];
        Assert.Equal(30, second.Sent.Count);
        Assert.Equal(127, second.Sent.Last(m => m.Number == 48).Value);
        Assert.Equal(1, _clock.ActiveTimers);

        _clock.AdvanceMs(600);
        Assert.Equal(31, second.Sent.Count);
        Assert.Equal(Msg(M1, 0), second.Sent[^1]);
    }

    [Fact]
    public void Disconnect_AndDispose_StopTheTimer()
    {
        _speakers.Muted = true;
        LedController leds = CreateM1M2();
        leds.Poll(true, 1);
        Assert.Equal(1, _clock.ActiveTimers);

        leds.Poll(inputConnected: false, inputGeneration: 1);
        Assert.Equal(0, _clock.ActiveTimers);
        int sent = _provider.AllSent.Count;
        _clock.AdvanceMs(2000);
        Assert.Equal(sent, _provider.AllSent.Count);

        leds.Poll(true, 2);
        Assert.Equal(1, _clock.ActiveTimers);
        leds.Dispose();
        Assert.Equal(0, _clock.ActiveTimers);
        _clock.AdvanceMs(2000);
        Assert.Equal(0, _provider.Opened[^1].Sent.Last(m => m.Number == 48).Value); // Dispose switched the lit LED off
    }

    [Fact]
    public void ZeroHalfPeriod_MeansSteadyLight()
    {
        _speakers.Muted = true;
        using LedController leds = Create(new LedOptions { BlinkHalfPeriod = TimeSpan.Zero }, new ControlMapping(M1, new MuteAction(AudioTarget.Speakers)));
        leds.Poll(true, 1);
        Sent.Clear();

        _clock.AdvanceMs(5000);
        Assert.Empty(Sent);
        Assert.Equal(0, _clock.ActiveTimers);
    }

    [Fact]
    public void BlinkPhase_IsOnInTheFirstHalf_OffInTheSecond()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan half = TimeSpan.FromMilliseconds(600);
        Assert.True(BlinkPhase.IsOn(start, start, half));
        Assert.True(BlinkPhase.IsOn(start, start.AddMilliseconds(599), half));
        Assert.False(BlinkPhase.IsOn(start, start.AddMilliseconds(600), half));
        Assert.False(BlinkPhase.IsOn(start, start.AddMilliseconds(1199), half));
        Assert.True(BlinkPhase.IsOn(start, start.AddMilliseconds(1200), half));
        Assert.True(BlinkPhase.IsOn(start, start.AddMilliseconds(-5), half));
        Assert.True(BlinkPhase.IsOn(start, start.AddMilliseconds(700), TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromMilliseconds(600), BlinkPhase.UntilNextChange(start, start, half));
        Assert.Equal(TimeSpan.FromMilliseconds(1), BlinkPhase.UntilNextChange(start, start.AddMilliseconds(599), half));
        Assert.Equal(TimeSpan.FromMilliseconds(600), BlinkPhase.UntilNextChange(start, start.AddMilliseconds(600), half));
        Assert.Equal(TimeSpan.FromMilliseconds(605), BlinkPhase.UntilNextChange(start, start.AddMilliseconds(-5), half));
        Assert.Equal(TimeSpan.FromMilliseconds(600), BlinkPhase.DefaultHalfPeriod);
    }

    [Fact]
    public void Settings_LedBlinkIsOnByDefault_AndSaved()
    {
        string dir = Path.Combine(Path.GetTempPath(), "earsax-blink-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            Assert.True(store.Load().LedBlink);
            Assert.NotEqual(new AppSettings(), new AppSettings(LedBlink: false));
            store.Save(new AppSettings(LedBlink: false));
            Assert.False(store.Load().LedBlink);
            File.WriteAllText(store.Path, "{ \"ledFeedback\": true }"); // older settings file without the new switch
            Assert.True(store.Load().LedBlink);
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

/// <summary>The blink rhythm of the device LEDs and of the picture through the main view model.</summary>
public sealed class LedBlinkViewModelTests : IDisposable
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
    public void PictureAndDevice_BlinkTogether_MutedLitStaysTrue_AndTheSwitchMakesItSteady()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Speakers)));
        _h.Speakers.Muted = true;
        MainViewModel vm = Create();
        ControllerElementViewModel m1 = ViewModelHarness.Element(vm, "M 1");
        Assert.True(vm.LedBlink);
        Assert.True(m1.IsMuteLit && m1.IsLedShownOn && !m1.IsLedBlinkOff);
        Assert.Equal(127, LastValue(48));

        _h.Clock.AdvanceMs(600);
        Assert.True(m1.IsMuteLit);
        Assert.True(m1.IsLedBlinkOff);
        Assert.False(m1.IsLedShownOn);
        Assert.Equal(0, LastValue(48));
        Assert.All(vm.Elements.Where(e => !e.IsMuteLit), e => Assert.False(e.IsLedBlinkOff));

        _h.Clock.AdvanceMs(600);
        Assert.False(m1.IsLedBlinkOff);
        Assert.Equal(127, LastValue(48));

        _h.Clock.AdvanceMs(600);
        vm.LedBlink = false;
        Assert.False(m1.IsLedBlinkOff);
        Assert.Equal(127, LastValue(48));
        Assert.False(new SettingsStore(_h.SettingsPath).Load().LedBlink);
        int sent = _outputs.AllSent.Count;
        _h.Clock.AdvanceMs(3000);
        vm.Tick();
        Assert.False(m1.IsLedBlinkOff);
        Assert.Equal(sent, _outputs.AllSent.Count);
    }

    [Fact]
    public void Picture_BlinksWithoutADeviceToo_AndStopsWhenUnmuted()
    {
        _h.SaveProfile(new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Speakers)));
        MainViewModel vm = _h.Create();
        ControllerElementViewModel m1 = ViewModelHarness.Element(vm, "M 1");
        Assert.False(m1.IsMuteLit);
        Assert.Equal(0, _h.Clock.ActiveTimers);

        _h.Speakers.Muted = true;
        vm.RefreshMuteStates();
        _h.Clock.AdvanceMs(600);
        Assert.True(m1.IsLedBlinkOff);

        _h.Speakers.Muted = false;
        vm.RefreshMuteStates();
        Assert.False(m1.IsMuteLit);
        Assert.False(m1.IsLedBlinkOff);
        Assert.Equal(0, _h.Clock.ActiveTimers);
    }
}
