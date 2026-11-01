using System.Net;
using EarSaxophone.Core.Updates;

namespace EarSaxophone.Core.Tests;

public class UpdateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static (UpdateService Service, FakeReleaseSource Source, FakeDownloader Downloader) Create(UpdateFolders folders, string current = "1.0.0-rc3")
    {
        var source = new FakeReleaseSource();
        var downloader = new FakeDownloader();
        return (new UpdateService(source, downloader, SemVersion.Parse(current), folders.App, tempRoot: folders.Temp), source, downloader);
    }

    private static UpdateFolders Installation()
    {
        var folders = new UpdateFolders();
        folders.WriteApp("EarSaxophone.exe", "exe v1");
        folders.WriteApp("EarSaxophone.dll", "dll v1");
        folders.WriteApp("profiles.json", "KEEP-ME");
        folders.WriteApp("settings.json", "KEEP-ME-TOO");
        return folders;
    }

    [Fact]
    public async Task Check_PicksTheHighestPublishedVersion_IncludingPrereleases_IgnoringDrafts()
    {
        using var folders = Installation();
        var (service, source, _) = Create(folders);
        source.Releases.AddRange([
            Packages.Release("1.0.0-rc2"),
            Packages.Release("1.0.0-rc4"),
            Packages.Release("1.0.0-rc10"),
            Packages.Release("2.0.0", draft: true),
            Packages.Release("1.5.0", withZip: false), // nothing to install
            Packages.Release("1.0.0-rc3"),
        ]);

        UpdateCheckResult result = await service.CheckAsync();

        Assert.Equal(UpdateCheckStatus.Available, result.Status);
        Assert.Equal("1.0.0-rc10", result.Release!.Version.ToString());
    }

    [Theory]
    [InlineData("1.0.0-rc3")]
    [InlineData("1.0.0")]
    public async Task Check_SameOrOlder_IsUpToDate(string current)
    {
        using var folders = Installation();
        var (service, source, _) = Create(folders, current);
        source.Releases.AddRange([Packages.Release("1.0.0-rc2"), Packages.Release("1.0.0-rc3")]);

        Assert.Equal(UpdateCheckStatus.UpToDate, (await service.CheckAsync()).Status);
    }

    [Fact]
    public async Task Check_ReleaseAboveTheCurrentPrerelease_IsOffered()
    {
        using var folders = Installation();
        var (service, source, _) = Create(folders, "1.0.0-rc3");
        source.Releases.Add(Packages.Release("1.0.0"));

        Assert.Equal(UpdateCheckStatus.Available, (await service.CheckAsync()).Status);
    }

    [Theory]
    [InlineData(UpdateSourceProblem.NotFound)]
    [InlineData(UpdateSourceProblem.Offline)]
    [InlineData(UpdateSourceProblem.RateLimited)]
    public async Task Check_UnreachableSource_IsAResult_NotAnException(UpdateSourceProblem problem)
    {
        using var folders = Installation();
        var (service, source, _) = Create(folders);
        source.Failure = new UpdateSourceException(problem, "x");

        UpdateCheckResult result = await service.CheckAsync();

        Assert.Equal(UpdateCheckStatus.NotReachable, result.Status);
        Assert.Equal(problem, result.Problem);
    }

    [Fact]
    public async Task Install_VerifiesChecksum_Unpacks_AndReplaces_KeepingUserFiles()
    {
        using var folders = Installation();
        var (service, _, downloader) = Create(folders);
        ReleaseInfo release = Packages.Release("2.0.0");
        downloader.Serve(release, Packages.Zip(Packages.Standard()));
        var progress = new List<double>();

        UpdateInstallResult result = await service.InstallAsync(release, new SyncProgress(progress.Add));

        Assert.True(result.ChecksumVerified);
        Assert.Equal(Path.Combine(folders.App, "EarSaxophone.exe"), result.Executable);
        Assert.Equal("exe v2", folders.ReadApp("EarSaxophone.exe"));
        Assert.Equal("res v2", folders.ReadApp("de/EarSaxophone.resources.dll"));
        Assert.Equal("KEEP-ME", folders.ReadApp("profiles.json"));
        Assert.Equal("KEEP-ME-TOO", folders.ReadApp("settings.json"));
        Assert.False(File.Exists(folders.AppFile("error.log")));
        Assert.Equal([0.45, 0.9, 0.95, 1.0], progress);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folders.Temp)); // the temporary folder is removed
    }

    [Theory]
    [InlineData("")]                                   // empty file
    [InlineData("not a checksum")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000  EarSaxophone-2.0.0-win-x64.zip")]
    public async Task Install_WrongOrUnreadableChecksum_ChangesNothing(string checksum)
    {
        using var folders = Installation();
        var (service, _, downloader) = Create(folders);
        ReleaseInfo release = Packages.Release("2.0.0");
        downloader.Serve(release, Packages.Zip(Packages.Standard()), checksum);
        Dictionary<string, string> before = folders.Snapshot();

        var ex = await Assert.ThrowsAsync<UpdateException>(() => service.InstallAsync(release));

        Assert.Equal(UpdateFailure.ChecksumMismatch, ex.Failure);
        Assert.True(ex.RolledBack);
        Assert.Equal(before, folders.Snapshot());
        Assert.Empty(Directory.EnumerateFileSystemEntries(folders.Temp));
    }

    [Fact]
    public async Task Install_ChecksumWithBomAndUpperCase_IsAccepted()
    {
        using var folders = Installation();
        var (service, _, downloader) = Create(folders);
        ReleaseInfo release = Packages.Release("2.0.0");
        byte[] zip = Packages.Zip(Packages.Standard());
        downloader.Serve(release, zip, "﻿" + Packages.Sha256Line(zip).ToUpperInvariant());

        Assert.True((await service.InstallAsync(release)).ChecksumVerified);
    }

    [Fact]
    public async Task Install_WithoutChecksumFile_Installs_AndSaysSo()
    {
        using var folders = Installation();
        var (service, _, downloader) = Create(folders);
        ReleaseInfo release = Packages.Release("2.0.0", withChecksum: false);
        downloader.Serve(release, Packages.Zip(Packages.Standard()));

        UpdateInstallResult result = await service.InstallAsync(release);

        Assert.False(result.ChecksumVerified);
        Assert.Equal("exe v2", folders.ReadApp("EarSaxophone.exe"));
    }

    [Fact]
    public async Task Install_PackageWithoutExe_ChangesNothing()
    {
        using var folders = Installation();
        var (service, _, downloader) = Create(folders);
        ReleaseInfo release = Packages.Release("2.0.0");
        downloader.Serve(release, Packages.Zip(new Dictionary<string, string> { ["other.txt"] = "x", ["profiles.json"] = "x" }));
        Dictionary<string, string> before = folders.Snapshot();

        var ex = await Assert.ThrowsAsync<UpdateException>(() => service.InstallAsync(release));

        Assert.Equal(UpdateFailure.InvalidPackage, ex.Failure);
        Assert.Equal(before, folders.Snapshot());
    }

    [Fact]
    public async Task Install_NotAZip_ChangesNothing()
    {
        using var folders = Installation();
        var (service, _, downloader) = Create(folders);
        ReleaseInfo release = Packages.Release("2.0.0");
        downloader.Serve(release, "this is no zip"u8.ToArray());
        Dictionary<string, string> before = folders.Snapshot();

        var ex = await Assert.ThrowsAsync<UpdateException>(() => service.InstallAsync(release));

        Assert.Equal(UpdateFailure.InvalidPackage, ex.Failure);
        Assert.Equal(before, folders.Snapshot());
    }

    [Fact]
    public async Task Install_ZipWithoutTopFolder_Works_LikeUpdatePs1()
    {
        using var folders = Installation();
        var (service, _, downloader) = Create(folders);
        ReleaseInfo release = Packages.Release("2.0.0");
        downloader.Serve(release, Packages.Zip(Packages.Standard(), folder: string.Empty));

        await service.InstallAsync(release);

        Assert.Equal("exe v2", folders.ReadApp("EarSaxophone.exe"));
        Assert.Equal("KEEP-ME", folders.ReadApp("profiles.json"));
    }

    [Fact]
    public async Task Install_Offline_ChangesNothing()
    {
        using var folders = Installation();
        var (service, _, downloader) = Create(folders);
        downloader.Failure = new UpdateSourceException(UpdateSourceProblem.Offline, "No such host is known.");
        Dictionary<string, string> before = folders.Snapshot();

        var ex = await Assert.ThrowsAsync<UpdateException>(() => service.InstallAsync(Packages.Release("2.0.0")));

        Assert.Equal(UpdateFailure.Download, ex.Failure);
        Assert.Equal(before, folders.Snapshot());
    }

    [Fact]
    public async Task Install_LockedFile_RollsBack()
    {
        using var folders = Installation();
        ReleaseInfo release = Packages.Release("2.0.0");
        var downloader = new FakeDownloader();
        downloader.Serve(release, Packages.Zip(Packages.Standard()));
        var service = new UpdateService(new FakeReleaseSource(), downloader, SemVersion.Parse("1.0.0"), folders.App,
            new UpdateInstaller(new FailingFileOperations { FailCopyTo = "NEW-FILE.txt" }), folders.Temp);
        Dictionary<string, string> before = folders.Snapshot();

        var ex = await Assert.ThrowsAsync<UpdateException>(() => service.InstallAsync(release));

        Assert.Equal(UpdateFailure.Install, ex.Failure);
        Assert.True(ex.RolledBack);
        Assert.Equal(before, folders.Snapshot());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(-25.0, true)]
    [InlineData(-24.0, true)]
    [InlineData(-23.9, false)]
    [InlineData(-1.0, false)]
    [InlineData(0.0, false)]
    [InlineData(48.0, true)] // the clock went back (or the file was edited): check again instead of never
    public void AutomaticCheck_RunsAtMostOncePer24Hours(double? hoursSinceLast, bool due) =>
        Assert.Equal(due, UpdateService.IsAutomaticCheckDue(hoursSinceLast is { } h ? Now.AddHours(h) : null, Now));

    [Fact]
    public void ParseChecksum_ReadsTheFormatOfReleaseYml()
    {
        const string hash = "3f2a1b0c3f2a1b0c3f2a1b0c3f2a1b0c3f2a1b0c3f2a1b0c3f2a1b0c3f2a1b0c";
        Assert.Equal(hash, UpdateService.ParseChecksum($"{hash}  EarSaxophone-1.0.0-win-x64.zip\r\n"));
        Assert.Equal(hash, UpdateService.ParseChecksum(hash.ToUpperInvariant()));
        Assert.Null(UpdateService.ParseChecksum(hash[..63]));
        Assert.Null(UpdateService.ParseChecksum(string.Empty));
    }

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}

public class GitHubReleaseSourceTests
{
    private const string Json = """
        [
          { "tag_name": "v1.0.0-rc3", "draft": false, "prerelease": true, "html_url": "https://github.com/o/r/releases/tag/v1.0.0-rc3",
            "assets": [
              { "name": "EarSaxophone-1.0.0-rc3-win-x64.zip", "size": 68000000, "browser_download_url": "https://github.com/o/r/releases/download/v1.0.0-rc3/EarSaxophone-1.0.0-rc3-win-x64.zip" },
              { "name": "EarSaxophone-1.0.0-rc3-win-x64.zip.sha256", "size": 100, "browser_download_url": "https://github.com/o/r/releases/download/v1.0.0-rc3/EarSaxophone-1.0.0-rc3-win-x64.zip.sha256" }
            ] },
          { "tag_name": "v1.1.0", "draft": true, "prerelease": false, "assets": [] },
          { "tag_name": "nightly", "draft": false, "prerelease": true, "assets": [] },
          { "tag_name": "v0.9.0", "draft": false, "prerelease": false,
            "assets": [ { "name": "EarSaxophone-0.9.0-win-x64.zip", "size": 1, "browser_download_url": "http://insecure.example/x.zip" } ] }
        ]
        """;

    [Fact]
    public void Parse_ReadsVersionsAssetsAndFlags()
    {
        IReadOnlyList<ReleaseInfo> releases = GitHubReleaseSource.Parse(Json);

        Assert.Equal(["1.0.0-rc3", "1.1.0", "0.9.0"], releases.Select(r => r.Version.ToString()));
        ReleaseInfo rc3 = releases[0];
        Assert.True(rc3.IsPrerelease);
        Assert.False(rc3.IsDraft);
        Assert.Equal("EarSaxophone-1.0.0-rc3-win-x64.zip", rc3.Zip!.Name);
        Assert.Equal(68000000, rc3.Zip.Size);
        Assert.Equal("EarSaxophone-1.0.0-rc3-win-x64.zip.sha256", rc3.Checksum!.Name);
        Assert.True(releases[1].IsDraft);
        Assert.Null(releases[2].Zip); // only https downloads are accepted
        Assert.Equal("1.0.0-rc3", UpdateService.Newest(releases)!.Version.ToString());
    }

    [Theory]
    [InlineData("{ \"message\": \"Not Found\" }")]
    [InlineData("not json")]
    public void Parse_UnexpectedAnswer_IsAFailure(string body) =>
        Assert.Equal(UpdateSourceProblem.Failed, Assert.Throws<UpdateSourceException>(() => GitHubReleaseSource.Parse(body)).Problem);

    [Theory]
    [InlineData(HttpStatusCode.NotFound, UpdateSourceProblem.NotFound)] // private repository (today)
    [InlineData(HttpStatusCode.Forbidden, UpdateSourceProblem.RateLimited)]
    [InlineData(HttpStatusCode.TooManyRequests, UpdateSourceProblem.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, UpdateSourceProblem.Failed)]
    [InlineData(HttpStatusCode.ServiceUnavailable, UpdateSourceProblem.Offline)]
    public async Task HttpStatus_IsMappedToAProblem(HttpStatusCode status, UpdateSourceProblem expected)
    {
        var handler = new StubHttpHandler(status, "{\"message\":\"Not Found\"}");
        using var http = new HttpClient(handler);
        var source = new GitHubReleaseSource(http, "ragusa-ui", "Ear-Saxophone", "EarSaxophone/1.0.0");

        var ex = await Assert.ThrowsAsync<UpdateSourceException>(() => source.GetReleasesAsync(CancellationToken.None));

        Assert.Equal(expected, ex.Problem);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/ragusa-ui/Ear-Saxophone/releases?per_page=30", request.RequestUri!.AbsoluteUri);
        Assert.Contains("EarSaxophone/1.0.0", request.Headers.UserAgent.ToString());
        Assert.Null(request.Headers.Authorization); // never a token
    }

    [Fact]
    public async Task NoConnection_IsOffline()
    {
        using var http = new HttpClient(new StubHttpHandler(HttpStatusCode.OK, failure: new HttpRequestException("No such host is known.")));
        var source = new GitHubReleaseSource(http, "o", "r", "EarSaxophone/1.0.0");

        var ex = await Assert.ThrowsAsync<UpdateSourceException>(() => source.GetReleasesAsync(CancellationToken.None));

        Assert.Equal(UpdateSourceProblem.Offline, ex.Problem);
    }

    [Fact]
    public async Task Success_ReturnsTheParsedList()
    {
        using var http = new HttpClient(new StubHttpHandler(HttpStatusCode.OK, Json));
        var source = new GitHubReleaseSource(http, "o", "r", "EarSaxophone/1.0.0");

        Assert.Equal(3, (await source.GetReleasesAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Downloader_ReportsProgress_AndMapsNotFound()
    {
        string dir = Path.Combine(Path.GetTempPath(), "earsax-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var ok = new HttpClient(new StubHttpHandler(HttpStatusCode.OK, new string('x', 200_000)));
            var progress = new List<double>();
            string file = Path.Combine(dir, "a.zip");
            await new HttpFileDownloader(ok, "ua").DownloadFileAsync(new Uri("https://example.test/a.zip"), file, new Collect(progress), CancellationToken.None);
            Assert.Equal(200_000, new FileInfo(file).Length);
            Assert.Equal(1.0, progress[^1]);
            Assert.True(progress.Count >= 2);

            using var missing = new HttpClient(new StubHttpHandler(HttpStatusCode.NotFound));
            var ex = await Assert.ThrowsAsync<UpdateSourceException>(() =>
                new HttpFileDownloader(missing, "ua").DownloadTextAsync(new Uri("https://example.test/a.sha256"), CancellationToken.None));
            Assert.Equal(UpdateSourceProblem.NotFound, ex.Problem);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class Collect(List<double> values) : IProgress<double>
    {
        public void Report(double value) => values.Add(value);
    }
}
