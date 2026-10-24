using EarSaxophone.Core.Settings;
using Microsoft.Win32;

namespace EarSaxophone.Windows.Startup;

/// <summary>
/// Starts the program with Windows through the per-user Run key (no administrator rights needed).
/// The stored command contains the full path, so moving the program folder breaks it until the program is started once
/// from the new place; <see cref="Refresh"/> repairs the entry then.
/// </summary>
public sealed class RegistryAutostart(string exePath, string valueName = "EarSaxophone", string subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run") : IAutostart
{
    public string Command => $"\"{exePath}\" --minimized";

    public bool IsEnabled
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath);
            return key?.GetValue(valueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath);
            key.SetValue(valueName, Command, RegistryValueKind.String);
        }
        else
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    /// <summary>If autostart is on but points to another location (folder was moved), rewrite it. Returns true when changed.</summary>
    public bool Refresh()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath);
        if (key?.GetValue(valueName) is not string current || current == Command)
        {
            return false;
        }

        SetEnabled(true);
        return true;
    }
}
