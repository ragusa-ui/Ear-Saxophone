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

/// <summary>Visual style of the whole window (colours, shapes, fonts, look of the device picture).</summary>
public enum AppStyle
{
    Studio,
    Papier,
    Minimal,
    Amber,
    Blueprint,
    Sunset,
    Rack,
    Messing,
}

/// <param name="Style">Visual style of the main window, chosen in the gear menu.</param>
/// <param name="AskForNewPrograms">Ask on which control a program goes when it plays sound for the first time (see <c>ProgramPromptService</c>).</param>
/// <param name="IgnoredPrograms">Programs the user never wants to be asked about ("Nie fragen"), as executable names; null means none.</param>
/// <param name="CheckForUpdates">Look for a new version in the background at start (at most once per 24 h, see <c>UpdateService</c>).</param>
/// <param name="LastUpdateCheck">When the last automatic or manual update check ran (UTC); null = never.</param>
/// <param name="LedFeedback">Show mute states on the key LEDs of the device (see <c>LedController</c>).</param>
/// <param name="PromptSound">Play a short, quiet system sound when the question for a new program with sound appears.</param>
/// <param name="LedBlink">Muted keys blink (device LEDs and picture) instead of being lit steadily (see <c>LedController.Blink</c>).</param>
public sealed record AppSettings(
    bool StartWithWindows = false,
    bool KeepInTray = true,
    bool StartMinimized = false,
    bool ShowOsd = true,
    OsdPosition OsdPosition = OsdPosition.BottomRight,
    bool AskForNewPrograms = true,
    IReadOnlyList<string>? IgnoredPrograms = null,
    bool CheckForUpdates = true,
    DateTimeOffset? LastUpdateCheck = null,
    bool LedFeedback = true,
    bool PromptSound = true,
    bool LedBlink = true,
    AppStyle Style = AppStyle.Studio)
{
    /// <summary>The ignored programs, never null.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> IgnoredProgramList => IgnoredPrograms ?? [];

    // Lists compare by content (an empty list equals null), so an unchanged setting is not saved again.
    public bool Equals(AppSettings? other) =>
        other is not null
        && StartWithWindows == other.StartWithWindows
        && KeepInTray == other.KeepInTray
        && StartMinimized == other.StartMinimized
        && ShowOsd == other.ShowOsd
        && OsdPosition == other.OsdPosition
        && AskForNewPrograms == other.AskForNewPrograms
        && CheckForUpdates == other.CheckForUpdates
        && LastUpdateCheck == other.LastUpdateCheck
        && LedFeedback == other.LedFeedback
        && PromptSound == other.PromptSound
        && LedBlink == other.LedBlink
        && Style == other.Style
        && IgnoredProgramList.SequenceEqual(other.IgnoredProgramList);

    public override int GetHashCode() =>
        HashCode.Combine(StartWithWindows, KeepInTray, StartMinimized, ShowOsd, OsdPosition, AskForNewPrograms, IgnoredProgramList.Count,
            HashCode.Combine(CheckForUpdates, LastUpdateCheck, LedFeedback, PromptSound, LedBlink, Style));
}

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
