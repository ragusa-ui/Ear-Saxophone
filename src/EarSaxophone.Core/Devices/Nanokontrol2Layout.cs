using EarSaxophone.Core.Mapping;
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
/// <see cref="Strip"/> is the channel strip (column 1-8) of knob, S, M, R and fader; null for the transport keys.
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
    string GlyphColor = "#DDE1EE",
    int? Strip = null)
{
    /// <summary>Knobs and faders are stepless, every key is a button.</summary>
    public ControlShape Shape => Kind == ElementKind.Button ? ControlShape.Button : ControlShape.Continuous;
}

/// <summary>
/// Schematic layout of a Korg nanoKONTROL2 (transport block on the left, eight channel strips with knob, S, M, R and fader)
/// with the control numbers of the factory mode (MIDI channel 1). The numbers are the usual factory assignment; if a device
/// was reprogrammed, MIDI-Learn still finds every control and shows it as an additional control. The picture is a
/// schematic, not true to scale.
/// </summary>
public static class Nanokontrol2Layout
{
    public const double DeviceWidth = 1000;
    public const double DeviceHeight = 270;

    public const double KnobSize = 48;
    public const double ButtonSize = 30;
    public const double FaderWidth = 40;
    public const double FaderHeight = 170;

    public static IReadOnlyList<LayoutElement> Elements { get; } = Build();

    private static readonly Dictionary<ControlId, LayoutElement> ByControl = Elements.ToDictionary(e => e.Control);

    /// <summary>The element with this control number, or null for a control that is not part of the layout.</summary>
    public static LayoutElement? Find(ControlId control) => ByControl.GetValueOrDefault(control);

    /// <summary>Shape of a layout control; <see cref="ControlShape.Unknown"/> for controls outside the layout. Usable as <see cref="ShapeLookup"/>.</summary>
    public static ControlShape ShapeOf(ControlId control) => Find(control)?.Shape ?? ControlShape.Unknown;

    /// <summary>The channel strip (1-8) of a control, null for the transport keys and for controls outside the layout.</summary>
    public static int? StripOf(ControlId control) => Find(control)?.Strip;

    /// <summary>The fader of a channel strip (1-8), null for other numbers.</summary>
    public static ControlId? FaderOfStrip(int strip) =>
        Elements.FirstOrDefault(e => e.Strip == strip && e.Kind == ElementKind.Fader)?.Control;

    /// <summary>
    /// The fader of the strip the control belongs to (also for the fader itself), null outside the strips. Usable as
    /// <see cref="FaderLookup"/>.
    /// </summary>
    public static ControlId? StripFaderOf(ControlId control) => StripOf(control) is { } strip ? FaderOfStrip(strip) : null;

    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    private static IReadOnlyList<LayoutElement> Build()
    {
        var elements = new List<LayoutElement>();

        // Transport block as on the real device: small flat keys for TRACK, CYCLE and MARKER, a row of five square keys below.
        const double pillW = 40;
        const double pillH = 22;
        void Pill(string label, int cc, double x, double y, string glyph, string caption = "") =>
            elements.Add(new LayoutElement(label, Cc(cc), ElementKind.Button, x, y, pillW, pillH, glyph, caption));

        Pill("Track ◄", 58, 33, 110, "◄", "TRACK");
        Pill("Track ►", 59, 81, 110, "►", "TRACK");
        Pill("Cycle", 46, 33, 152, "CYCLE", "CYCLE");
        Pill("Marker Set", 60, 129, 152, "SET", "MARKER");
        Pill("Marker ◄", 61, 180, 152, "◄", "MARKER");
        Pill("Marker ►", 62, 228, 152, "►", "MARKER");

        const double sq = 36;
        void Square(string label, int cc, double x, string glyph) =>
            elements.Add(new LayoutElement(label, Cc(cc), ElementKind.Button, x, 198, sq, sq, glyph));

        Square("Zurückspulen", 43, 37, "◄◄");
        Square("Vorspulen", 44, 85, "►►");
        Square("Stopp", 42, 133, "■");
        Square("Play", 41, 181, "►");
        Square("Aufnahme", 45, 229, "●");

        // Eight channel strips: knob on top, S/M/R keys to the left of the fader slot.
        for (int i = 0; i < 8; i++)
        {
            double centre = 349 + i * 85;
            int n = i + 1;
            elements.Add(new LayoutElement($"Drehregler {n}", Cc(16 + i), ElementKind.Knob, centre - KnobSize / 2, 20, KnobSize, KnobSize, Strip: n));
            elements.Add(new LayoutElement($"S {n}", Cc(32 + i), ElementKind.Button, centre - 54, 84, ButtonSize, ButtonSize, "S", Strip: n));
            elements.Add(new LayoutElement($"M {n}", Cc(48 + i), ElementKind.Button, centre - 54, 134, ButtonSize, ButtonSize, "M", Strip: n));
            elements.Add(new LayoutElement($"R {n}", Cc(64 + i), ElementKind.Button, centre - 54, 184, ButtonSize, ButtonSize, "R", Strip: n));
            elements.Add(new LayoutElement($"Fader {n}", Cc(i), ElementKind.Fader, centre - 16, 76, FaderWidth, FaderHeight, Caption: n.ToString(), Strip: n));
        }

        return elements;
    }
}
