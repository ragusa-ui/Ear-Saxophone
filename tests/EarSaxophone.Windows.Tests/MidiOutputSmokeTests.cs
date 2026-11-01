using EarSaxophone.Core.Midi;
using EarSaxophone.Windows.Midi;

namespace EarSaxophone.Windows.Tests;

/// <summary>
/// First real execution of the WinMM MIDI output. CI runners have no nanoKONTROL2, so this only proves that listing, a missing
/// device and a vanished port fail gracefully; whether the LEDs react must be checked with the real device.
/// </summary>
public class MidiOutputSmokeTests
{
    [Fact]
    public void Provider_ListsOutputs_WithoutThrowing()
    {
        Assert.NotNull(new NAudioMidiOutputProvider().GetDevices());
    }

    [Fact]
    public void MissingDevice_IsNoCrash()
    {
        using var connection = new MidiOutputConnection(new NAudioMidiOutputProvider(), "no-such-device-" + Guid.NewGuid().ToString("N"));
        var status = new List<string>();
        connection.StatusChanged += status.Add;

        connection.Poll(true);
        connection.Poll(true);

        Assert.False(connection.IsOpen);
        Assert.False(connection.TrySend(new ControlId(ControlKind.ControlChange, 0, 48), 127));
        Assert.Single(status);
    }

    [Fact]
    public void OpeningAPortThatDoesNotExist_ThrowsAndIsReportedNotCrashed()
    {
        var provider = new NAudioMidiOutputProvider();
        var missing = new MidiDeviceInfo(provider.GetDevices().Count + 50, "nanoKONTROL2 (gone)");

        Assert.ThrowsAny<Exception>(() => provider.Open(missing));
    }

    [Fact]
    public void FirstOutput_IfAny_CanSendAndBeDisposedTwice()
    {
        var provider = new NAudioMidiOutputProvider();
        IReadOnlyList<MidiDeviceInfo> devices = provider.GetDevices();
        if (devices.Count == 0)
        {
            return; // nothing to open on this machine
        }

        IMidiOutput output;
        try
        {
            output = provider.Open(devices[0]);
        }
        catch (Exception)
        {
            return; // the port exists but is busy or unusable here
        }

        output.SendControlChange(0, 48, 0); // a Control-Change without a note makes no sound on a synthesizer
        output.Dispose();
        output.Dispose();
        Assert.Throws<ObjectDisposedException>(() => output.SendControlChange(0, 48, 0));
    }
}
