using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Devices;

public sealed record LayoutElement(string Label, ControlId Control);

public sealed record LayoutRow(string Title, IReadOnlyList<LayoutElement> Elements);

/// <summary>
/// The elements of a Korg nanoKONTROL2 with their control numbers in the factory mode (MIDI channel 1).
/// The numbers are taken from the usual factory assignment; if a device was reprogrammed, MIDI-Learn still finds
/// every control and shows it as an additional control.
/// </summary>
public static class Nanokontrol2Layout
{
    public static IReadOnlyList<LayoutRow> Rows { get; } = Build();

    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    private static LayoutRow Strips(string title, string labelPrefix, int firstNumber) =>
        new(title, Enumerable.Range(0, 8).Select(i => new LayoutElement($"{labelPrefix} {i + 1}", Cc(firstNumber + i))).ToList());

    private static IReadOnlyList<LayoutRow> Build() =>
    [
        Strips("Drehregler", "Drehregler", 16),
        Strips("Fader", "Fader", 0),
        Strips("S-Tasten (Solo)", "S", 32),
        Strips("M-Tasten (Mute)", "M", 48),
        Strips("R-Tasten (Rec)", "R", 64),
        new LayoutRow("Transport",
        [
            new LayoutElement("Track ◄", Cc(58)),
            new LayoutElement("Track ►", Cc(59)),
            new LayoutElement("Cycle", Cc(46)),
            new LayoutElement("Marker Set", Cc(60)),
            new LayoutElement("Marker ◄", Cc(61)),
            new LayoutElement("Marker ►", Cc(62)),
            new LayoutElement("Zurückspulen", Cc(43)),
            new LayoutElement("Vorspulen", Cc(44)),
            new LayoutElement("Stopp", Cc(42)),
            new LayoutElement("Play", Cc(41)),
            new LayoutElement("Aufnahme", Cc(45)),
        ]),
    ];
}
