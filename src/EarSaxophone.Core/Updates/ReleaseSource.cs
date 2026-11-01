namespace EarSaxophone.Core.Updates;

/// <summary>One downloadable file of a release.</summary>
public sealed record ReleaseAsset(string Name, Uri Url, long Size);

/// <summary>A published release as the release source lists it (drafts and pre-releases included; <see cref="UpdateService"/> decides).</summary>
/// <param name="Zip">The portable package <c>EarSaxophone-&lt;version&gt;-win-x64.zip</c>; null when the release has none.</param>
/// <param name="Checksum">The matching <c>.sha256</c> file; null when the release has none.</param>
public sealed record ReleaseInfo(
    SemVersion Version,
    string Tag,
    bool IsDraft,
    bool IsPrerelease,
    ReleaseAsset? Zip,
    ReleaseAsset? Checksum,
    Uri? Page = null);

/// <summary>Where releases come from (GitHub in the program, a fake list in tests and in the CI demo).</summary>
public interface IReleaseSource
{
    /// <summary>Lists the releases; throws <see cref="UpdateSourceException"/> when they cannot be read.</summary>
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken cancellationToken);
}

/// <summary>Downloads release files.</summary>
public interface IFileDownloader
{
    /// <summary>Saves <paramref name="url"/> to <paramref name="destination"/>, reporting 0..1 when the size is known.</summary>
    Task DownloadFileAsync(Uri url, string destination, IProgress<double>? progress, CancellationToken cancellationToken);

    /// <summary>Reads a small text file (the checksum).</summary>
    Task<string> DownloadTextAsync(Uri url, CancellationToken cancellationToken);
}

public enum UpdateSourceProblem
{
    /// <summary>HTTP 404: the repository is private, was renamed, or does not exist. Without a token GitHub cannot tell these apart.</summary>
    NotFound,

    /// <summary>No connection (offline, DNS, proxy, timeout).</summary>
    Offline,

    /// <summary>HTTP 403/429: GitHub limits requests without sign-in (60 per hour and address).</summary>
    RateLimited,

    /// <summary>Any other unexpected answer.</summary>
    Failed,
}

public sealed class UpdateSourceException(UpdateSourceProblem problem, string detail, Exception? inner = null)
    : Exception(detail, inner)
{
    public UpdateSourceProblem Problem { get; } = problem;
}
