using System.Diagnostics;
using System.Security.Cryptography;
using EarSaxophone.Core.Updates;

namespace EarSaxophone.Windows.Tests;

/// <summary>
/// The update installer against real Windows file locking: a copied system program runs as "EarSaxophone.exe" in a test
/// program folder while the update is applied. Windows refuses to overwrite or delete a running .exe but allows renaming it,
/// which is what <see cref="UpdateInstaller"/> relies on. Only runs on Windows (CI); not executed on the development machine.
/// </summary>
public sealed class UpdateRunningExeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "earsax-wintest-" + Guid.NewGuid().ToString("N"));
    private readonly List<Process> _processes = [];
    private readonly List<IDisposable> _handles = [];

    public UpdateRunningExeTests()
    {
        Directory.CreateDirectory(App);
        File.WriteAllText(Path.Combine(App, "profiles.json"), "KEEP-ME");
    }

    private string App => Path.Combine(_root, "app");

    private string Exe => Path.Combine(App, UpdateInstaller.ExecutableName);

    [Fact]
    public void RunningExe_IsReplaced_AndItsOldFileIsDeletedOnceItHasExited()
    {
        Process running = StartCopiedSystemProgram();
        Assert.ThrowsAny<IOException>(() => File.WriteAllText(Exe, "overwrite")); // why the installer renames instead
        string source = Package(new() { ["EarSaxophone.exe"] = "new exe", ["EarSaxophone.dll"] = "new dll", ["profiles.json"] = "FROM-ZIP" });

        InstallResult result = new UpdateInstaller().Apply(source, App);

        Assert.Equal("new exe", File.ReadAllText(Exe));
        Assert.Equal("new dll", File.ReadAllText(Path.Combine(App, "EarSaxophone.dll")));
        Assert.Equal("KEEP-ME", File.ReadAllText(Path.Combine(App, "profiles.json")));
        Assert.Contains("EarSaxophone.exe.old", result.Leftovers);
        Assert.True(File.Exists(Exe + ".old"));
        Assert.False(running.HasExited, "the running program must not be disturbed by the update");

        var installer = new UpdateInstaller();
        Assert.Equal(1, installer.CleanUpLeftovers(App)); // still running: stays listed

        Stop(running);
        Assert.True(Retry(() => installer.CleanUpLeftovers(App) == 0), "the .old file could not be deleted after the program exited");
        Assert.False(File.Exists(Exe + ".old"));
        Assert.False(File.Exists(Path.Combine(App, UpdateInstaller.LeftoverListName)));
    }

    [Fact]
    public void LockedFile_RollsBack_IncludingTheRenamedRunningExe()
    {
        Process running = StartCopiedSystemProgram();
        string originalHash = Hash(Exe);
        string locked = Path.Combine(App, "zz-locked.dll"); // processed after EarSaxophone.exe (ordinal order)
        File.WriteAllText(locked, "old locked");
        _handles.Add(new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None)); // no FILE_SHARE_DELETE: cannot be renamed
        string source = Package(new() { ["EarSaxophone.exe"] = "new exe", ["zz-locked.dll"] = "new locked" });

        var ex = Assert.Throws<UpdateInstallException>(() => new UpdateInstaller().Apply(source, App));

        Assert.True(ex.RolledBack, string.Join("; ", ex.RollbackProblems));
        Assert.Equal(originalHash, Hash(Exe));
        Assert.Empty(Directory.GetFiles(App, "*.old"));
        Assert.False(running.HasExited);
        Assert.Equal("KEEP-ME", File.ReadAllText(Path.Combine(App, "profiles.json")));
    }

    [Fact]
    public void ReadOnlyFolder_IsReportedAsMissingWriteAccess()
    {
        File.WriteAllText(Exe, "old exe");
        string source = Package(new() { ["EarSaxophone.exe"] = "new exe", ["sub/new.dll"] = "x" });
        var info = new DirectoryInfo(App);
        var security = System.IO.FileSystemAclExtensions.GetAccessControl(info);
        var deny = new System.Security.AccessControl.FileSystemAccessRule(
            System.Security.Principal.WindowsIdentity.GetCurrent().User!,
            System.Security.AccessControl.FileSystemRights.CreateDirectories | System.Security.AccessControl.FileSystemRights.CreateFiles,
            System.Security.AccessControl.AccessControlType.Deny);
        security.AddAccessRule(deny);
        System.IO.FileSystemAclExtensions.SetAccessControl(info, security);
        try
        {
            var ex = Assert.Throws<UpdateInstallException>(() => new UpdateInstaller().Apply(source, App));

            Assert.True(ex.AccessDenied, ex.InnerException?.ToString());
            Assert.True(ex.RolledBack, string.Join("; ", ex.RollbackProblems));
            Assert.Equal("old exe", File.ReadAllText(Exe));
        }
        finally
        {
            security.RemoveAccessRule(deny);
            System.IO.FileSystemAclExtensions.SetAccessControl(info, security);
        }
    }

    public void Dispose()
    {
        foreach (IDisposable handle in _handles)
        {
            handle.Dispose();
        }

        foreach (Process process in _processes)
        {
            Stop(process);
            process.Dispose();
        }

        Retry(() =>
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }

                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Copies a program of Windows as EarSaxophone.exe into the test folder and starts it so that it keeps running for a
    /// minute. cmd.exe (running "ping" as a child) is tried first, ping.exe itself second.
    /// </summary>
    private Process StartCopiedSystemProgram()
    {
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var candidates = new (string Program, string Arguments)[]
        {
            ("cmd.exe", "/d /c ping -n 120 127.0.0.1 >nul"),
            ("PING.EXE", "-n 120 127.0.0.1"),
        };
        var tried = new List<string>();
        foreach ((string program, string arguments) in candidates)
        {
            File.Copy(Path.Combine(system, program), Exe, overwrite: true);
            var start = new ProcessStartInfo(Exe, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = App,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = Process.Start(start)!;
            _processes.Add(process);
            if (!process.WaitForExit(1500))
            {
                return process;
            }

            tried.Add($"{program} exited with {process.ExitCode}");
        }

        throw new InvalidOperationException("No copied system program kept running: " + string.Join(", ", tried));
    }

    private string Package(Dictionary<string, string> files)
    {
        string dir = Path.Combine(_root, "package-" + Guid.NewGuid().ToString("N"), "EarSaxophone-9.9.9-win-x64");
        foreach ((string relative, string content) in files)
        {
            string path = Path.Combine(dir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        return dir;
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10000);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already gone.
        }
    }

    private static bool Retry(Func<bool> action)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (action())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
