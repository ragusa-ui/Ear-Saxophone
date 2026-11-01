using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Devices;

/// <summary>
/// Everything the LED feedback assumes about the device, in one place. NONE OF THESE VALUES WAS TESTED ON REAL HARDWARE
/// (see docs/LED_RESEARCH.md); correct them here after a test with the device. The device only obeys when its LED mode is
/// set to "External" in the Korg KONTROL Editor.
/// </summary>
public sealed record LedOptions
{
    public static LedOptions Default { get; } = new();

    /// <summary>UNGEPRÜFT: value that switches a key LED on (research: 127, sent as Control-Change).</summary>
    public byte OnValue { get; init; } = 127;

    /// <summary>UNGEPRÜFT: value that switches a key LED off (research: 0).</summary>
    public byte OffValue { get; init; } = 0;

    /// <summary>UNGEPRÜFT: part of the name of the MIDI output port (compared without case; the output list is separate from the input list).</summary>
    public string OutputPortFragment { get; init; } = "nanokontrol";

    /// <summary>
    /// UNGEPRÜFT: the keys that have an LED. Channel and number of each LED are those of the key in the layout (factory scene,
    /// MIDI channel 1), assuming the LED listens on the same channel and number the key sends.
    /// </summary>
    public IReadOnlyList<ControlId> Controls { get; init; } = Nanokontrol2Leds.Controls;

    /// <summary>
    /// A message from the device that equals one of our own LED messages sent within this time is treated as an echo and ignored
    /// (only matters if a driver loops output back to input; the device itself is not expected to). Zero switches the filter off.
    /// </summary>
    public TimeSpan EchoWindow { get; init; } = TimeSpan.FromMilliseconds(30);
}

/// <summary>The keys of the nanoKONTROL2 that have an LED, taken from <see cref="Nanokontrol2Layout"/>.</summary>
public static class Nanokontrol2Leds
{
    // UNGEPRÜFT: S 32-39, M 48-55, R 64-71, Play 41, Stop 42, Rewind 43, Forward 44, Record 45, Cycle 46.
    // Track (58, 59) and Marker (60-62) are assumed to have no LED.
    private static readonly HashSet<int> LedNumbers =
        [.. Enumerable.Range(32, 8), .. Enumerable.Range(48, 8), .. Enumerable.Range(64, 8), 41, 42, 43, 44, 45, 46];

    public static IReadOnlyList<ControlId> Controls { get; } = Nanokontrol2Layout.Elements
        .Where(e => e.Kind == ElementKind.Button && LedNumbers.Contains(e.Control.Number))
        .Select(e => e.Control)
        .ToList();

    public static bool HasLed(ControlId control) => Controls.Contains(control);
}
