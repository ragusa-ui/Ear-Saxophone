using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Audio;

/// <summary>
/// Decides whether an audio session belongs to an application. The identity is the executable name
/// (case-insensitive, ".exe" optional, exact match so "Discord" does not match "DiscordPTB").
/// An executable path in the selector additionally has to match; note that apps such as Discord install into
/// versioned folders, so a path match breaks after an update and is off by default.
/// </summary>
public static class AppMatcher
{
    public static bool Matches(AppSelector selector, AudioSessionInfo session)
    {
        if (session.IsSystemSounds || string.IsNullOrWhiteSpace(session.ExePath) || !selector.IsValid)
        {
            return false;
        }

        if (NormalizeName(selector.ExeName) != NormalizeName(session.ExePath))
        {
            return false;
        }

        return selector.ExePath is null || NormalizePath(selector.ExePath) == NormalizePath(session.ExePath);
    }

    /// <summary>The file name part of a path (works with both separators), original casing.</summary>
    public static string FileNameOf(string nameOrPath)
    {
        string trimmed = nameOrPath.Trim();
        int cut = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        return cut >= 0 ? trimmed[(cut + 1)..] : trimmed;
    }

    public static string NormalizeName(string nameOrPath)
    {
        string file = FileNameOf(nameOrPath);
        if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            file = file[..^4];
        }

        return file.ToLowerInvariant();
    }

    public static string NormalizePath(string path) => path.Trim().Replace('\\', '/').ToLowerInvariant();
}
