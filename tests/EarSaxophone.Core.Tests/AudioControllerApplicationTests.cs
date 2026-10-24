using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Tests;

/// <summary>
/// Tests for the application-identity hypothesis (the "Discord keeps losing its assignment" problem):
/// the target is the executable, sessions are resolved fresh on every action, and new sessions get the last value once.
/// </summary>
public class AudioControllerApplicationTests
{
    private const string DiscordV1 = @"C:\Users\x\AppData\Local\Discord\app-1.0.9100\Discord.exe";
    private const string DiscordV2 = @"C:\Users\x\AppData\Local\Discord\app-1.0.9101\Discord.exe";
    private static readonly AudioTarget Discord = AudioTarget.Application("Discord.exe");

    private static (FakeAudioSystem System, AudioController Controller) Create()
    {
        var system = new FakeAudioSystem();
        system.AddEndpoint("spk", "Speakers", DeviceFlow.Render);
        system.AddEndpoint("hs", "Headset", DeviceFlow.Render, makeDefault: false);
        system.AddEndpoint("mic", "Mic", DeviceFlow.Capture);
        return (system, new AudioController(system));
    }

    [Fact]
    public void MultipleSessionsOfOneApp_AcrossProcessesAndDevices_AreAllChanged_OthersUntouched()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession a = system.AddSession(DiscordV1, "spk", pid: 100);
        FakeSession b = system.AddSession(DiscordV1, "spk", pid: 101); // second Discord process
        FakeSession c = system.AddSession(DiscordV1, "hs", pid: 100);  // same app on another device
        FakeSession other = system.AddSession(@"C:\Apps\Spotify.exe", "spk");
        FakeSession mic = system.AddSession(DiscordV1, "mic", DeviceFlow.Capture, pid: 100);

        ActionResult result = controller.SetVolume(Discord, 0.3f);

        Assert.Equal(ActionOutcome.Applied, result.Outcome);
        Assert.All(new[] { a, b, c }, s => Assert.Equal(0.3f, s.Volume, 5));
        Assert.Equal(1f, other.Volume, 5);
        Assert.Equal(1f, mic.Volume, 5); // capture session belongs to the capture target, not the playback one
    }

    [Fact]
    public void RestartingTheApp_NewProcessAndSession_GetsTheLastValue_WithoutReassigning()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession first = system.AddSession(DiscordV1, "spk", pid: 100);
        controller.SetVolume(Discord, 0.4f);

        // Discord quits: session disappears, nothing to control for a while.
        system.Sessions.Remove(first);
        system.RaiseChanged();
        Assert.Equal(ActionOutcome.Deferred, controller.SetVolume(Discord, 0.4f).Outcome);

        // Discord starts again with a new process id and a new session.
        FakeSession second = system.AddSession(DiscordV1, "spk", pid: 777);
        system.RaiseChanged();

        Assert.Equal(0.4f, second.Volume, 5);
        // And the fader still works on the new session without any re-learning.
        controller.SetVolume(Discord, 0.9f);
        Assert.Equal(0.9f, second.Volume, 5);
    }

    [Fact]
    public void AppUpdate_ChangesInstallFolder_StillMatchesByExecutableName()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession old = system.AddSession(DiscordV1, "spk");
        controller.SetVolume(Discord, 0.2f);
        system.Sessions.Remove(old);

        FakeSession updated = system.AddSession(DiscordV2, "spk");
        system.RaiseChanged();

        Assert.Equal(0.2f, updated.Volume, 5);
    }

    [Fact]
    public void SessionAppearingOnAnotherDevice_AfterOutputSwitch_GetsTheValue()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession onSpeakers = system.AddSession(DiscordV1, "spk", pid: 100);
        controller.SetVolume(Discord, 0.6f);

        // User moves Discord to the headset: Windows creates a session on the other endpoint.
        system.Sessions.Remove(onSpeakers);
        FakeSession onHeadset = system.AddSession(DiscordV1, "hs", pid: 100);
        system.RaiseChanged();

        Assert.Equal(0.6f, onHeadset.Volume, 5);
    }

    [Fact]
    public void ValueIsGivenToANewSessionOnlyOnce_SoManualChangesInWindowsSurvive()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        controller.SetVolume(Discord, 0.5f); // nothing running yet
        FakeSession session = system.AddSession(DiscordV1, "spk");

        system.RaiseChanged();
        Assert.Equal(0.5f, session.Volume, 5);

        session.Volume = 0.8f; // user changes it in the Windows mixer
        int writes = session.VolumeWrites;
        system.RaiseChanged();
        system.RaiseChanged();

        Assert.Equal(0.8f, session.Volume, 5);
        Assert.Equal(writes, session.VolumeWrites);
    }

    [Fact]
    public void SessionClosingBetweenListingAndWriting_DoesNotBreakTheOthers()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession dying = system.AddSession(DiscordV1, "spk", pid: 100);
        FakeSession alive = system.AddSession(DiscordV1, "spk", pid: 101);
        dying.Gone = true;

        ActionResult result = controller.SetVolume(Discord, 0.7f);

        Assert.Equal(ActionOutcome.Applied, result.Outcome);
        Assert.Equal(0.7f, alive.Volume, 5);
    }

    [Fact]
    public void AllSessionsGone_ReportsFailure_NotSuccess()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        system.AddSession(DiscordV1, "spk").Gone = true;

        Assert.True(controller.SetVolume(Discord, 0.7f).IsFailure);
    }

    [Fact]
    public void AppNotRunning_IsDeferredNotAnError()
    {
        (_, AudioController controller) = Create();

        ActionResult result = controller.SetVolume(Discord, 0.7f);

        Assert.Equal(ActionOutcome.Deferred, result.Outcome);
        Assert.Contains("Discord.exe", result.Message);
    }

    [Fact]
    public void SystemSoundsAndSimilarApps_AreNeverChanged()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession sounds = system.AddSession(null, "spk", pid: 0, systemSounds: true);
        FakeSession ptb = system.AddSession(@"C:\Apps\DiscordPTB.exe", "spk");
        FakeSession real = system.AddSession(DiscordV1, "spk");

        controller.SetVolume(Discord, 0.1f);

        Assert.Equal(1f, sounds.Volume, 5);
        Assert.Equal(1f, ptb.Volume, 5);
        Assert.Equal(0.1f, real.Volume, 5);
    }

    [Fact]
    public void ToggleMute_MutesAllSessions_ThenUnmutesAll()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession a = system.AddSession(DiscordV1, "spk", pid: 100);
        FakeSession b = system.AddSession(DiscordV1, "hs", pid: 101);

        controller.ToggleMute(Discord);
        Assert.True(a.Muted && b.Muted);

        controller.ToggleMute(Discord);
        Assert.False(a.Muted || b.Muted);
    }

    [Fact]
    public void ToggleMute_WithMixedState_MutesEveryone()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession a = system.AddSession(DiscordV1, "spk", pid: 100);
        FakeSession b = system.AddSession(DiscordV1, "spk", pid: 101);
        a.Muted = true;

        controller.ToggleMute(Discord);

        Assert.True(a.Muted && b.Muted);
    }

    [Fact]
    public void MuteWhileAppIsClosed_IsAppliedWhenItStarts()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        Assert.Equal(ActionOutcome.Deferred, controller.ToggleMute(Discord).Outcome);

        FakeSession session = system.AddSession(DiscordV1, "spk");
        system.RaiseChanged();

        Assert.True(session.Muted);
    }

    [Fact]
    public void AudioServiceFailure_DuringListing_DoesNotThrow()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        system.ThrowOnListing = true;

        ActionResult result = controller.SetVolume(Discord, 0.5f);
        system.RaiseChanged();

        Assert.Equal(ActionOutcome.Deferred, result.Outcome);
    }

    [Fact]
    public void CaptureSessionsOfAnApp_AreControlledByACaptureTarget()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        FakeSession render = system.AddSession(DiscordV1, "spk");
        FakeSession capture = system.AddSession(DiscordV1, "mic", DeviceFlow.Capture);

        controller.SetVolume(new AudioTarget(AudioTargetKind.Application, DeviceFlow.Capture, App: new AppSelector("Discord.exe")), 0.2f);

        Assert.Equal(1f, render.Volume, 5);
        Assert.Equal(0.2f, capture.Volume, 5);
    }

    [Fact]
    public void InvalidApplicationTarget_Fails()
    {
        (_, AudioController controller) = Create();

        Assert.True(controller.SetVolume(new AudioTarget(AudioTargetKind.Application), 0.5f).IsFailure);
        Assert.True(controller.ToggleMute(new AudioTarget(AudioTargetKind.Application)).IsFailure);
    }
}
