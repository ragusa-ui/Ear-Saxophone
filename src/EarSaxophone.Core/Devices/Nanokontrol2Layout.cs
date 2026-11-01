using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Devices;

public enum ElementKind
{
    Knob,
    Fader,
    Button,
}

/// <summary>
/// One control of the device with its control number and its place in the schematic picture of the device
/// (design units; the picture is <see cref="Nanokontrol2Layout.DeviceWidth"/> x <see cref="Nanokontrol2Layout.DeviceHeight"/>).
/// </summary>
public sealed record LayoutElement(
    string Label,
    ControlId Control,
    ElementKind Kind,
    double X,
    double Y,
    double Width,
    double Height,
    string Glyph = "",
    string Caption = "",
    string GlyphColor = "#DDE1EE");

/// <summary>
/// Schematic layout of a Korg nanoKONTROL2 (transport block on the left, eight channel strips with knob, S, M, R and fader)
/// with the control numbers of the factory mode (MIDI channel 1). The numbers are the usual factory assignment; if a device
/// was reprogrammed, MIDI-Learn still finds every control and shows it as an additional control. The picture is a
/// schematic, not true to scale.
/// </summary>
public static class Nanokontrol2Layout
{
    public const double DeviceWidth = 1000;
    public const double DeviceHeight = 460;

    public const double KnobSize = 60;
    public const double ButtonWidth = 54;
    public const double ButtonHeight = 32;
    public const double FaderWidth = 46;
    public const double FaderHeight = 175;

    public static IReadOnlyList<LayoutElement> Elements { get; } = Build();

    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    private static IReadOnlyList<LayoutElement> Build()
    {
        var elements = new List<LayoutElement>();

        // Transport block: three columns of 62 x 44 buttons.
        const double tw = 62;
        const double th = 44;
        double[] column = [34, 106, 178];
        void Transport(string label, int cc, double x, double y, string glyph, string caption = "", string color = "#DDE1EE") =>
            elements.Add(new LayoutElement(label, Cc(cc), ElementKind.Button, x, y, tw, th, glyph, caption, color));

        Transport("Track ◄", 58, column[0], 44, "◄", "TRACK");
        Transport("Track ►", 59, column[1], 44, "►", "TRACK");
        Transport("Cycle", 46, column[2], 44, "↻", "CYCLE");
        Transport("Marker Set", 60, column[0], 124, "SET", "MARKER");
        Transport("Marker ◄", 61, column[1], 124, "◄", "MARKER");
        Transport("Marker ►", 62, column[2], 124, "►", "MARKER");
        Transport("Zurückspulen", 43, column[0], 232, "◄◄");
        Transport("Vorspulen", 44, column[1], 232, "►►");
        Transport("Stopp", 42, column[2], 232, "■");
        Transport("Play", 41, column[0], 296, "►", "", "#5EDC9A");
        Transport("Aufnahme", 45, column[1], 296, "●", "", "#F0605A");

        // Eight channel strips.
        for (int i = 0; i < 8; i++)
        {
            double centre = 350 + i * 80;
            int n = i + 1;
            elements.Add(new LayoutElement($"Drehregler {n}", Cc(16 + i), ElementKind.Knob, centre - KnobSize / 2, 36, KnobSize, KnobSize));
            elements.Add(new LayoutElement($"S {n}", Cc(32 + i), ElementKind.Button, centre - ButtonWidth / 2, 120, ButtonWidth, ButtonHeight, "S", GlyphColor: "#E8CB55"));
            elements.Add(new LayoutElement($"M {n}", Cc(48 + i), ElementKind.Button, centre - ButtonWidth / 2, 160, ButtonWidth, ButtonHeight, "M", GlyphColor: "#F29A4B"));
            elements.Add(new LayoutElement($"R {n}", Cc(64 + i), ElementKind.Button, centre - ButtonWidth / 2, 200, ButtonWidth, ButtonHeight, "R", GlyphColor: "#F0605A"));
            elements.Add(new LayoutElement($"Fader {n}", Cc(i), ElementKind.Fader, centre - FaderWidth / 2, 252, FaderWidth, FaderHeight, Caption: n.ToString()));
        }

        return elements;
    }
}
