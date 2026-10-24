using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Input;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class ActionExecutorTests
{
    private sealed class FakeKeyboard : IKeyboardSender
    {
        public List<MediaCommand> Media { get; } = [];

        public List<Hotkey> Hotkeys { get; } = [];

        public Exception? Failure { get; set; }

        public void SendMediaCommand(MediaCommand command)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Media.Add(command);
        }

        public void SendHotkey(Hotkey hotkey)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Hotkeys.Add(hotkey);
        }
    }

    private static ActionInvocation Invocation(ActionDefinition action, float level = 1f) =>
        new(new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 1), action), level);

    private static (ActionExecutor Executor, FakeKeyboard Keyboard, FakeAudioSystem System) Create()
    {
        var system = new FakeAudioSystem();
        system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);
        var keyboard = new FakeKeyboard();
        return (new ActionExecutor(new AudioController(system), keyboard), keyboard, system);
    }

    [Theory]
    [InlineData(MediaCommand.PlayPause)]
    [InlineData(MediaCommand.Next)]
    [InlineData(MediaCommand.Previous)]
    [InlineData(MediaCommand.Stop)]
    public void MediaAction_SendsExactlyThatCommand_AndNoHotkey(MediaCommand command)
    {
        (ActionExecutor executor, FakeKeyboard keyboard, _) = Create();

        ActionResult result = executor.Execute(Invocation(new MediaAction(command)));

        Assert.Equal(ActionOutcome.Applied, result.Outcome);
        Assert.Equal([command], keyboard.Media);
        Assert.Empty(keyboard.Hotkeys);
    }

    [Fact]
    public void HotkeyAction_SendsParsedShortcut_AndNoMediaCommand()
    {
        (ActionExecutor executor, FakeKeyboard keyboard, _) = Create();

        ActionResult result = executor.Execute(Invocation(new HotkeyAction("strg+alt+m")));

        Assert.Equal(ActionOutcome.Applied, result.Outcome);
        Assert.Equal([new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, 0x4D)], keyboard.Hotkeys);
        Assert.Contains("Ctrl+Alt+M", result.Message);
        Assert.Empty(keyboard.Media);
    }

    [Fact]
    public void HotkeyAction_WithInvalidText_FailsWithoutSendingAnything()
    {
        (ActionExecutor executor, FakeKeyboard keyboard, _) = Create();

        ActionResult result = executor.Execute(Invocation(new HotkeyAction("Ctrl+Banana")));

        Assert.True(result.IsFailure);
        Assert.Contains("Banana", result.Message);
        Assert.Empty(keyboard.Hotkeys);
    }

    [Fact]
    public void KeyboardRefusal_BecomesFailureResult_NotAnException()
    {
        (ActionExecutor executor, FakeKeyboard keyboard, _) = Create();
        keyboard.Failure = new InvalidOperationException("Windows blocked the input");

        ActionResult media = executor.Execute(Invocation(new MediaAction(MediaCommand.Next)));
        ActionResult hotkey = executor.Execute(Invocation(new HotkeyAction("Ctrl+M")));

        Assert.True(media.IsFailure);
        Assert.True(hotkey.IsFailure);
        Assert.Contains("blocked", media.Message);
    }

    [Fact]
    public void AudioActions_AreDelegatedToTheAudioController()
    {
        (ActionExecutor executor, FakeKeyboard keyboard, FakeAudioSystem system) = Create();

        ActionResult volume = executor.Execute(Invocation(new SetVolumeAction(AudioTarget.Speakers), 0.4f));
        ActionResult mute = executor.Execute(Invocation(new ToggleMuteAction(AudioTarget.Speakers)));

        Assert.Equal(ActionOutcome.Applied, volume.Outcome);
        Assert.Equal(ActionOutcome.Applied, mute.Outcome);
        Assert.Equal(0.4f, system.Endpoints[0].Volume, 5);
        Assert.True(system.Endpoints[0].Muted);
        Assert.Empty(keyboard.Media);
        Assert.Empty(keyboard.Hotkeys);
    }

    [Fact]
    public void HotkeyAction_IsValid_OnlyForParsableShortcuts()
    {
        Assert.True(new HotkeyAction("Ctrl+Alt+M").IsValid);
        Assert.False(new HotkeyAction("Ctrl+").IsValid);
        Assert.False(new HotkeyAction("").IsValid);
    }

    [Fact]
    public void EndToEnd_ButtonPress_ThroughRouter_SendsMediaKeyOnce()
    {
        (ActionExecutor executor, FakeKeyboard keyboard, _) = Create();
        var button = new ControlId(ControlKind.ControlChange, 0, 41);
        var router = new ControlRouter(new Profile("p", [new ControlMapping(button, new MediaAction(MediaCommand.PlayPause))]));
        router.ActionRequested += i => executor.Execute(i);

        router.Handle(new MidiMessage(MidiMessageKind.ControlChange, 0, 41, 127));
        router.Handle(new MidiMessage(MidiMessageKind.ControlChange, 0, 41, 0));
        router.Handle(new MidiMessage(MidiMessageKind.ControlChange, 0, 41, 127));

        Assert.Equal([MediaCommand.PlayPause, MediaCommand.PlayPause], keyboard.Media);
    }
}
