using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Tests;

public class AppMatcherTests
{
    private static AudioSessionInfo Session(string? path, bool system = false) =>
        new("k", 10, path, "dev", DeviceFlow.Render, system);

    [Theory]
    [InlineData("Discord.exe", @"C:\Users\x\AppData\Local\Discord\app-1.0.9100\Discord.exe")]
    [InlineData("discord", @"C:\Users\x\AppData\Local\Discord\app-1.0.9101\DISCORD.EXE")]
    [InlineData("DISCORD.EXE", "/opt/discord")]
    public void SameExecutable_Matches_RegardlessOfFolderCaseAndExtension(string selector, string path) =>
        Assert.True(AppMatcher.Matches(new AppSelector(selector), Session(path)));

    [Theory]
    [InlineData(@"C:\Apps\DiscordPTB.exe")]
    [InlineData(@"C:\Apps\Discord Canary.exe")]
    [InlineData(@"C:\Apps\NotDiscord.exe")]
    public void SimilarNames_DoNotMatch(string path) =>
        Assert.False(AppMatcher.Matches(new AppSelector("Discord.exe"), Session(path)));

    [Fact]
    public void SelectorWithPath_BreaksWhenAppMovesToNewVersionFolder()
    {
        var selector = new AppSelector("Discord.exe", @"C:\L\Discord\app-1.0.1\Discord.exe");
        Assert.True(AppMatcher.Matches(selector, Session(@"c:\l\discord\app-1.0.1\discord.exe")));
        Assert.False(AppMatcher.Matches(selector, Session(@"C:\L\Discord\app-1.0.2\Discord.exe")));
    }

    [Fact]
    public void SystemSoundsAndUnknownProcesses_NeverMatch()
    {
        Assert.False(AppMatcher.Matches(new AppSelector("Discord.exe"), Session(@"C:\x\Discord.exe", system: true)));
        Assert.False(AppMatcher.Matches(new AppSelector("Discord.exe"), Session(null)));
        Assert.False(AppMatcher.Matches(new AppSelector("  "), Session(@"C:\x\Discord.exe")));
    }
}
