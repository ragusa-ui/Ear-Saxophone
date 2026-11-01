using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;

namespace EarSaxophone.Core.Tests;

public class AudioControllerSwitchTests
{
    private static (FakeAudioSystem System, AudioController Controller) Create()
    {
        var system = new FakeAudioSystem();
        system.AddEndpoint("a", "Lautsprecher", DeviceFlow.Render);              // default
        system.AddEndpoint("c", "Monitor (HDMI)", DeviceFlow.Render, makeDefault: false);
        system.AddEndpoint("b", "Kopfhörer", DeviceFlow.Render, makeDefault: false);
        system.AddEndpoint("m1", "USB-Mikrofon", DeviceFlow.Capture);            // default
        system.AddEndpoint("m2", "Headset-Mikrofon", DeviceFlow.Capture, makeDefault: false);
        return (system, new AudioController(system));
    }

    private static string DefaultName(FakeAudioSystem system, DeviceFlow flow) =>
        system.GetDefaultEndpoint(flow, DeviceRole.Console)!.Name;

    [Fact]
    public void EachPress_SwitchesToTheNextDevice_ByName_AndWrapsAround()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        var action = new SwitchDeviceAction();

        // Sorted by name: Kopfhörer, Lautsprecher, Monitor (HDMI); current default is "Lautsprecher".
        Assert.Equal("Monitor (HDMI)", controller.Execute(Invocation(action))!.Detail);
        Assert.Equal("Kopfhörer", controller.Execute(Invocation(action))!.Detail);
        Assert.Equal("Lautsprecher", controller.Execute(Invocation(action))!.Detail);

        Assert.Equal("Lautsprecher", DefaultName(system, DeviceFlow.Render));
        Assert.Equal(3, system.DefaultChanges.Count);
    }

    [Fact]
    public void Result_NamesTheNewDevice_InGerman()
    {
        (_, AudioController controller) = Create();

        ActionResult result = controller.Execute(Invocation(new SwitchDeviceAction()))!;

        Assert.Equal(ActionOutcome.Applied, result.Outcome);
        Assert.Equal("Wiedergabegerät: Monitor (HDMI)", result.Message);
    }

    [Fact]
    public void ChosenSubset_IsTheOnlyRotation_AndAStartOutsideItEntersAtTheFirst()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        var action = new SwitchDeviceAction(DeviceFlow.Render, ["b", "c"]); // Kopfhörer, Monitor; current "Lautsprecher" is not in it

        Assert.Equal("Kopfhörer", controller.Execute(Invocation(action))!.Detail);
        Assert.Equal("Monitor (HDMI)", controller.Execute(Invocation(action))!.Detail);
        Assert.Equal("Kopfhörer", controller.Execute(Invocation(action))!.Detail);
        Assert.DoesNotContain(system.DefaultChanges, c => c.Id == "a");
    }

    [Fact]
    public void Microphone_IsSwitchedIndependently_OfThePlaybackDevice()
    {
        (FakeAudioSystem system, AudioController controller) = Create();

        ActionResult result = controller.Execute(Invocation(new SwitchDeviceAction(DeviceFlow.Capture)))!;

        Assert.Equal("Headset-Mikrofon", DefaultName(system, DeviceFlow.Capture));
        Assert.Equal("Headset-Mikrofon", result.Detail);   // sorted: Headset-Mikrofon, USB-Mikrofon; current USB -> wraps to Headset
        Assert.Equal("Lautsprecher", DefaultName(system, DeviceFlow.Render));
        Assert.All(system.DefaultChanges, c => Assert.Equal(DeviceFlow.Capture, c.Flow));
    }

    [Fact]
    public void SingleDevice_CannotBeSwitched_AndSaysWhy()
    {
        var system = new FakeAudioSystem();
        system.AddEndpoint("a", "Lautsprecher", DeviceFlow.Render);
        var controller = new AudioController(system);

        ActionResult result = controller.Execute(Invocation(new SwitchDeviceAction()))!;

        Assert.True(result.IsFailure);
        Assert.Contains("mindestens zwei", result.Message);
        Assert.Empty(system.DefaultChanges);
    }

    [Fact]
    public void NoMatchingDevice_FailsWithAClearMessage()
    {
        (FakeAudioSystem system, AudioController controller) = Create();

        ActionResult result = controller.Execute(Invocation(new SwitchDeviceAction(DeviceFlow.Render, ["unplugged"])))!;

        Assert.True(result.IsFailure);
        Assert.Contains("kein Wiedergabegerät", result.Message);
        Assert.Empty(system.DefaultChanges);
    }

    [Fact]
    public void WindowsRefusing_IsReported_WithTheDeviceName_AndNothingChanges()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        system.SetDefaultFailure = new AudioUnsupportedException("Windows meldet 0x80004002");

        ActionResult result = controller.Execute(Invocation(new SwitchDeviceAction()))!;

        Assert.True(result.IsFailure);
        Assert.Contains("Monitor (HDMI)", result.Message);
        Assert.Contains("abgelehnt", result.Message);
        Assert.Equal("Lautsprecher", DefaultName(system, DeviceFlow.Render));
    }

    [Fact]
    public void Executor_RunsTheSwitchAction()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        var executor = new ActionExecutor(controller, new RecordingKeyboard());

        ActionResult result = executor.Execute(Invocation(new SwitchDeviceAction()));

        Assert.Equal(ActionOutcome.Applied, result.Outcome);
        Assert.Equal("Monitor (HDMI)", DefaultName(system, DeviceFlow.Render));
    }

    [Fact]
    public void SwitchAction_IsSavedAndRestored_WithItsDeviceSelection()
    {
        var original = new SwitchDeviceAction(DeviceFlow.Capture, ["m1", "m2"]);
        var doc = new ProfileDocument(ProfileDocument.CurrentSchemaVersion, "p",
            [new Profile("p", [new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 58), original)])]);

        string json = ProfileSerializer.Serialize(doc);
        ProfileDocument copy = ProfileSerializer.Deserialize(json);

        Assert.Contains("\"type\": \"switchDevice\"", json);
        Assert.Equal(original, copy.Profiles[0].Mappings[0].Action);
        Assert.Empty(copy.Profiles.SelectMany(p => p.Validate()));
    }

    [Fact]
    public void SwitchAction_WithoutDeviceList_MeansAllDevices_AndComparesByContent()
    {
        Assert.Equal(new SwitchDeviceAction(), new SwitchDeviceAction(DeviceFlow.Render, []));
        Assert.NotEqual(new SwitchDeviceAction(), new SwitchDeviceAction(DeviceFlow.Capture));
        Assert.NotEqual(new SwitchDeviceAction(DeviceFlow.Render, ["a"]), new SwitchDeviceAction(DeviceFlow.Render, ["b"]));
    }

    [Fact]
    public void VolumeAndMuteResults_CarryLevelAndState_ForTheOnScreenDisplay()
    {
        (FakeAudioSystem system, AudioController controller) = Create();
        system.AddSession(@"C:\x\Discord.exe", "a");

        ActionResult volume = controller.SetVolume(AudioTarget.Speakers, 0.4f);
        ActionResult mute = controller.ToggleMute(AudioTarget.Speakers);
        ActionResult unmute = controller.ToggleMute(AudioTarget.Speakers);
        ActionResult appVolume = controller.SetVolume(AudioTarget.Application("Discord.exe"), 0.7f);
        ActionResult appMute = controller.ToggleMute(AudioTarget.Application("Discord.exe"));
        ActionResult idleApp = controller.SetVolume(AudioTarget.Application("Spotify.exe"), 0.2f);

        Assert.Equal(0.4f, volume.Level);
        Assert.True(mute.Muted);
        Assert.False(unmute.Muted);
        Assert.Equal(0.7f, appVolume.Level);
        Assert.True(appMute.Muted);
        Assert.Equal(ActionOutcome.Deferred, idleApp.Outcome);
        Assert.Equal(0.2f, idleApp.Level);
    }

    private static ActionInvocation Invocation(ActionDefinition action) =>
        new(new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 58), action), 1f);
}
