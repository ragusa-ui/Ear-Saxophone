using System.Text.Json;

namespace EarSaxophone.Core.Settings;

/// <summary>Per-user program settings. Autostart is stored by Windows itself; this value mirrors the last choice.</summary>
public enum OsdPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

public sealed record AppSettings(
    bool StartWithWindows = false,
    bool KeepInTray = true,
    bool StartMinimized = false,
    bool ShowOsd = true,
    OsdPosition OsdPosition = OsdPosition.BottomRight);

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string Path { get; } = path;

    /// <summary>Returns defaults when the file is missing or unreadable; settings are never worth blocking startup.</summary>
    public AppSettings Load()
    {
        try
        {
            return File.Exists(Path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string? directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, Path, overwrite: true);
    }
}

/// <summary>Registers the program to start with Windows. The Windows implementation writes the per-user Run key.</summary>
public interface IAutostart
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

/// <summary>
/// Chooses where profiles and settings live: next to the program (portable) when that folder is writable, otherwise
/// in the user's application-data folder (for example when the program was unpacked below Program Files).
/// </summary>
public static class DataLocation
{
    public static string Resolve(string programFolder, Func<string, bool>? isWritable = null, string? appDataRoot = null)
    {
        isWritable ??= CanWrite;
        if (isWritable(programFolder))
        {
            return programFolder;
        }

        string root = appDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(root, "EarSaxophone");
    }

    private static bool CanWrite(string folder)
    {
        try
        {
            string probe = System.IO.Path.Combine(folder, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
