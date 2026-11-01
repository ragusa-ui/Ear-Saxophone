using EarSaxophone.Core.Updates;

namespace EarSaxophone.Core.Tests;

public class UpdateInstallerTests
{
    private static UpdateFolders OldInstallation()
    {
        var folders = new UpdateFolders();
        folders.WriteApp("EarSaxophone.exe", "exe v1");
        folders.WriteApp("EarSaxophone.dll", "dll v1");
        folders.WriteApp("OldOnly.dll", "only in v1");
        folders.WriteApp("profiles.json", "KEEP-ME");
        folders.WriteApp("settings.json", "KEEP-ME-TOO");
        folders.WriteApp("error.log", "old log");
        folders.WriteApp("profiles.json.bak", "backup");
        return folders;
    }

    [Theory]
    [InlineData("profiles.json")]
    [InlineData("Settings.JSON")]
    [InlineData("error.log")]
    [InlineData("profiles.json.tmp")]
    [InlineData("profiles.json.corrupt")]
    [InlineData("settings.json.tmp")]
    [InlineData("anything.bak")]
    [InlineData("EarSaxophone.exe.old")]
    [InlineData("update-leftovers.txt")]
    [InlineData("sub/profiles.json")]
    [InlineData(@"sub\settings.json")]
    public void UserFiles_AreProtected(string name) => Assert.True(UpdateInstaller.IsProtected(name));

    [Theory]
    [InlineData("EarSaxophone.exe")]
    [InlineData("EarSaxophone.dll")]
    [InlineData("README.md")]
    [InlineData("de/EarSaxophone.resources.dll")]
    [InlineData("myprofiles.json")]
    public void ProgramFiles_AreNotProtected(string name) => Assert.False(UpdateInstaller.IsProtected(name));

    [Fact]
    public void Apply_ReplacesProgramFiles_AddsNewOnes_AndNeverTouchesUserFiles()
    {
        using UpdateFolders folders = OldInstallation();
        string source = folders.MakeSource(Packages.Standard());

        InstallResult result = new UpdateInstaller().Apply(source, folders.App);

        Assert.Equal("exe v2", folders.ReadApp("EarSaxophone.exe"));
        Assert.Equal("dll v2", folders.ReadApp("EarSaxophone.dll"));
        Assert.Equal("res v2", folders.ReadApp("de/EarSaxophone.resources.dll"));
        Assert.Equal("new", folders.ReadApp("NEW-FILE.txt"));
        Assert.Equal("only in v1", folders.ReadApp("OldOnly.dll")); // like Update.ps1: nothing is deleted
        Assert.Equal("KEEP-ME", folders.ReadApp("profiles.json"));
        Assert.Equal("KEEP-ME-TOO", folders.ReadApp("settings.json"));
        Assert.Equal("old log", folders.ReadApp("error.log"));
        Assert.Equal("backup", folders.ReadApp("profiles.json.bak"));
        Assert.Equal(4, result.FilesWritten);
        Assert.Equal(4, result.ProtectedSkipped);
        Assert.Empty(result.Leftovers); // nothing is running here, so the .old files are gone at once
        Assert.Empty(Directory.GetFiles(folders.App, "*.old", SearchOption.AllDirectories));
        Assert.False(File.Exists(folders.AppFile(UpdateInstaller.LeftoverListName)));
    }

    [Fact]
    public void Apply_RenamesBeforeWriting_SoARunningFileIsNeverOverwritten()
    {
        using UpdateFolders folders = OldInstallation();
        var files = new FailingFileOperations();

        new UpdateInstaller(files).Apply(folders.MakeSource(new Dictionary<string, string> { ["EarSaxophone.exe"] = "exe v2" }), folders.App);

        Assert.Equal(["move EarSaxophone.exe -> EarSaxophone.exe.old", "copy EarSaxophone.exe"], files.Log);
    }

    [Fact]
    public void LockedFile_RollsEverythingBack()
    {
        using UpdateFolders folders = OldInstallation();
        Dictionary<string, string> before = folders.Snapshot();
        // The files are processed in ordinal order: EarSaxophone.dll, EarSaxophone.exe, NEW-FILE.txt, de/…. The last one fails
        // (half written) after two files were renamed and replaced, one was added and a new folder was created.
        var files = new FailingFileOperations { FailCopyTo = "EarSaxophone.resources.dll" };

        var ex = Assert.Throws<UpdateInstallException>(() => new UpdateInstaller(files).Apply(folders.MakeSource(Packages.Standard()), folders.App));

        Assert.True(ex.RolledBack);
        Assert.False(ex.AccessDenied);
        Assert.Equal(before, folders.Snapshot());
        Assert.False(Directory.Exists(folders.AppFile("de")));
        Assert.Contains(files.Log, l => l == "copy NEW-FILE.txt"); // it really got that far
    }

    [Fact]
    public void FileThatCannotBeRenamed_RollsBack_AndReportsMissingWriteAccess()
    {
        using UpdateFolders folders = OldInstallation();
        Dictionary<string, string> before = folders.Snapshot();
        var files = new FailingFileOperations { FailMoveOf = "EarSaxophone.exe", AccessDenied = true };

        var ex = Assert.Throws<UpdateInstallException>(() => new UpdateInstaller(files).Apply(folders.MakeSource(Packages.Standard()), folders.App));

        Assert.True(ex.RolledBack);
        Assert.True(ex.AccessDenied);
        Assert.Equal(before, folders.Snapshot());
    }

    [Fact]
    public void FailedRestore_IsReported_NotHidden()
    {
        using UpdateFolders folders = OldInstallation();
        var files = new FailingFileOperations { FailCopyTo = "EarSaxophone.resources.dll", FailRestoreOf = "EarSaxophone.dll" };

        var ex = Assert.Throws<UpdateInstallException>(() => new UpdateInstaller(files).Apply(folders.MakeSource(Packages.Standard()), folders.App));

        Assert.False(ex.RolledBack);
        Assert.Contains(ex.RollbackProblems, p => p.EndsWith("EarSaxophone.dll", StringComparison.Ordinal));
        Assert.Equal("exe v1", folders.ReadApp("EarSaxophone.exe")); // the others were still restored
        Assert.True(File.Exists(folders.AppFile("EarSaxophone.dll.old"))); // the old file is not lost
    }

    [Fact]
    public void OldFileInUse_IsListed_AndDeletedAtTheNextStart()
    {
        using UpdateFolders folders = OldInstallation();
        var files = new UndeletableOperations("EarSaxophone.exe.old");

        InstallResult result = new UpdateInstaller(files).Apply(folders.MakeSource(Packages.Standard()), folders.App);

        Assert.Equal(["EarSaxophone.exe.old"], result.Leftovers);
        Assert.True(File.Exists(folders.AppFile("EarSaxophone.exe.old")));
        Assert.Equal("EarSaxophone.exe.old", File.ReadAllText(folders.AppFile(UpdateInstaller.LeftoverListName)).Trim());

        // Still in use at the next start: stays listed.
        Assert.Equal(1, new UpdateInstaller(files).CleanUpLeftovers(folders.App));

        // Then the old process is gone.
        Assert.Equal(0, new UpdateInstaller().CleanUpLeftovers(folders.App));
        Assert.False(File.Exists(folders.AppFile("EarSaxophone.exe.old")));
        Assert.False(File.Exists(folders.AppFile(UpdateInstaller.LeftoverListName)));
        Assert.Equal("exe v2", folders.ReadApp("EarSaxophone.exe"));
    }

    [Fact]
    public void Cleanup_DeletesOnlyListedOldFiles_InsideTheProgramFolder()
    {
        using UpdateFolders folders = OldInstallation();
        folders.WriteApp("notes.txt.old", "a file of the user");
        folders.WriteApp("x.dll.old", "leftover");
        string outside = Path.Combine(folders.Root, "outside.old");
        File.WriteAllText(outside, "outside");
        File.WriteAllLines(folders.AppFile(UpdateInstaller.LeftoverListName), ["x.dll.old", "../outside.old", "profiles.json", ""]);

        Assert.Equal(0, new UpdateInstaller().CleanUpLeftovers(folders.App));

        Assert.False(File.Exists(folders.AppFile("x.dll.old")));
        Assert.True(File.Exists(folders.AppFile("notes.txt.old")));
        Assert.True(File.Exists(outside));
        Assert.Equal("KEEP-ME", folders.ReadApp("profiles.json"));
    }

    [Fact]
    public void StaleOldFileFromAnEarlierUpdate_IsReplaced_OrAvoidedWhenStillLocked()
    {
        using UpdateFolders folders = OldInstallation();
        folders.WriteApp("EarSaxophone.exe.old", "from the update before");
        var files = new UndeletableOperations("EarSaxophone.exe.old");

        InstallResult result = new UpdateInstaller(files).Apply(folders.MakeSource(new Dictionary<string, string> { ["EarSaxophone.exe"] = "exe v2" }), folders.App);

        Assert.Equal("exe v2", folders.ReadApp("EarSaxophone.exe"));
        Assert.Equal("from the update before", folders.ReadApp("EarSaxophone.exe.old"));
        Assert.Empty(result.Leftovers); // exe v1 went to EarSaxophone.exe.1.old, which could be deleted
        Assert.False(File.Exists(folders.AppFile("EarSaxophone.exe.1.old")));
    }

    [Fact]
    public void FindPackageRoot_SearchesSubfolders()
    {
        using var folders = new UpdateFolders();
        string extracted = folders.MakeSource(new Dictionary<string, string>
        {
            ["EarSaxophone-2.0.0-win-x64/EarSaxophone.exe"] = "exe",
            ["EarSaxophone-2.0.0-win-x64/tools/EarSaxophone.exe"] = "deeper",
        });

        Assert.Equal(Path.Combine(extracted, "EarSaxophone-2.0.0-win-x64"), UpdateInstaller.FindPackageRoot(extracted));
        Assert.Null(UpdateInstaller.FindPackageRoot(folders.MakeSource(new Dictionary<string, string> { ["readme.txt"] = "x" })));
    }

    /// <summary>Like a running program: the named file can be renamed but not deleted.</summary>
    private sealed class UndeletableOperations(string name) : IFileOperations
    {
        public void Move(string source, string destination) => File.Move(source, destination, overwrite: false);

        public void Copy(string source, string destination) => File.Copy(source, destination, overwrite: false);

        public void Delete(string path)
        {
            if (Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.");
            }

            File.Delete(path);
        }

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void DeleteEmptyDirectory(string path) => Directory.Delete(path, recursive: false);
    }
}
