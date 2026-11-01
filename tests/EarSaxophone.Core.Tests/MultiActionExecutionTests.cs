using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

/// <summary>Several actions on one control, mute modes (push-to-talk) and group mute, from the router down to the sessions.</summary>
public class MultiActionExecutionTests
{
    private const byte FaderNumber = 3;
    private const byte KeyNumber = 48;

    private static readonly ControlId Fader = new(ControlKind.ControlChange, 0, FaderNumber);
    private static readonly ControlId Key = new(ControlKind.ControlChange, 0, KeyNumber);

    private readonly FakeAudioSystem _system = new();
    private readonly RecordingKeyboard _keyboard = new();
    private readonly FakeEndpoint _speakers;
    private readonly FakeEndpoint _mic;
    private readonly ActionExecutor _executor;
    private readonly AudioController _audio;

    public MultiActionExecutionTests()
    {
        _speakers = _system.AddEndpoint("spk", "Lautsprecher", DeviceFlow.Render);
        _mic = _system.AddEndpoint("mic", "Mikrofon", DeviceFlow.Capture);
        _audio = new AudioController(_system);
        _executor = new ActionExecutor(_audio, _keyboard);
    }

    private static MidiMessage Cc(byte number, byte value) => new(MidiMessageKind.ControlChange, 0, number, value);

    /// <summary>Routes like the program does: all executions of one message run together.</summary>
    private ControlRouter Route(params ControlMapping[] mappings)
    {
        var router = new ControlRouter(new Profile("p", mappings));
        router.ActionsRequested += invocations => _executor.ExecuteAll(invocations);
        return router;
    }

    private static AudioTarget App(string exe) => AudioTarget.Application(exe);

    [Fact]
    public void OneFader_ControlsAllGames_AndEverySessionOfThem()
    {
        FakeSession game1 = _system.AddSession(@"C:\Games\One\game1.exe", "spk");
        FakeSession game1Helper = _system.AddSession(@"C:\Games\One\game1.exe", "spk", pid: 4711);
        FakeSession game2 = _system.AddSession(@"D:\Steam\game2.exe", "spk");
        FakeSession game2OnHeadset = _system.AddSession(@"D:\Steam\game2.exe", "headset");
        FakeSession discord = _system.AddSession(@"C:\Discord\Discord.exe", "spk");
        ControlRouter router = Route(new ControlMapping(Fader, [new SetVolumeAction(App("game1.exe")), new SetVolumeAction(App("Game2"))], Name: "Spiele"));

        router.Handle(Cc(FaderNumber, 127));
        router.Handle(Cc(FaderNumber, 32));

        float expected = 32 / 127f;
        Assert.All(new[] { game1, game1Helper, game2, game2OnHeadset }, s => Assert.Equal(expected, s.Volume, 5));
        Assert.Equal(1f, discord.Volume);                       // not on the fader
        Assert.Equal(0, discord.VolumeWrites);
        Assert.Equal(0.5f, _speakers.Volume);                   // the device itself is untouched
    }

    [Fact]
    public void OneFader_SeveralGames_AGameStartedLaterGetsTheValueOnce()
    {
        _system.AddSession(@"C:\Games\game1.exe", "spk");
        ControlRouter router = Route(new ControlMapping(Fader, [new SetVolumeAction(App("game1.exe")), new SetVolumeAction(App("game2.exe"))]));

        router.Handle(Cc(FaderNumber, 64));
        FakeSession late = _system.AddSession(@"C:\Games\game2.exe", "spk");
        _system.RaiseChanged();
        _system.RaiseChanged();

        Assert.Equal(64 / 127f, late.Volume, 5);
        Assert.Equal(1, late.VolumeWrites);
    }

    [Fact]
    public void ExecuteAll_ReturnsOneResultPerExecution_InOrder()
    {
        _system.AddSession(@"C:\Games\game1.exe", "spk");
        var mapping = new ControlMapping(Fader, [new SetVolumeAction(App("game1.exe")), new SetVolumeAction(App("notrunning.exe")), new SetVolumeAction(AudioTarget.Microphone)]);

        IReadOnlyList<ActionResult> results = _executor.ExecuteMapping(mapping, 0.25f);

        Assert.Equal([ActionOutcome.Applied, ActionOutcome.Deferred, ActionOutcome.Applied], results.Select(r => r.Outcome));
        Assert.All(results, r => Assert.Equal(0.25f, r.Level!.Value, 5));
        Assert.Equal(0.25f, _mic.Volume, 5);
    }

    [Fact]
    public void PushToTalk_MicIsOnOnlyWhileTheKeyIsHeld()
    {
        _mic.Muted = true;
        ControlRouter router = Route(new ControlMapping(Key, new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld)));

        router.Handle(Cc(KeyNumber, 127));
        Assert.False(_mic.Muted);
        router.Handle(Cc(KeyNumber, 127));                      // repeat: still on
        Assert.False(_mic.Muted);
        router.Handle(Cc(KeyNumber, 0));
        Assert.True(_mic.Muted);
        Assert.False(_speakers.Muted);
    }

    [Fact]
    public void PushToTalk_SetsTheStateInsteadOfToggling_WhenItStartsUnmuted()
    {
        ControlRouter router = Route(new ControlMapping(Key, new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld)));

        router.Handle(Cc(KeyNumber, 127));
        Assert.False(_mic.Muted);
        router.Handle(Cc(KeyNumber, 0));
        Assert.True(_mic.Muted);
        router.Handle(Cc(KeyNumber, 127));
        Assert.False(_mic.Muted);
    }

    [Fact]
    public void MuteWhileHeld_SilencesOnlyWhileHeld_AlsoForApplications()
    {
        FakeSession discord = _system.AddSession(@"C:\Discord\Discord.exe", "spk");
        ControlRouter router = Route(new ControlMapping(Key,
            [new MuteAction(AudioTarget.Speakers, MuteMode.MuteWhileHeld), new MuteAction(App("Discord.exe"), MuteMode.MuteWhileHeld)]));

        router.Handle(Cc(KeyNumber, 100));
        Assert.True(_speakers.Muted);
        Assert.True(discord.Muted);

        router.Handle(Cc(KeyNumber, 10));
        Assert.False(_speakers.Muted);
        Assert.False(discord.Muted);
    }

    [Fact]
    public void WhileHeldResults_ReportTheNewState()
    {
        var ptt = new ControlMapping(Key, new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld));

        ActionResult down = Assert.Single(_executor.ExecuteMapping(ptt, keyPhase: ActionPhase.Pressed));
        ActionResult up = Assert.Single(_executor.ExecuteMapping(ptt, keyPhase: ActionPhase.Released));

        Assert.False(down.Muted);
        Assert.True(up.Muted);
        Assert.Equal(new OsdMessage(Texts.TargetMicrophoneShort, Texts.OsdUnmuted, null, false), OsdMessageFactory.Create(ptt.Actions[0], down));
        Assert.Equal(new OsdMessage(Texts.TargetMicrophoneShort, Texts.OsdMuted, null, true), OsdMessageFactory.Create(ptt.Actions[0], up));
    }

    [Fact]
    public void ToggleMute_IgnoresTheRelease()
    {
        ControlRouter router = Route(new ControlMapping(Key, new MuteAction(AudioTarget.Speakers)));

        router.Handle(Cc(KeyNumber, 127));
        router.Handle(Cc(KeyNumber, 0));
        Assert.True(_speakers.Muted);
        router.Handle(Cc(KeyNumber, 127));
        router.Handle(Cc(KeyNumber, 0));
        Assert.False(_speakers.Muted);
    }

    [Fact]
    public void GroupMute_AnyTargetAudible_MutesAll_ThenUnmutesAll()
    {
        FakeSession discord = _system.AddSession(@"C:\Discord\Discord.exe", "spk");
        FakeSession game = _system.AddSession(@"C:\Games\game.exe", "spk");
        _speakers.Muted = true;                                  // already muted; Discord and the game are audible
        ControlRouter router = Route(new ControlMapping(Key,
            [new MuteAction(AudioTarget.Speakers), new MuteAction(App("Discord.exe")), new MuteAction(App("game.exe"))]));

        router.Handle(Cc(KeyNumber, 127));
        router.Handle(Cc(KeyNumber, 0));
        Assert.True(_speakers.Muted);                            // not toggled back to audible
        Assert.True(discord.Muted);
        Assert.True(game.Muted);

        router.Handle(Cc(KeyNumber, 127));
        router.Handle(Cc(KeyNumber, 0));
        Assert.False(_speakers.Muted);
        Assert.False(discord.Muted);
        Assert.False(game.Muted);
    }

    [Fact]
    public void GroupMute_OnlyOneTargetAudible_StillMutesAll()
    {
        FakeSession discord = _system.AddSession(@"C:\Discord\Discord.exe", "spk");
        _speakers.Muted = true;
        _mic.Muted = true;
        var mapping = new ControlMapping(Key, [new MuteAction(AudioTarget.Speakers), new MuteAction(AudioTarget.Microphone), new MuteAction(App("Discord.exe"))]);

        IReadOnlyList<ActionResult> results = _executor.ExecuteMapping(mapping);

        Assert.True(_speakers.Muted && _mic.Muted && discord.Muted);
        Assert.All(results, r => Assert.True(r.Muted));
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void GroupMute_AppWithoutSessions_CountsWithItsRememberedState()
    {
        // Nothing is running: the program counts as audible, so the group mutes and remembers it for later.
        var mapping = new ControlMapping(Key, [new MuteAction(App("game.exe")), new MuteAction(AudioTarget.Speakers)]);
        _speakers.Muted = true;

        IReadOnlyList<ActionResult> first = _executor.ExecuteMapping(mapping);
        Assert.Equal(ActionOutcome.Deferred, first[0].Outcome);
        Assert.True(first[0].Muted);
        Assert.True(_speakers.Muted);

        FakeSession game = _system.AddSession(@"C:\Games\game.exe", "spk");
        _system.RaiseChanged();
        Assert.True(game.Muted);                                 // the remembered state reaches the new session

        _executor.ExecuteMapping(mapping);                       // everything muted now: the group unmutes
        Assert.False(game.Muted);
        Assert.False(_speakers.Muted);
    }

    [Fact]
    public void GroupMute_AMissingDeviceDoesNotBlockTheOthers()
    {
        _system.Defaults[DeviceFlow.Capture] = null;             // no microphone
        _speakers.Muted = false;
        var mapping = new ControlMapping(Key, [new MuteAction(AudioTarget.Microphone), new MuteAction(AudioTarget.Speakers)]);

        IReadOnlyList<ActionResult> results = _executor.ExecuteMapping(mapping);

        Assert.True(results[0].IsFailure);
        Assert.Equal(Texts.NoRecordingDevice, results[0].Message);
        Assert.Equal(ActionOutcome.Applied, results[1].Outcome);
        Assert.True(_speakers.Muted);
    }

    [Fact]
    public void GroupMute_AMissingDeviceDoesNotCountAsAudible()
    {
        _system.Defaults[DeviceFlow.Capture] = null;             // no microphone
        _speakers.Muted = true;
        var mapping = new ControlMapping(Key, [new MuteAction(AudioTarget.Microphone), new MuteAction(AudioTarget.Speakers)]);

        _executor.ExecuteMapping(mapping);

        Assert.False(_speakers.Muted);                           // the only readable target was muted: the group unmutes
    }

    [Fact]
    public void GroupMute_SeparateExecutions_WouldDriftApart_ButTheGroupDoesNot()
    {
        _speakers.Muted = true;
        var mapping = new ControlMapping(Key, [new MuteAction(AudioTarget.Speakers), new MuteAction(AudioTarget.Microphone)]);

        // One by one each target toggles on its own (this is why the executor groups them).
        foreach (ActionInvocation invocation in ActionInvocation.ForMapping(mapping, ActionPhase.Pressed, 1f))
        {
            _executor.Execute(invocation);
        }

        Assert.NotEqual(_speakers.Muted, _mic.Muted);

        _executor.ExecuteMapping(mapping);
        Assert.True(_speakers.Muted && _mic.Muted);
        _executor.ExecuteMapping(mapping);
        Assert.False(_speakers.Muted || _mic.Muted);
    }

    [Fact]
    public void MixedMapping_ToggleGroupWhileHeldAndMedia_EachDoTheirPart()
    {
        _mic.Muted = true;
        FakeSession discord = _system.AddSession(@"C:\Discord\Discord.exe", "spk");
        ControlRouter router = Route(new ControlMapping(Key,
        [
            new MediaAction(MediaCommand.PlayPause),
            new MuteAction(AudioTarget.Speakers),
            new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld),
            new MuteAction(App("Discord.exe")),
        ]));

        router.Handle(Cc(KeyNumber, 127));
        Assert.Equal([MediaCommand.PlayPause], _keyboard.Media);
        Assert.True(_speakers.Muted);
        Assert.True(discord.Muted);
        Assert.False(_mic.Muted);

        router.Handle(Cc(KeyNumber, 0));
        Assert.Equal([MediaCommand.PlayPause], _keyboard.Media);  // no second media key on release
        Assert.True(_speakers.Muted);                              // toggles stay
        Assert.True(discord.Muted);
        Assert.True(_mic.Muted);                                   // push-to-talk ends
    }

    [Fact]
    public void Execute_ActionForAnotherPhase_DoesNothing()
    {
        var media = new MediaAction(MediaCommand.Next);
        var mapping = new ControlMapping(Key, media);

        ActionResult released = _executor.Execute(new ActionInvocation(mapping, media, 0f, ActionPhase.Released));
        ActionResult toggleReleased = _executor.Execute(new ActionInvocation(mapping, new MuteAction(AudioTarget.Speakers), 0f, ActionPhase.Released));

        Assert.Empty(_keyboard.Media);
        Assert.False(_speakers.Muted);
        Assert.Equal(Texts.ActionNotTriggered, released.Message);
        Assert.False(released.IsFailure);
        Assert.Equal(Texts.ActionNotTriggered, toggleReleased.Message);
    }

    [Fact]
    public void ExecuteAll_GroupsOnlyTogglesOfTheSameMapping()
    {
        _speakers.Muted = true;
        var a = new ControlMapping(Key, new MuteAction(AudioTarget.Speakers));
        var b = new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 49), new MuteAction(AudioTarget.Microphone));

        _executor.ExecuteAll([.. ActionInvocation.ForMapping(a, ActionPhase.Pressed, 1f), .. ActionInvocation.ForMapping(b, ActionPhase.Pressed, 1f)]);

        Assert.False(_speakers.Muted);                             // toggled on its own
        Assert.True(_mic.Muted);
    }

    [Fact]
    public void ExecuteAll_AFailingKeyboardDoesNotStopTheOtherActions()
    {
        var executor = new ActionExecutor(_audio, new ThrowingKeyboard());
        var mapping = new ControlMapping(Key, [new HotkeyAction("Ctrl+M"), new MuteAction(AudioTarget.Speakers)]);

        IReadOnlyList<ActionResult> results = executor.ExecuteMapping(mapping);

        Assert.True(results[0].IsFailure);
        Assert.Equal(ActionOutcome.Applied, results[1].Outcome);
        Assert.True(_speakers.Muted);
    }

    [Fact]
    public void ExecuteAll_EmptyList_ReturnsNothing() => Assert.Empty(_executor.ExecuteAll([]));

    [Fact]
    public void OsdForSeveralExecutions_ShowsTheFirst()
    {
        _system.AddSession(@"C:\Games\game1.exe", "spk");
        var mapping = new ControlMapping(Fader, [new SetVolumeAction(App("game1.exe")), new SetVolumeAction(AudioTarget.Speakers)]);
        IReadOnlyList<ActionInvocation> invocations = ActionInvocation.ForMapping(mapping, null, 0.3f);

        OsdMessage? osd = OsdMessageFactory.Create(invocations, _executor.ExecuteAll(invocations));

        Assert.Equal("game1.exe", osd!.Title);
        Assert.Equal(0.3, osd.Level!.Value, 5);
        Assert.Null(OsdMessageFactory.Create([], []));
    }

    private sealed class ThrowingKeyboard : Input.IKeyboardSender
    {
        public void SendMediaCommand(MediaCommand command) => throw new InvalidOperationException("blockiert");

        public void SendHotkey(Input.Hotkey hotkey) => throw new InvalidOperationException("blockiert");
    }
}
