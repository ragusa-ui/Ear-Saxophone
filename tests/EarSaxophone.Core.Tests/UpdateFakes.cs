using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using EarSaxophone.Core.Updates;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

/// <summary>A program folder and a temp folder for one update test, removed afterwards.</summary>
internal sealed class UpdateFolders : IDisposable
{
    public UpdateFolders()
    {
        Directory.CreateDirectory(App);
        Directory.CreateDirectory(Temp);
    }

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "earsax-update-" + Guid.NewGuid().ToString("N"));

    public string App => Path.Combine(Root, "app");

    public string Temp => Path.Combine(Root, "temp");

    public string AppFile(string relative) => Path.Combine(App, relative);

    public void WriteApp(string relative, string content)
    {
        string path = AppFile(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public string ReadApp(string relative) => File.ReadAllText(AppFile(relative));

    /// <summary>Every file of the program folder with its content, for "nothing changed" comparisons.</summary>
    public Dictionary<string, string> Snapshot() =>
        Directory.EnumerateFiles(App, "*", SearchOption.AllDirectories)
            .ToDictionary(p => Path.GetRelativePath(App, p).Replace('\\', '/'), File.ReadAllText);

    /// <summary>A folder with the given files, as an unpacked package.</summary>
    public string MakeSource(IReadOnlyDictionary<string, string> files)
    {
        string dir = Path.Combine(Root, "source-" + Guid.NewGuid().ToString("N"));
        foreach ((string relative, string content) in files)
        {
            string path = Path.Combine(dir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        return dir;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal static class Packages
{
    /// <summary>A ZIP like release.yml builds it (one top-level folder) with the given files.</summary>
    public static byte[] Zip(IReadOnlyDictionary<string, string> files, string folder = "EarSaxophone-2.0.0-win-x64")
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string relative, string content) in files)
            {
                ZipArchiveEntry entry = zip.CreateEntry(folder.Length == 0 ? relative : folder + "/" + relative);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return memory.ToArray();
    }

    public static string Sha256Line(byte[] data, string name = "EarSaxophone-2.0.0-win-x64.zip") =>
        $"{Convert.ToHexStringLower(SHA256.HashData(data))}  {name}\r\n";

    public static Dictionary<string, string> Standard() => new()
    {
        ["EarSaxophone.exe"] = "exe v2",
        ["EarSaxophone.dll"] = "dll v2",
        ["de/EarSaxophone.resources.dll"] = "res v2",
        ["NEW-FILE.txt"] = "new",
        ["profiles.json"] = "FROM-ZIP-MUST-NOT-OVERWRITE",
        ["settings.json"] = "FROM-ZIP-MUST-NOT-OVERWRITE",
        ["error.log"] = "FROM-ZIP",
        ["profiles.json.bak"] = "FROM-ZIP",
    };

    public static ReleaseInfo Release(string version, bool draft = false, bool withZip = true, bool withChecksum = true)
    {
        string zip = $"EarSaxophone-{version}-win-x64.zip";
        return new ReleaseInfo(SemVersion.Parse(version), "v" + version, draft, version.Contains('-'),
            withZip ? new ReleaseAsset(zip, new Uri("https://example.test/" + zip), 1) : null,
            withZip && withChecksum ? new ReleaseAsset(zip + ".sha256", new Uri("https://example.test/" + zip + ".sha256"), 1) : null);
    }
}

internal sealed class FakeReleaseSource : IReleaseSource
{
    public List<ReleaseInfo> Releases { get; } = [];

    public UpdateSourceException? Failure { get; set; }

    public int Calls { get; private set; }

    public Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Failure is not null ? Task.FromException<IReadOnlyList<ReleaseInfo>>(Failure) : Task.FromResult<IReadOnlyList<ReleaseInfo>>(Releases.ToList());
    }
}

internal sealed class FakeDownloader : IFileDownloader
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public List<double> Progress { get; } = [];

    public UpdateSourceException? Failure { get; set; }

    /// <summary>Serves a package for every release: the ZIP and its correct checksum (unless overridden).</summary>
    public void Serve(ReleaseInfo release, byte[] zip, string? checksumText = null)
    {
        Files[release.Zip!.Url.AbsoluteUri] = zip;
        if (release.Checksum is { } checksum)
        {
            Files[checksum.Url.AbsoluteUri] = Encoding.UTF8.GetBytes(checksumText ?? Packages.Sha256Line(zip, release.Zip.Name));
        }
    }

    public async Task DownloadFileAsync(Uri url, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        byte[] data = Get(url);
        progress?.Report(0.5);
        Progress.Add(0.5);
        await File.WriteAllBytesAsync(destination, data, cancellationToken);
        progress?.Report(1.0);
        Progress.Add(1.0);
    }

    public Task<string> DownloadTextAsync(Uri url, CancellationToken cancellationToken) =>
        Failure is not null ? Task.FromException<string>(Failure) : Task.FromResult(Encoding.UTF8.GetString(Get(url)));

    private byte[] Get(Uri url) =>
        Files.TryGetValue(url.AbsoluteUri, out byte[]? data) ? data : throw new UpdateSourceException(UpdateSourceProblem.NotFound, "HTTP 404");
}

/// <summary>Real file operations, except that touching a file with the given name fails like a locked or read-only file.</summary>
internal sealed class FailingFileOperations : IFileOperations
{
    public string? FailCopyTo { get; set; }

    public string? FailMoveOf { get; set; }

    public string? FailRestoreOf { get; set; }

    public bool AccessDenied { get; set; }

    public List<string> Log { get; } = [];

    public void Move(string source, string destination)
    {
        Log.Add($"move {Path.GetFileName(source)} -> {Path.GetFileName(destination)}");
        if (Matches(FailMoveOf, source) || (Matches(FailRestoreOf, destination) && source.EndsWith(".old", StringComparison.Ordinal)))
        {
            Throw(source);
        }

        File.Move(source, destination, overwrite: false);
    }

    public void Copy(string source, string destination)
    {
        Log.Add($"copy {Path.GetFileName(destination)}");
        if (Matches(FailCopyTo, destination))
        {
            File.WriteAllText(destination, "HALF-WRITTEN"); // like a copy that stopped in the middle
            Throw(destination);
        }

        File.Copy(source, destination, overwrite: false);
    }

    public void Delete(string path) => File.Delete(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteEmptyDirectory(string path) => Directory.Delete(path, recursive: false);

    private static bool Matches(string? name, string path) =>
        name is not null && Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase);

    private void Throw(string path) =>
        throw (AccessDenied ? new UnauthorizedAccessException($"Access to '{path}' is denied.") : new IOException($"The file '{path}' is being used by another process."));
}

internal sealed class FakeRestarter : IAppRestarter
{
    public List<(string Executable, bool Verified)> Calls { get; } = [];

    public Exception? Failure { get; set; }

    public void Restart(string executable, bool checksumVerified)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Calls.Add((executable, checksumVerified));
    }
}

/// <summary>Answers every HTTP request with a fixed status and body (or throws like a missing connection).</summary>
internal sealed class StubHttpHandler(HttpStatusCode status, string body = "", Exception? failure = null) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (failure is not null)
        {
            return Task.FromException<HttpResponseMessage>(failure);
        }

        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
