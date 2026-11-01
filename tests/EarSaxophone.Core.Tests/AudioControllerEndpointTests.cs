using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Tests;

public class AudioControllerEndpointTests
{
    private static (FakeAudioSystem System, AudioController Controller) Create()
    {
        var system = new FakeAudioSystem();
        return (system, new AudioController(system));
    }

    [Fact]
    public void Speakers_VolumeAndMute_AffectOnlyThePlaybackDevice()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeEndpoint speakers = system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);
        FakeEndpoint mic = system.AddEndpoint("mic", "Microphone", DeviceFlow.Capture);

        Assert.Equal(ActionOutcome.Applied, controller.SetVolume(AudioTarget.Speakers, 0.8f).Outcome);
        Assert.Equal(ActionOutcome.Applied, controller.ToggleMute(AudioTarget.Speakers).Outcome);

        Assert.Equal(0.8f, speakers.Volume, 5);
        Assert.True(speakers.Muted);
        Assert.Equal(0.5f, mic.Volume, 5);
        Assert.False(mic.Muted);
    }

    [Fact]
    public void Microphone_UsesCaptureDevice_NotSpeakers()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeEndpoint speakers = system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);
        FakeEndpoint mic = system.AddEndpoint("mic", "USB Mic", DeviceFlow.Capture);

        controller.SetVolume(AudioTarget.Microphone, 0.25f);
        controller.ToggleMute(AudioTarget.Microphone);
        controller.ToggleMute(AudioTarget.Microphone);
        controller.ToggleMute(AudioTarget.Microphone);

        Assert.Equal(0.25f, mic.Volume, 5);
        Assert.True(mic.Muted);
        Assert.Equal(0.5f, speakers.Volume, 5);
        Assert.False(speakers.Muted);
    }

    [Fact]
    public void Microphone_NoRecordingDevice_ReportsClearFailure()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);

        ActionResult result = controller.ToggleMute(AudioTarget.Microphone);

        Assert.True(result.IsFailure);
        Assert.Contains("Aufnahmegerät", result.Message);
    }

    [Fact]
    public void Microphone_WithoutWindowsVolumeControl_ReportsDeviceNameAndReason()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeEndpoint mic = system.AddEndpoint("mic", "Headset Mic", DeviceFlow.Capture);
        mic.Failure = new AudioUnsupportedException("no hardware volume");

        ActionResult result = controller.SetVolume(AudioTarget.Microphone, 0.5f);

        Assert.True(result.IsFailure);
        Assert.Contains("Headset Mic", result.Message);
        Assert.Contains("erlaubt keine", result.Message);
    }

    [Fact]
    public void DefaultDeviceChange_RedirectsNextAction()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeEndpoint speakers = system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);
        FakeEndpoint headset = system.AddEndpoint("hs", "Headset", DeviceFlow.Render, makeDefault: false);

        controller.SetVolume(AudioTarget.Speakers, 0.1f);
        system.Defaults[DeviceFlow.Render] = "hs";
        system.RaiseChanged();
        controller.SetVolume(AudioTarget.Speakers, 0.9f);

        Assert.Equal(0.1f, speakers.Volume, 5);
        Assert.Equal(0.9f, headset.Volume, 5);
    }

    [Fact]
    public void SpecificDevice_IsUsedById_AndMissingDeviceFails()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);
        FakeEndpoint other = system.AddEndpoint("hs", "Headset", DeviceFlow.Render, makeDefault: false);

        Assert.Equal(ActionOutcome.Applied,
            controller.SetVolume(new AudioTarget(AudioTargetKind.SpecificDevice, DeviceId: "hs"), 0.3f).Outcome);
        Assert.Equal(0.3f, other.Volume, 5);

        Assert.True(controller.SetVolume(new AudioTarget(AudioTargetKind.SpecificDevice, DeviceId: "gone"), 0.3f).IsFailure);
        Assert.True(controller.SetVolume(new AudioTarget(AudioTargetKind.SpecificDevice), 0.3f).IsFailure);
    }

    [Fact]
    public void Level_IsClamped()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeEndpoint speakers = system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);

        controller.SetVolume(AudioTarget.Speakers, 1.7f);

        Assert.Equal(1f, speakers.Volume, 5);
    }

    [Fact]
    public void Execute_RunsAudioActions_AndIgnoresOthers()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeEndpoint speakers = system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);
        var fader = new ControlMapping(new Core.Midi.ControlId(Core.Midi.ControlKind.ControlChange, 0, 0), new SetVolumeAction(AudioTarget.Speakers));
        var media = new ControlMapping(new Core.Midi.ControlId(Core.Midi.ControlKind.ControlChange, 0, 1), new MediaAction(MediaCommand.Next));

        Assert.NotNull(controller.Execute(new ActionInvocation(fader, fader.Actions[0], 0.2f, ActionPhase.Changed)));
        Assert.Equal(0.2f, speakers.Volume, 5);
        Assert.Null(controller.Execute(new ActionInvocation(media, media.Actions[0], 1f, ActionPhase.Pressed)));
    }
}
