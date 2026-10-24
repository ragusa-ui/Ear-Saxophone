using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Persistence;

/// <summary>Result of loading; Warning is set when the stored file could not be used and defaults were returned.</summary>
public sealed record ProfileLoadResult(ProfileDocument Document, string? Warning);

/// <summary>
/// Reads and writes the profile file next to the executable (portable mode). Writes go to a temp file first so an
/// interrupted save never destroys the previous file. An unreadable file is kept as "*.corrupt" instead of being lost.
/// </summary>
public sealed class ProfileStore(string path)
{
    public string Path { get; } = path;

    public ProfileLoadResult Load()
    {
        if (!File.Exists(Path))
        {
            return new ProfileLoadResult(ProfileDocument.CreateDefault(), null);
        }

        try
        {
            return new ProfileLoadResult(ProfileSerializer.Deserialize(File.ReadAllText(Path)), null);
        }
        catch (Exception ex) when (ex is ProfileFormatException or IOException or UnauthorizedAccessException)
        {
            string backup = Path + ".corrupt";
            string note;
            try
            {
                File.Move(Path, backup, overwrite: true);
                note = Texts.ProfileBackupKept(backup);
            }
            catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
            {
                note = Texts.ProfileBackupFailed;
            }

            return new ProfileLoadResult(
                ProfileDocument.CreateDefault(),
                Texts.ProfileLoadFailed(ex.Message, note));
        }
    }

    public void Save(ProfileDocument document)
    {
        string? directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temp = Path + ".tmp";
        File.WriteAllText(temp, ProfileSerializer.Serialize(document));
        File.Move(temp, Path, overwrite: true);
    }
}
