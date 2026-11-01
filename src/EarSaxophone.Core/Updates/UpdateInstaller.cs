namespace EarSaxophone.Core.Updates;

/// <summary>The few file operations the installer needs; tests replace them to simulate locked or read-only files.</summary>
public interface IFileOperations
{
    /// <summary>Renames or moves a file; fails when the destination exists.</summary>
    void Move(string source, string destination);

    /// <summary>Copies a file; fails when the destination exists.</summary>
    void Copy(string source, string destination);

    void Delete(string path);

    void CreateDirectory(string path);

    void DeleteEmptyDirectory(string path);
}

public sealed class PhysicalFileOperations : IFileOperations
{
    public static PhysicalFileOperations Instance { get; } = new();

    public void Move(string source, string destination) => File.Move(source, destination, overwrite: false);

    public void Copy(string source, string destination) => File.Copy(source, destination, overwrite: false);

    public void Delete(string path) => File.Delete(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteEmptyDirectory(string path) => Directory.Delete(path, recursive: false);
}

/// <param name="FilesWritten">Program files that were written.</param>
/// <param name="ProtectedSkipped">Files of the package that were left out because they belong to the user (profiles, settings, log, backups).</param>
/// <param name="Leftovers">Old files that could not be deleted yet (the running program and its libraries); removed at the next start.</param>
public sealed record InstallResult(int FilesWritten, int ProtectedSkipped, IReadOnlyList<string> Leftovers);

/// <summary>Installing failed; <see cref="RolledBack"/> says whether every file was put back as it was.</summary>
public sealed class UpdateInstallException(Exception cause, bool rolledBack, IReadOnlyList<string> rollbackProblems)
    : Exception(cause.Message, cause)
{
    public bool RolledBack { get; } = rolledBack;

    public IReadOnlyList<string> RollbackProblems { get; } = rollbackProblems;

    public bool AccessDenied => InnerException is UnauthorizedAccessException;
}

/// <summary>
/// Replaces the program files without a script, while the program runs. Windows does not allow overwriting or deleting a
/// running .exe or a loaded .dll, but it allows renaming them: each existing file is renamed to <c>&lt;name&gt;.old</c> and the
/// new file is written in its place. If anything fails, everything done so far is undone (new files deleted, renamed ones
/// renamed back). The <c>.old</c> files that are still in use are listed in <see cref="LeftoverListName"/> and deleted at the
/// next start (<see cref="CleanUpLeftovers"/>); only listed files are ever deleted, nothing is guessed by pattern.
/// User files (profiles, settings, error.log, backups) are never written, even when the package contains them.
/// Files of the old version that the new package no longer contains stay (like the former Update.ps1).
/// </summary>
public sealed class UpdateInstaller(IFileOperations? files = null)
{
    public const string ExecutableName = "EarSaxophone.exe";

    public const string LeftoverListName = "update-leftovers.txt";

    private const string OldSuffix = ".old";

    private static readonly string[] ProtectedNames = ["profiles.json", "settings.json", "error.log", LeftoverListName];

    private readonly IFileOperations _files = files ?? PhysicalFileOperations.Instance;

    /// <summary>
    /// Whether a file of the package must never be written into the program folder: profiles.json, settings.json, error.log,
    /// their temporary/broken variants (profiles.json.tmp, profiles.json.corrupt, ...), any *.bak and *.old, and the leftover list.
    /// Compared by file name in any folder, ignoring case.
    /// </summary>
    public static bool IsProtected(string relativePath)
    {
        string name = Path.GetFileName(relativePath.Replace('\\', '/').TrimEnd('/'));
        if (name.Length == 0)
        {
            return true;
        }

        return ProtectedNames.Any(p => name.Equals(p, StringComparison.OrdinalIgnoreCase))
            || name.StartsWith("profiles.json.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("settings.json.", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(OldSuffix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The folder inside an unpacked package that holds EarSaxophone.exe (searched in all subfolders, the shallowest wins), or null.</summary>
    public static string? FindPackageRoot(string extractedDirectory) =>
        Directory.EnumerateFiles(extractedDirectory, ExecutableName, SearchOption.AllDirectories)
            .OrderBy(p => p.Count(c => c is '/' or '\\'))
            .ThenBy(p => p, StringComparer.Ordinal)
            .Select(Path.GetDirectoryName)
            .FirstOrDefault();

    /// <summary>Copies every program file of <paramref name="sourceDirectory"/> into <paramref name="appDirectory"/> (see the class remarks).</summary>
    public InstallResult Apply(string sourceDirectory, string appDirectory)
    {
        string source = Path.GetFullPath(sourceDirectory);
        string app = Path.GetFullPath(appDirectory);
        if (string.Equals(source.TrimEnd(Path.DirectorySeparatorChar), app.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The package folder must not be the program folder.", nameof(sourceDirectory));
        }

        var plan = new List<string>();
        int skipped = 0;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            if (IsProtected(relative))
            {
                skipped++;
            }
            else
            {
                plan.Add(relative);
            }
        }

        plan.Sort(StringComparer.Ordinal);
        var renamed = new List<(string Original, string Backup)>();
        var written = new List<string>();
        var createdDirectories = new List<string>();
        try
        {
            foreach (string relative in plan)
            {
                string target = Path.Combine(app, relative);
                CreateParents(app, Path.GetDirectoryName(target)!, createdDirectories);
                if (File.Exists(target))
                {
                    string backup = FreeBackupName(target);
                    _files.Move(target, backup);
                    renamed.Add((target, backup));
                }

                written.Add(target); // before copying: a half-written file is removed again on failure
                _files.Copy(Path.Combine(source, relative), target);
            }
        }
        catch (Exception ex)
        {
            List<string> problems = RollBack(renamed, written, createdDirectories);
            throw new UpdateInstallException(ex, problems.Count == 0, problems);
        }

        // Old files that are not in use (README, unchanged libraries that were not loaded) can go at once.
        var leftovers = new List<string>();
        foreach ((_, string backup) in renamed)
        {
            if (!TryDelete(backup))
            {
                leftovers.Add(Path.GetRelativePath(app, backup));
            }
        }

        RememberLeftovers(app, leftovers);
        return new InstallResult(plan.Count, skipped, leftovers);
    }

    /// <summary>
    /// Deletes the <c>.old</c> files listed by an earlier update (call at start; they may still be in use for a moment while
    /// the previous instance exits, so a retry is fine). Returns how many are left; the list is removed when none are.
    /// </summary>
    public int CleanUpLeftovers(string appDirectory)
    {
        string app = Path.GetFullPath(appDirectory);
        string list = Path.Combine(app, LeftoverListName);
        if (!File.Exists(list))
        {
            return 0;
        }

        var remaining = new List<string>();
        foreach (string line in File.ReadAllLines(list).Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string full = Path.GetFullPath(Path.Combine(app, line));
            bool inside = full.StartsWith(app.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!inside || !full.EndsWith(OldSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue; // never delete anything the installer did not create
            }

            if (File.Exists(full) && !TryDelete(full))
            {
                remaining.Add(line);
            }
        }

        if (remaining.Count == 0)
        {
            TryDelete(list);
        }
        else
        {
            File.WriteAllLines(list, remaining);
        }

        return remaining.Count;
    }

    private void CreateParents(string app, string directory, List<string> created)
    {
        var missing = new Stack<string>();
        for (string? d = directory; d is not null && !Directory.Exists(d) && d.Length > app.Length; d = Path.GetDirectoryName(d))
        {
            missing.Push(d);
        }

        while (missing.Count > 0)
        {
            string d = missing.Pop();
            _files.CreateDirectory(d);
            created.Add(d);
        }
    }

    private string FreeBackupName(string target)
    {
        string candidate = target + OldSuffix;
        for (int n = 1; File.Exists(candidate); n++)
        {
            if (TryDelete(candidate))
            {
                break; // a leftover of an earlier update that is no longer in use
            }

            candidate = $"{target}.{n}{OldSuffix}";
        }

        return candidate;
    }

    private List<string> RollBack(List<(string Original, string Backup)> renamed, List<string> written, List<string> createdDirectories)
    {
        var problems = new List<string>();
        for (int i = written.Count - 1; i >= 0; i--)
        {
            if (File.Exists(written[i]) && !TryDelete(written[i]))
            {
                problems.Add(written[i]);
            }
        }

        for (int i = renamed.Count - 1; i >= 0; i--)
        {
            try
            {
                _files.Move(renamed[i].Backup, renamed[i].Original);
            }
            catch (Exception)
            {
                problems.Add(renamed[i].Original);
            }
        }

        for (int i = createdDirectories.Count - 1; i >= 0; i--)
        {
            try
            {
                _files.DeleteEmptyDirectory(createdDirectories[i]);
            }
            catch (Exception)
            {
                // An empty folder that stays is harmless.
            }
        }

        return problems;
    }

    private void RememberLeftovers(string app, List<string> leftovers)
    {
        if (leftovers.Count == 0)
        {
            return;
        }

        try
        {
            string list = Path.Combine(app, LeftoverListName);
            IEnumerable<string> earlier = File.Exists(list) ? File.ReadAllLines(list) : [];
            File.WriteAllLines(list, earlier.Concat(leftovers).Where(l => l.Trim().Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The update itself succeeded; an .old file that stays is harmless.
        }
    }

    private bool TryDelete(string path)
    {
        try
        {
            _files.Delete(path);
            return !File.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
