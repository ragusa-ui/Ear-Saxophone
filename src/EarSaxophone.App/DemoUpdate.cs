using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using EarSaxophone.Core.Updates;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.App;

/// <summary>
/// CI only (--demo-screenshot): a fake release list and a fake download (no network), installing into a test folder in the
/// temp directory instead of the real program folder, and a restart that is only recorded. Lets <see cref="UiSelfCheck"/>
/// operate the update area and the banner end to end (download, SHA-256, unpack, replace files, user files kept).
/// </summary>
internal sealed class DemoUpdate
{
    public const string NewVersion = "9.9.9";

    public const string NewExeContent = "Ear Saxophone 9.9.9 (Demo)";

    public const string OldExeContent = "Ear Saxophone alte Version (Demo)";

    public const string UserFileContent = "BENUTZERDATEN-BLEIBEN";

    private static readonly Lazy<byte[]> Package = new(BuildPackage);

    private DemoUpdate(string root)
    {
        Root = root;
        AppDirectory = Path.Combine(root, "app");
        Directory.CreateDirectory(AppDirectory);
        File.WriteAllText(Path.Combine(AppDirectory, UpdateInstaller.ExecutableName), OldExeContent);
        File.WriteAllText(Path.Combine(AppDirectory, "profiles.json"), UserFileContent);
        File.WriteAllText(Path.Combine(AppDirectory, "settings.json"), UserFileContent);
        SemVersion current = SemVersion.TryParse(AppInfo.Version, out SemVersion? parsed) ? parsed : SemVersion.Parse("0.1.0");
        Service = new UpdateService(new Source(), new Downloader(), current, AppDirectory, tempRoot: root);
    }

    public string Root { get; }

    /// <summary>The test installation the demo update writes into (never the real program folder).</summary>
    public string AppDirectory { get; }

    public UpdateService Service { get; }

    public RecordingRestarter Restarter { get; } = new();

    public static DemoUpdate Create() => new(Path.Combine(Path.GetTempPath(), "earsax-demo-update-" + Guid.NewGuid().ToString("N")));

    private static Uri Url(string name) => new($"https://demo.invalid/{name}");

    // A package like release.yml builds it: one top-level folder; it also contains a profiles.json that must not be installed.
    private static byte[] BuildPackage()
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string content)
            {
                ZipArchiveEntry entry = zip.CreateEntry($"EarSaxophone-{NewVersion}-win-x64/{path}");
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            Add(UpdateInstaller.ExecutableName, NewExeContent);
            Add("EarSaxophone.dll", "demo library");
            Add("de/EarSaxophone.resources.dll", "demo resources");
            Add("profiles.json", "AUS-DER-ZIP-DARF-NICHT-UEBERSCHREIBEN");
        }

        return memory.ToArray();
    }

    public sealed class RecordingRestarter : IAppRestarter
    {
        public string? Executable { get; private set; }

        public int Calls { get; private set; }

        public void Restart(string executable, bool checksumVerified)
        {
            Executable = executable;
            Calls++;
        }
    }

    /// <summary>Includes a draft with a higher version and an older pre-release; only 9.9.9 may be offered.</summary>
    private sealed class Source : IReleaseSource
    {
        public Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken cancellationToken)
        {
            ReleaseInfo Release(string version, bool draft)
            {
                string zip = $"EarSaxophone-{version}-win-x64.zip";
                return new ReleaseInfo(SemVersion.Parse(version), "v" + version, draft, version.Contains('-'),
                    new ReleaseAsset(zip, Url(zip), Package.Value.Length), new ReleaseAsset(zip + ".sha256", Url(zip + ".sha256"), 100));
            }

            IReadOnlyList<ReleaseInfo> list = [Release("10.0.0", draft: true), Release(NewVersion, draft: false), Release("9.9.9-rc1", draft: false)];
            return Task.FromResult(list);
        }
    }

    private sealed class Downloader : IFileDownloader
    {
        public async Task DownloadFileAsync(Uri url, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            byte[] data = Package.Value;
            for (int step = 1; step <= 5; step++)
            {
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                progress?.Report(step / 5.0);
            }

            await File.WriteAllBytesAsync(destination, data, cancellationToken).ConfigureAwait(false);
        }

        public Task<string> DownloadTextAsync(Uri url, CancellationToken cancellationToken) =>
            Task.FromResult($"{Convert.ToHexStringLower(SHA256.HashData(Package.Value))}  EarSaxophone-{NewVersion}-win-x64.zip");
    }
}
