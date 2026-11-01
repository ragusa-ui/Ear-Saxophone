using System.IO.Compression;
using System.Security.Cryptography;

namespace EarSaxophone.Core.Updates;

public enum UpdateCheckStatus
{
    UpToDate,
    Available,

    /// <summary>The release list could not be read (private repository, offline, limited); <see cref="UpdateCheckResult.Problem"/> says why.</summary>
    NotReachable,
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, ReleaseInfo? Release = null, UpdateSourceProblem? Problem = null);

public enum UpdateFailure
{
    /// <summary>The download (package or checksum) failed; nothing was changed.</summary>
    Download,

    /// <summary>The checksum does not match; nothing was changed.</summary>
    ChecksumMismatch,

    /// <summary>The package is no valid ZIP or contains no EarSaxophone.exe; nothing was changed.</summary>
    InvalidPackage,

    /// <summary>Writing the program files failed; see <see cref="UpdateException.RolledBack"/>.</summary>
    Install,
}

public sealed class UpdateException(UpdateFailure failure, string detail, Exception? inner = null, bool rolledBack = true, bool accessDenied = false)
    : Exception(detail, inner)
{
    public UpdateFailure Failure { get; } = failure;

    /// <summary>True when the program folder is as it was before (always true for failures before installing).</summary>
    public bool RolledBack { get; } = rolledBack;

    public bool AccessDenied { get; } = accessDenied;
}

/// <param name="ChecksumVerified">False when the release has no .sha256 file (the package was installed without check).</param>
/// <param name="Executable">The new program file to start.</param>
public sealed record UpdateInstallResult(SemVersion Version, bool ChecksumVerified, string Executable, InstallResult Files);

/// <summary>
/// Looks for a newer release and installs it into the program folder: download into a temporary folder, SHA-256 check
/// against the release's <c>.sha256</c> file, unpack, find EarSaxophone.exe, replace the files (<see cref="UpdateInstaller"/>).
/// Restarting is left to the caller. Contains no Windows-specific code; tests run it with fake sources on Linux.
/// </summary>
public sealed class UpdateService
{
    /// <summary>The automatic check runs at most this often.</summary>
    public static readonly TimeSpan AutomaticInterval = TimeSpan.FromHours(24);

    private readonly IReleaseSource _source;
    private readonly IFileDownloader _downloader;
    private readonly UpdateInstaller _installer;
    private readonly string _tempRoot;

    public UpdateService(IReleaseSource source, IFileDownloader downloader, SemVersion currentVersion, string appDirectory,
        UpdateInstaller? installer = null, string? tempRoot = null)
    {
        _source = source;
        _downloader = downloader;
        CurrentVersion = currentVersion;
        AppDirectory = appDirectory;
        _installer = installer ?? new UpdateInstaller();
        _tempRoot = tempRoot ?? Path.GetTempPath();
    }

    public SemVersion CurrentVersion { get; }

    public string AppDirectory { get; }

    /// <summary>Whether the automatic check is due: never checked, the last check is 24 h or more ago, or the clock went back.</summary>
    public static bool IsAutomaticCheckDue(DateTimeOffset? lastCheck, DateTimeOffset now) =>
        lastCheck is not { } last || now - last >= AutomaticInterval || last > now + TimeSpan.FromMinutes(5);

    /// <summary>The highest version among the published releases (drafts and releases without package ignored), if any.</summary>
    public static ReleaseInfo? Newest(IEnumerable<ReleaseInfo> releases) =>
        releases.Where(r => !r.IsDraft && r.Zip is not null).OrderByDescending(r => r.Version).FirstOrDefault();

    /// <summary>Never throws for an unreachable source; the problem is part of the result.</summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ReleaseInfo> releases;
        try
        {
            releases = await _source.GetReleasesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UpdateSourceException ex)
        {
            return new UpdateCheckResult(UpdateCheckStatus.NotReachable, Problem: ex.Problem);
        }

        ReleaseInfo? newest = Newest(releases);
        return newest is not null && newest.Version > CurrentVersion
            ? new UpdateCheckResult(UpdateCheckStatus.Available, newest)
            : new UpdateCheckResult(UpdateCheckStatus.UpToDate, newest);
    }

    /// <summary>
    /// Downloads, checks and installs <paramref name="release"/>. Progress: 0..0.9 download, 0.95 unpacked, 1 installed.
    /// Throws <see cref="UpdateException"/>; before the install step nothing in the program folder is touched.
    /// </summary>
    public async Task<UpdateInstallResult> InstallAsync(ReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ReleaseAsset zip = release.Zip ?? throw new UpdateException(UpdateFailure.InvalidPackage, "release has no package");
        string work = Path.Combine(_tempRoot, "EarSaxophone-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            string zipPath = Path.Combine(work, "package.zip");
            string? expected = null;
            try
            {
                if (release.Checksum is { } checksum)
                {
                    expected = ParseChecksum(await _downloader.DownloadTextAsync(checksum.Url, cancellationToken).ConfigureAwait(false))
                        ?? throw new UpdateException(UpdateFailure.ChecksumMismatch, "checksum file has no SHA-256 value");
                }

                var download = progress is null ? null : new Progress(p => progress.Report(Math.Clamp(p, 0, 1) * 0.9));
                await _downloader.DownloadFileAsync(zip.Url, zipPath, download, cancellationToken).ConfigureAwait(false);
            }
            catch (UpdateSourceException ex)
            {
                throw new UpdateException(UpdateFailure.Download, ex.Message, ex);
            }
            catch (IOException ex)
            {
                throw new UpdateException(UpdateFailure.Download, ex.Message, ex);
            }

            if (expected is not null && !string.Equals(expected, await Sha256Async(zipPath, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
            {
                throw new UpdateException(UpdateFailure.ChecksumMismatch, "SHA-256 differs");
            }

            string extracted = Path.Combine(work, "unpacked");
            try
            {
                ZipFile.ExtractToDirectory(zipPath, extracted); // refuses entries that would land outside the folder
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                throw new UpdateException(UpdateFailure.InvalidPackage, ex.Message, ex);
            }

            string root = UpdateInstaller.FindPackageRoot(extracted)
                ?? throw new UpdateException(UpdateFailure.InvalidPackage, "no EarSaxophone.exe in the package");
            progress?.Report(0.95);
            cancellationToken.ThrowIfCancellationRequested();

            InstallResult files;
            try
            {
                files = _installer.Apply(root, AppDirectory);
            }
            catch (UpdateInstallException ex)
            {
                throw new UpdateException(UpdateFailure.Install, ex.Message, ex, ex.RolledBack, ex.AccessDenied);
            }

            progress?.Report(1.0);
            return new UpdateInstallResult(release.Version, expected is not null, Path.Combine(AppDirectory, UpdateInstaller.ExecutableName), files);
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Temporary files; Windows cleans the temp folder eventually.
            }
        }
    }

    /// <summary>The hash in a <c>.sha256</c> file ("&lt;hex&gt;  &lt;file name&gt;", as release.yml writes it), lower case; null if there is none.</summary>
    public static string? ParseChecksum(string text)
    {
        string first = text.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        first = first.TrimStart('﻿');
        return first.Length == 64 && first.All(char.IsAsciiHexDigit) ? first.ToLowerInvariant() : null;
    }

    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Reports synchronously (unlike <see cref="Progress{T}"/>, which posts to a captured context).</summary>
    private sealed class Progress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
