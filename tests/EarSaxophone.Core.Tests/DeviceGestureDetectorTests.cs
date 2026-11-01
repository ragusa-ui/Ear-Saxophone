using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

public class DeviceGestureDetectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly DeviceGestureDetector _detector = new();

    private ControlId? Feed(int cc, int value, int ms, ControlShape shape = ControlShape.Continuous) =>
        _detector.Observe(new MidiMessage(MidiMessageKind.ControlChange, 0, (byte)cc, (byte)value), shape, T0.AddMilliseconds(ms));

    private static ControlId Cc(int n) => ViewModelHarness.Cc(n);

    [Fact]
    public void AGestureCountsOnce_AndAgainOnlyAfterTheQuietTime()
    {
        Assert.Equal(Cc(0), Feed(0, 10, 0));
        Assert.Null(Feed(0, 20, 100));
        Assert.Null(Feed(0, 30, 699));             // 599 ms after the last message: same gesture
        Assert.Equal(Cc(0), Feed(0, 40, 1299));    // exactly 600 ms of rest: new gesture
    }

    [Fact]
    public void AnotherControl_IsIgnoredWhileTheFirstIsOperated_AndCountsWhenItRests()
    {
        Assert.Equal(Cc(0), Feed(0, 10, 0));
        Assert.Null(Feed(1, 10, 599));
        Assert.Equal(Cc(1), Feed(1, 20, 600));
    }

    [Fact]
    public void NoiseOfAnotherControl_DoesNotBlock_AnUncountedGesture()
    {
        Assert.Equal(Cc(0), Feed(0, 64, 0));
        Assert.Equal(Cc(1), Feed(1, 30, 1000));     // fader 2 is now the operated one
        Assert.Null(Feed(0, 65, 1100));             // fader 1 jitters by one step (blocked anyway)
        Assert.Null(Feed(0, 65, 2000));             // ... and once fader 2 rests: still only noise
        Assert.Equal(Cc(2), Feed(2, 127, 2050, ControlShape.Button)); // 50 ms after the noise: the noise does not block
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(-2, true)]
    public void AFaderMustMoveTwoStepsFromWhereItRested(int delta, bool counts)
    {
        Feed(0, 64, 0);
        Assert.Equal(counts ? Cc(0) : null, Feed(0, 64 + delta, 5000));
    }

    [Fact]
    public void KeysCountWhenPressed_NotWhenReleased()
    {
        Assert.Null(Feed(41, 0, 0, ControlShape.Button));
        Assert.Null(Feed(41, 63, 1000, ControlShape.Button));
        Assert.Equal(Cc(41), Feed(41, 64, 2000, ControlShape.Button));
        Assert.Null(Feed(41, 0, 2100, ControlShape.Button));
    }

    [Fact]
    public void UnknownControls_CountWhenPressedOrClearlyMoved()
    {
        Assert.Equal(Cc(100), Feed(100, 5, 0, ControlShape.Unknown));   // first value ever
        Assert.Null(Feed(100, 6, 1000, ControlShape.Unknown));
        Assert.Equal(Cc(100), Feed(100, 127, 3000, ControlShape.Unknown));
    }
}
