using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

/// <summary>Noticing a new program with sound (design V2, section 6), with the fake audio system and a manual clock.</summary>
public class ProgramPromptServiceTests
{
    private static readonly ControlId Fader1 = ViewModelHarness.Cc(0);
    private static readonly Profile Empty = new("p", []);

    private readonly FakeAudioSystem _audio = new();
    private readonly ManualClock _clock = new();
    private readonly ProgramPromptService _service;

    public ProgramPromptServiceTests()
    {
        _audio.AddEndpoint("spk", "Lautsprecher", DeviceFlow.Render);
        _audio.AddEndpoint("mic", "Mikrofon", DeviceFlow.Capture);
        _service = new ProgramPromptService(_audio, _clock, "EarSaxophone.exe", ownProcessId: 4242);
    }

    private FakeSession Playing(string path, DeviceFlow flow = DeviceFlow.Render, int? pid = null, bool systemSounds = false)
    {
        FakeSession session = _audio.AddSession(path, flow == DeviceFlow.Render ? "spk" : "mic", flow, pid, systemSounds);
        session.Activity = AudioSessionActivity.Active;
        return session;
    }

    /// <summary>Checks once now and once after the debounce time.</summary>
    private ProgramPrompt? CheckAfterDebounce(Profile? profile = null, params string[] ignored)
    {
        Assert.Null(_service.Check(profile ?? Empty, ignored));
        _clock.Advance(ProgramPromptService.MinimumActiveTime);
        return _service.Check(profile ?? Empty, ignored);
    }

    [Fact]
    public void AnUnmappedProgramThatKeepsPlaying_IsOffered_WithNameAndPath()
    {
        Playing(@"C:\Games\game.exe");

        ProgramPrompt? prompt = CheckAfterDebounce();

        Assert.Equal(new ProgramPrompt("game.exe", @"C:\Games\game.exe"), prompt);
        Assert.Equal(prompt, _service.OpenPrompt);
    }

    [Fact]
    public void AShortSound_IsNotOffered_TheTimeStartsAgainAfterAPause()
    {
        FakeSession game = Playing(@"C:\Games\game.exe");
        Assert.Null(_service.Check(Empty, []));
        _clock.AdvanceMs(1500);
        game.Activity = AudioSessionActivity.Inactive;
        Assert.Null(_service.Check(Empty, []));
        game.Activity = AudioSessionActivity.Active;
        Assert.Null(_service.Check(Empty, []));
        _clock.AdvanceMs(1500);

        Assert.Null(_service.Check(Empty, []));     // only 1.5 s without interruption
        _clock.AdvanceMs(500);
        Assert.NotNull(_service.Check(Empty, []));
    }

    [Theory]
    [InlineData(AudioSessionActivity.Unknown)]
    [InlineData(AudioSessionActivity.Inactive)]
    [InlineData(AudioSessionActivity.Expired)]
    public void ASessionThatOnlyExists_IsNotOffered(AudioSessionActivity activity)
    {
        Playing(@"C:\Games\game.exe").Activity = activity;

        Assert.Null(CheckAfterDebounce());
    }

    [Fact]
    public void RecordingSystemSoundsAndThisProgram_AreNeverOffered()
    {
        Playing(@"C:\Discord\Discord.exe", DeviceFlow.Capture);       // microphone use only
        Playing(@"C:\Windows\System32\AudioSrv.dll", systemSounds: true);
        Playing(@"C:\Tools\EarSaxophone.exe");
        Playing(@"C:\Tools\renamed.exe", pid: 4242);                   // this process under another name
        _audio.AddSession(null, "spk").Activity = AudioSessionActivity.Active;

        Assert.Null(CheckAfterDebounce());
    }

    [Theory]
    [InlineData("game.exe")]
    [InlineData("GAME")]
    public void AProgramOnAnyControl_IsNotOffered_ByVolumeOrMute(string mappedName)
    {
        Playing(@"C:\Games\game.exe");
        var byVolume = new Profile("p", [new ControlMapping(Fader1, [new SetVolumeAction(AudioTarget.Speakers), new SetVolumeAction(AudioTarget.Application(mappedName))])]);
        var byMute = new Profile("p", [new ControlMapping(ViewModelHarness.Cc(48), new MuteAction(AudioTarget.Application(mappedName)))]);

        Assert.Null(CheckAfterDebounce(byVolume));
        Assert.Null(_service.Check(byMute, []));
        Assert.True(ProgramPromptService.IsMapped(byMute, @"C:\Games\game.exe"));
        Assert.False(ProgramPromptService.IsMapped(byMute, "other.exe"));
    }

    [Fact]
    public void AnIgnoredProgram_IsNotOffered()
    {
        Playing(@"C:\Games\game.exe");

        Assert.Null(CheckAfterDebounce(null, "Game.EXE"));
    }

    [Fact]
    public void EveryProgramIsOfferedOncePerRun_EvenWhenTheQuestionWasDismissed()
    {
        FakeSession game = Playing(@"C:\Games\game.exe");
        Assert.NotNull(CheckAfterDebounce());
        _service.Close();

        game.Activity = AudioSessionActivity.Inactive;
        _service.Check(Empty, []);
        game.Activity = AudioSessionActivity.Active;
        _clock.AdvanceMs(60_000);
        _service.Check(Empty, []);
        _clock.AdvanceMs(60_000);

        Assert.Null(_service.Check(Empty, []));
    }

    [Fact]
    public void OneQuestionAtATime_AndAPauseBeforeTheNext()
    {
        Playing(@"C:\Games\a.exe");
        _clock.AdvanceMs(10);
        Playing(@"C:\Games\b.exe");

        Assert.Null(_service.Check(Empty, []));
        _clock.AdvanceMs(2000);
        Assert.Equal("a.exe", _service.Check(Empty, [])?.ExeName);  // the one that started first
        _clock.AdvanceMs(10_000);
        Assert.Null(_service.Check(Empty, []));                   // still open

        _service.Close();
        _clock.AdvanceMs(4999);
        Assert.Null(_service.Check(Empty, []));
        _clock.AdvanceMs(1);
        Assert.Equal("b.exe", _service.Check(Empty, [])?.ExeName);
    }

    [Fact]
    public void AFailingAudioService_GivesNoQuestion()
    {
        Playing(@"C:\Games\game.exe");
        _audio.ThrowOnListing = true;

        Assert.Null(CheckAfterDebounce());
    }

    [Fact]
    public void AddProgram_CreatesAMappingOrAddsATarget_WithoutTouchingTheOthers()
    {
        var named = new ControlMapping(Fader1, new SetVolumeAction(AudioTarget.Application("game1.exe")), new ValueScaling(0f, 0.5f, false), "Spiele");
        var other = new ControlMapping(ViewModelHarness.Cc(1), new SetVolumeAction(AudioTarget.Speakers));
        var profile = new Profile("p", [named, other]);

        Profile added = ProgramPromptService.AddProgram(profile, Fader1, @"C:\Games\game2.exe");

        Assert.Equal(named with { Actions = [.. named.Actions, new SetVolumeAction(AudioTarget.Application("game2.exe"))] }, added.Find(Fader1));
        Assert.Equal(other, added.Find(ViewModelHarness.Cc(1)));
        Assert.Equal(new ControlMapping(ViewModelHarness.Cc(2), new SetVolumeAction(AudioTarget.Application("x.exe"))),
            ProgramPromptService.AddProgram(profile, ViewModelHarness.Cc(2), "x.exe").Find(ViewModelHarness.Cc(2)));
        Assert.Same(profile, ProgramPromptService.AddProgram(profile, Fader1, "GAME1"));
    }
}
