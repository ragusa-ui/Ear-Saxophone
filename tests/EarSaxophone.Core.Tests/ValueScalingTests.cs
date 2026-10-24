using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Tests;

public class ValueScalingTests
{
    [Theory]
    [InlineData(0, 0f)]
    [InlineData(127, 1f)]
    public void Default_CoversFullRange(byte value, float expected) =>
        Assert.Equal(expected, new ValueScaling().Apply(value), 5);

    [Fact]
    public void Midpoint_IsAboutHalf() =>
        Assert.Equal(64 / 127f, new ValueScaling().Apply(64), 5);

    [Fact]
    public void Invert_FlipsDirection()
    {
        var scaling = new ValueScaling(Invert: true);
        Assert.Equal(1f, scaling.Apply(0), 5);
        Assert.Equal(0f, scaling.Apply(127), 5);
    }

    [Fact]
    public void CustomRange_IsHonoured()
    {
        var scaling = new ValueScaling(Min: 0.2f, Max: 0.8f);
        Assert.Equal(0.2f, scaling.Apply(0), 5);
        Assert.Equal(0.8f, scaling.Apply(127), 5);
    }

    [Fact]
    public void ValuesAbove127_AreClamped() =>
        Assert.Equal(1f, new ValueScaling().Apply(200), 5);

    [Theory]
    [InlineData(-0.1f, 1f, false)]
    [InlineData(0f, 1.1f, false)]
    [InlineData(0.9f, 0.1f, false)]
    [InlineData(0f, 1f, true)]
    public void Validity(float min, float max, bool valid) =>
        Assert.Equal(valid, new ValueScaling(min, max).IsValid);
}
