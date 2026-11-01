using EarSaxophone.Core.Updates;

namespace EarSaxophone.Core.Tests;

public class SemVersionTests
{
    [Theory]
    [InlineData("1.0.0-rc2", "1.0.0-rc3")]
    [InlineData("1.0.0-rc3", "1.0.0")]
    [InlineData("1.0.0-rc9", "1.0.0-rc10")]   // digits compare as numbers (deliberate, release tags are written that way)
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")] // numeric parts rank below alphanumeric ones
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-beta", "1.0.0-rc1")]
    [InlineData("1.0.0", "1.0.1-rc1")]
    [InlineData("0.9.9", "0.10.0")]
    [InlineData("1.9.0", "2.0.0-rc1")]
    [InlineData("0.0.0-test", "0.1.0")]
    public void Orders_AsReleasesAreMeant(string lower, string higher)
    {
        SemVersion a = SemVersion.Parse(lower), b = SemVersion.Parse(higher);

        Assert.True(a < b, $"{lower} < {higher}");
        Assert.True(b > a);
        Assert.True(a.CompareTo(b) < 0 && b.CompareTo(a) > 0);
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("v1.0.0-rc3", "1.0.0-rc3")]
    [InlineData("1.0.0-rc3+abc123", "1.0.0-rc3")] // InformationalVersion carries the commit after '+'
    [InlineData(" 2.1.0 ", "2.1.0")]
    [InlineData("1.0.0-RC3", "1.0.0-rc3")]
    public void Parses_TagsAndInformationalVersions(string text, string expected)
    {
        Assert.True(SemVersion.TryParse(text, out SemVersion? version));
        Assert.Equal(SemVersion.Parse(expected), version);
        Assert.Equal(0, version!.CompareTo(SemVersion.Parse(expected)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("1.x.0")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0-rc..1")]
    [InlineData("1.0.0-rc_1")]
    [InlineData("-1.0.0")]
    public void Rejects_InvalidText(string? text) => Assert.False(SemVersion.TryParse(text, out _));

    [Fact]
    public void ToString_IsTheCanonicalForm()
    {
        Assert.Equal("1.0.0-rc3", SemVersion.Parse("v1.0.0-rc3+sha").ToString());
        Assert.Equal("2.0.0", SemVersion.Parse("2.0.0").ToString());
        Assert.False(SemVersion.Parse("2.0.0").IsPrerelease);
        Assert.True(SemVersion.Parse("2.0.0-rc1").IsPrerelease);
    }
}
