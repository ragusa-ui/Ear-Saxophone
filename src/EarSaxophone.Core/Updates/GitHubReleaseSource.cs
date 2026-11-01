using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EarSaxophone.Core.Updates;

/// <summary>
/// Reads the release list of a public GitHub repository without sign-in (no token in the program). Uses
/// <c>/releases</c> and not <c>/releases/latest</c>, because "latest" skips pre-releases and every release so far is one.
/// A private repository answers 404, which is reported as <see cref="UpdateSourceProblem.NotFound"/>.
/// </summary>
public sealed class GitHubReleaseSource : IReleaseSource
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    private readonly HttpClient _http;
    private readonly Uri _listUrl;
    private readonly string _userAgent;

    public GitHubReleaseSource(HttpClient http, string owner, string repository, string userAgent)
    {
        _http = http;
        _listUrl = new Uri($"https://api.github.com/repos/{owner}/{repository}/releases?per_page=30");
        _userAgent = userAgent;
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, _listUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        string body;
        try
        {
            using HttpResponseMessage response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            ThrowForStatus(response.StatusCode);
            body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateSourceException(UpdateSourceProblem.Offline, "timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateSourceException(UpdateSourceProblem.Offline, ex.Message, ex);
        }

        return Parse(body);
    }

    /// <summary>Reads the JSON of <c>GET /repos/{owner}/{repo}/releases</c>. Entries without a valid version tag are skipped.</summary>
    public static IReadOnlyList<ReleaseInfo> Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new UpdateSourceException(UpdateSourceProblem.Failed, "release list is not an array");
            }

            var releases = new List<ReleaseInfo>();
            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                string? tag = String(item, "tag_name");
                if (!SemVersion.TryParse(tag, out SemVersion? version))
                {
                    continue;
                }

                var assets = new List<ReleaseAsset>();
                if (item.TryGetProperty("assets", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement asset in list.EnumerateArray())
                    {
                        if (String(asset, "name") is { } name
                            && Uri.TryCreate(String(asset, "browser_download_url"), UriKind.Absolute, out Uri? url)
                            && url.Scheme == Uri.UriSchemeHttps)
                        {
                            long size = asset.TryGetProperty("size", out JsonElement s) && s.TryGetInt64(out long value) ? value : 0;
                            assets.Add(new ReleaseAsset(name, url, size));
                        }
                    }
                }

                ReleaseAsset? zip = FindZip(assets, version);
                ReleaseAsset? checksum = zip is null
                    ? null
                    : assets.FirstOrDefault(a => a.Name.Equals(zip.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
                Uri.TryCreate(String(item, "html_url"), UriKind.Absolute, out Uri? page);
                releases.Add(new ReleaseInfo(version, tag!, Bool(item, "draft"), Bool(item, "prerelease"), zip, checksum, page));
            }

            return releases;
        }
        catch (JsonException ex)
        {
            throw new UpdateSourceException(UpdateSourceProblem.Failed, ex.Message, ex);
        }
    }

    /// <summary>The package of exactly this version (as release.yml names it), otherwise any portable win-x64 package of the release.</summary>
    public static ReleaseAsset? FindZip(IReadOnlyList<ReleaseAsset> assets, SemVersion version) =>
        assets.FirstOrDefault(a => a.Name.Equals($"EarSaxophone-{version}-win-x64.zip", StringComparison.OrdinalIgnoreCase))
        ?? assets.FirstOrDefault(a => a.Name.StartsWith("EarSaxophone-", StringComparison.OrdinalIgnoreCase)
            && a.Name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase));

    internal static void ThrowForStatus(HttpStatusCode status)
    {
        if ((int)status is >= 200 and < 300)
        {
            return;
        }

        UpdateSourceProblem problem = status switch
        {
            HttpStatusCode.NotFound => UpdateSourceProblem.NotFound,
            HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests => UpdateSourceProblem.RateLimited,
            HttpStatusCode.ProxyAuthenticationRequired or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout => UpdateSourceProblem.Offline,
            _ => UpdateSourceProblem.Failed,
        };
        throw new UpdateSourceException(problem, $"HTTP {(int)status}");
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}

/// <summary>Downloads release files over HTTPS (GitHub redirects asset downloads to its storage; redirects are followed).</summary>
public sealed class HttpFileDownloader(HttpClient http, string userAgent) : IFileDownloader
{
    private static readonly TimeSpan FileTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TextTimeout = TimeSpan.FromSeconds(30);

    public async Task DownloadFileAsync(Uri url, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(FileTimeout);
        try
        {
            using HttpRequestMessage request = Request(url, "application/octet-stream");
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            GitHubReleaseSource.ThrowForStatus(response.StatusCode);
            long? total = response.Content.Headers.ContentLength;
            await using Stream input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            byte[] buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
                done += read;
                if (total is > 0)
                {
                    progress?.Report(Math.Min(1.0, (double)done / total.Value));
                }
            }

            progress?.Report(1.0);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateSourceException(UpdateSourceProblem.Offline, "timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateSourceException(UpdateSourceProblem.Offline, ex.Message, ex);
        }
    }

    public async Task<string> DownloadTextAsync(Uri url, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TextTimeout);
        try
        {
            using HttpRequestMessage request = Request(url, "application/octet-stream");
            using HttpResponseMessage response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            GitHubReleaseSource.ThrowForStatus(response.StatusCode);
            return await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateSourceException(UpdateSourceProblem.Offline, "timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateSourceException(UpdateSourceProblem.Offline, ex.Message, ex);
        }
    }

    private HttpRequestMessage Request(Uri url, string accept)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        return request;
    }
}
