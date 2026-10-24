using EarSaxophone.Windows.Startup;
using Microsoft.Win32;

namespace EarSaxophone.Windows.Tests;

/// <summary>Uses a throw-away registry key under HKCU, never the real Run key.</summary>
public sealed class RegistryAutostartTests : IDisposable
{
    private readonly string _root = @"Software\EarSaxophone.Tests\" + Guid.NewGuid().ToString("N");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\EarSaxophone.Tests", throwOnMissingSubKey: false);

    private RegistryAutostart Create(string exe = @"C:\Apps\EarSaxophone\EarSaxophone.exe") => new(exe, "EarSaxophoneTest", _root);

    [Fact]
    public void Disabled_ByDefault_AndEnableDisableRoundTrips()
    {
        RegistryAutostart autostart = Create();
        Assert.False(autostart.IsEnabled);

        autostart.SetEnabled(true);
        Assert.True(autostart.IsEnabled);

        autostart.SetEnabled(false);
        Assert.False(autostart.IsEnabled);
    }

    [Fact]
    public void StoredCommand_QuotesThePathAndStartsMinimized()
    {
        Create(@"C:\Program Files\Ear Saxophone\EarSaxophone.exe").SetEnabled(true);

        using RegistryKey key = Registry.CurrentUser.OpenSubKey(_root)!;
        Assert.Equal("\"C:\\Program Files\\Ear Saxophone\\EarSaxophone.exe\" --minimized", key.GetValue("EarSaxophoneTest"));
    }

    [Fact]
    public void DisablingWhenNeverEnabled_DoesNotThrow() => Create().SetEnabled(false);

    [Fact]
    public void Refresh_RepairsAMovedProgramFolder_OnlyWhenEnabled()
    {
        Assert.False(Create(@"D:\New\EarSaxophone.exe").Refresh()); // not enabled: leave it alone

        Create(@"C:\Old\EarSaxophone.exe").SetEnabled(true);
        RegistryAutostart moved = Create(@"D:\New\EarSaxophone.exe");

        Assert.True(moved.Refresh());
        Assert.False(moved.Refresh()); // already correct
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(_root)!;
        Assert.Equal(moved.Command, key.GetValue("EarSaxophoneTest"));
    }
}
