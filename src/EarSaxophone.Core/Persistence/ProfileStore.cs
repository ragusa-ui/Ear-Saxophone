using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Persistence;

/// <summary>
/// Result of loading; Warning is set when the stored file could not be used and defaults were returned.
/// MigratedFrom is the older schema version the file had (it was converted while loading), otherwise null.
/// </summary>
public sealed record ProfileLoadResult(ProfileDocument Document, string? Warning, int? MigratedFrom = null);

/// <summary>
/// Reads and writes the profile file next to the executable (portable mode). Writes go to a temp file first so an
/// interrupted save never destroys the previous file. An unreadable file is kept as "*.corrupt" instead of being lost.
/// Before a version 1 file is overwritten for the first time it is copied to "*.v1.bak" (an existing backup is never replaced).
/// </summary>
public sealed class ProfileStore(string path)
{
    public string Path { get; } = path;

    /// <summary>Where the original version 1 file is kept before it is first overwritten.</summary>
    public string V1BackupPath => Path + ".v1.bak";

    public ProfileLoadResult Load()
    {
        if (!File.Exists(Path))
        {
            return new ProfileLoadResult(ProfileDocument.CreateDefault(), null);
        }

        try
        {
            ProfileReadResult read = ProfileSerializer.Read(File.ReadAllText(Path));
            return new ProfileLoadResult(read.Document, null, read.WasMigrated ? read.SourceSchemaVersion : null);
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

        BackUpVersion1File();
        string temp = Path + ".tmp";
        File.WriteAllText(temp, ProfileSerializer.Serialize(document));
        File.Move(temp, Path, overwrite: true);
    }

    /// <summary>Keeps the file as it was written by version 1 of the format, once, before it is replaced by the current version.</summary>
    private void BackUpVersion1File()
    {
        if (!File.Exists(Path) || File.Exists(V1BackupPath) || ProfileSerializer.PeekSchemaVersion(File.ReadAllText(Path)) != 1)
        {
            return;
        }

        File.Copy(Path, V1BackupPath, overwrite: false);
    }
}
