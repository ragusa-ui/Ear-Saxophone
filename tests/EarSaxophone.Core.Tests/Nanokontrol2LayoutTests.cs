using EarSaxophone.Core.Devices;

namespace EarSaxophone.Core.Tests;

public class Nanokontrol2LayoutTests
{
    [Fact]
    public void Has51Elements_WithUniqueControlsAndLabels()
    {
        IReadOnlyList<LayoutElement> elements = Nanokontrol2Layout.Elements;

        Assert.Equal(51, elements.Count);
        Assert.Equal(51, elements.Select(e => e.Control).Distinct().Count());
        Assert.Equal(51, elements.Select(e => e.Label).Distinct().Count());
    }

    [Fact]
    public void ConsistsOfEightStripsAndATransportBlock()
    {
        IReadOnlyList<LayoutElement> elements = Nanokontrol2Layout.Elements;

        Assert.Equal(8, elements.Count(e => e.Kind == ElementKind.Knob));
        Assert.Equal(8, elements.Count(e => e.Kind == ElementKind.Fader));
        Assert.Equal(24 + 11, elements.Count(e => e.Kind == ElementKind.Button)); // S, M, R per strip + 11 transport keys
    }

    [Fact]
    public void AllElementsLieInsideTheDevicePicture_WithAMargin()
    {
        foreach (LayoutElement e in Nanokontrol2Layout.Elements)
        {
            Assert.True(e.X >= 20 && e.Y >= 20, $"{e.Label} too close to the left/top edge");
            Assert.True(e.X + e.Width <= Nanokontrol2Layout.DeviceWidth - 20, $"{e.Label} too close to the right edge");
            Assert.True(e.Y + e.Height <= Nanokontrol2Layout.DeviceHeight - 20, $"{e.Label} too close to the bottom edge");
        }
    }

    [Fact]
    public void NoTwoElementsOverlap_AndNeighboursKeepATouchableGap()
    {
        var elements = Nanokontrol2Layout.Elements;
        for (int i = 0; i < elements.Count; i++)
        {
            for (int j = i + 1; j < elements.Count; j++)
            {
                LayoutElement a = elements[i];
                LayoutElement b = elements[j];
                const double gap = 6; // elements must be at least this far apart
                bool apart = a.X + a.Width + gap <= b.X || b.X + b.Width + gap <= a.X
                    || a.Y + a.Height + gap <= b.Y || b.Y + b.Height + gap <= a.Y;
                Assert.True(apart, $"{a.Label} and {b.Label} are too close or overlap");
            }
        }
    }

    [Fact]
    public void FactoryControlNumbers_MatchTheUsualAssignment()
    {
        var byLabel = Nanokontrol2Layout.Elements.ToDictionary(e => e.Label, e => e.Control.Number);

        Assert.Equal(0, byLabel["Fader 1"]);
        Assert.Equal(7, byLabel["Fader 8"]);
        Assert.Equal(16, byLabel["Drehregler 1"]);
        Assert.Equal(32, byLabel["S 1"]);
        Assert.Equal(48, byLabel["M 1"]);
        Assert.Equal(64, byLabel["R 1"]);
        Assert.Equal(41, byLabel["Play"]);
        Assert.Equal(42, byLabel["Stopp"]);
        Assert.Equal(45, byLabel["Aufnahme"]);
    }

    [Fact]
    public void StripsAreEvenlySpaced_AndTheTransportBlockIsLeftOfThem()
    {
        var faders = Nanokontrol2Layout.Elements.Where(e => e.Kind == ElementKind.Fader).OrderBy(e => e.X).ToList();
        var pitch = faders.Zip(faders.Skip(1), (a, b) => b.X - a.X).Distinct().ToList();
        double transportRight = Nanokontrol2Layout.Elements.Where(e => e.Strip is null).Max(e => e.X + e.Width);
        double stripsLeft = Nanokontrol2Layout.Elements.Where(e => e.Strip is not null).Min(e => e.X);

        Assert.Single(pitch);
        Assert.True(transportRight < stripsLeft - 20);
    }
}
