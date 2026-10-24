using EarSaxophone.Core.Mapping;
using EarSaxophone.Windows.Audio;
using EarSaxophone.Windows.Midi;

namespace EarSaxophone.Windows.Tests;

/// <summary>
/// First real execution of the Windows adapters. CI runners have no MIDI hardware and usually no audio devices, so
/// these only prove that the native calls work and fail gracefully; they say nothing about behaviour with real devices.
/// </summary>
public class AdapterSmokeTests
{
    [Fact]
    public void MidiProvider_ListsDevices_WithoutThrowing()
    {
        var devices = new NAudioMidiInputProvider().GetDevices();

        Assert.NotNull(devices);
    }

    [Fact]
    public void AudioSystem_CanBeCreated_QueriedAndDisposed_WithoutThrowing()
    {
        using var system = new NAudioAudioSystem();

        Assert.NotNull(system.GetSessions());
        _ = system.GetDefaultEndpoint(DeviceFlow.Render, DeviceRole.Console);
        _ = system.GetDefaultEndpoint(DeviceFlow.Capture, DeviceRole.Console);
        Assert.Null(system.GetEndpoint("does-not-exist"));
    }
}
