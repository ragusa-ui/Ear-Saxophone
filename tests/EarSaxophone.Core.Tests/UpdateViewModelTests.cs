using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.Updates;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

public sealed class UpdateViewModelTests : IDisposable
{
    private readonly ViewModelHarness _h = new();
    private readonly UpdateFolders _folders = new();
    private readonly FakeReleaseSource _source = new();
    private readonly FakeDownloader _downloader = new();
    private readonly FakeRestarter _restarter = new();

    public UpdateViewModelTests()
    {
        _folders.WriteApp("EarSaxophone.exe", "exe v1");
        _folders.WriteApp("profiles.json", "KEEP-ME");
    }

    public void Dispose()
    {
        _h.Dispose();
        _folders.Dispose();
    }

    private UpdateService Service(string current = "1.0.0-rc3") =>
        new(_source, _downloader, SemVersion.Parse(current), _folders.App, tempRoot: _folders.Temp);

    private MainViewModel Create() => _h.Create(Service(), _restarter);

    private ReleaseInfo Publish(string version, bool withChecksum = true, string? checksum = null)
    {
        ReleaseInfo release = Packages.Release(version, withChecksum: withChecksum);
        _source.Releases.Add(release);
        _downloader.Serve(release, Packages.Zip(Packages.Standard()), checksum);
        return release;
    }

    private AppSettings Saved() => new SettingsStore(_h.SettingsPath).Load();

    [Fact]
    public void AutomaticCheckAtStart_FindsTheNewVersion_ShowsTheBanner_AndRemembersTheTime()
    {
        Publish("1.0.0");

        MainViewModel vm = Create(); // the harness ticks once, like the timer right after start

        Assert.Equal(1, _source.Calls);
        Assert.True(vm.Update.IsUpdateAvailable);
        Assert.True(vm.Update.ShowBanner);
        Assert.Equal(Texts.UpdateAvailable("1.0.0"), vm.Update.BannerText);
        Assert.Equal(Texts.UpdateAvailable("1.0.0"), vm.Update.StatusText);
        Assert.Equal(Texts.UpdateInstalledVersion("1.0.0-rc3"), vm.Update.InstalledVersionText);
        Assert.True(vm.Update.InstallCommand.CanExecute(null));
        Assert.Equal(_h.Clock.GetUtcNow(), Saved().LastUpdateCheck);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void AutomaticCheck_RunsAtMostOncePer24Hours_AlsoAcrossRestarts()
    {
        Publish("1.0.0");
        MainViewModel vm = Create();
        vm.Tick();
        vm.Tick();
        Assert.Equal(1, _source.Calls);

        _h.Clock.Advance(TimeSpan.FromHours(23));
        vm.Tick();
        Assert.Equal(1, _source.Calls);

        // The program is started again within the 24 hours: the remembered time counts.
        MainViewModel second = _h.Create(Service(), _restarter);
        Assert.Equal(1, _source.Calls);

        _h.Clock.Advance(TimeSpan.FromHours(1));
        second.Tick();
        Assert.Equal(2, _source.Calls);
    }

    [Fact]
    public void AutomaticCheck_SwitchedOff_NeverRuns_ButTheButtonStillWorks()
    {
        new SettingsStore(_h.SettingsPath).Save(new AppSettings(CheckForUpdates: false));
        Publish("1.0.0");

        MainViewModel vm = Create();
        _h.Clock.Advance(TimeSpan.FromDays(3));
        vm.Tick();
        Assert.Equal(0, _source.Calls);
        Assert.False(vm.Update.CheckOnStart);

        vm.Update.CheckCommand.Execute(null);
        Assert.Equal(1, _source.Calls);
        Assert.True(vm.Update.IsUpdateAvailable);
    }

    [Fact]
    public void Switch_IsSavedInTheSettings()
    {
        MainViewModel vm = Create();
        var changed = new List<string?>();
        vm.Update.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Update.CheckOnStart = false;

        Assert.False(Saved().CheckForUpdates);
        Assert.Contains(nameof(UpdateViewModel.CheckOnStart), changed);
        vm.Update.CheckOnStart = true;
        Assert.True(Saved().CheckForUpdates);
    }

    [Theory]
    [InlineData(UpdateSourceProblem.NotFound, Texts.UpdateNotReachable)] // the repository is private today
    [InlineData(UpdateSourceProblem.Offline, Texts.UpdateNotReachable)]
    [InlineData(UpdateSourceProblem.RateLimited, Texts.UpdateRateLimited)]
    [InlineData(UpdateSourceProblem.Failed, Texts.UpdateUnexpectedAnswer)]
    public async Task UnreachableReleases_GiveAFriendlyText_AndNeverTheErrorBar(UpdateSourceProblem problem, string expected)
    {
        _source.Failure = new UpdateSourceException(problem, "HTTP 404");

        MainViewModel vm = Create(); // automatic check
        Assert.Equal(expected, vm.Update.StatusText);
        Assert.False(vm.HasError);
        Assert.False(vm.Update.ShowBanner);
        Assert.False(vm.Update.IsChecking);

        await vm.Update.CheckNowAsync(automatic: false); // the button
        Assert.Equal(expected, vm.Update.StatusText);
        Assert.False(vm.HasError);
        Assert.Equal(2, _source.Calls);
        Assert.NotNull(Saved().LastUpdateCheck); // a failed check also waits 24 h (no retry on every start)
    }

    [Fact]
    public async Task UpToDate_IsSaid_WhenTheButtonIsUsed()
    {
        Publish("1.0.0-rc3");
        MainViewModel vm = Create();

        await vm.Update.CheckNowAsync(automatic: false);

        Assert.Equal(Texts.UpdateUpToDate("1.0.0-rc3"), vm.Update.StatusText);
        Assert.False(vm.Update.IsUpdateAvailable);
        Assert.False(vm.Update.ShowBanner);
    }

    [Fact]
    public async Task InstallNow_Downloads_Replaces_AndRestarts()
    {
        Publish("1.0.0");
        MainViewModel vm = Create();
        var progress = new List<double>();
        vm.Update.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpdateViewModel.Progress))
            {
                progress.Add(vm.Update.Progress);
            }
        };

        await vm.Update.InstallAsync();

        Assert.Equal((Path.Combine(_folders.App, "EarSaxophone.exe"), true), Assert.Single(_restarter.Calls));
        Assert.Equal("exe v2", _folders.ReadApp("EarSaxophone.exe"));
        Assert.Equal("KEEP-ME", _folders.ReadApp("profiles.json"));
        Assert.Equal(1.0, vm.Update.Progress);
        Assert.Contains(0.45, progress);
        Assert.True(vm.Update.IsInstalling); // stays busy until the program has quit
        Assert.True(vm.Update.ShowBanner);
        Assert.Equal(Texts.UpdateRestarting, vm.Update.BannerText);
        Assert.False(vm.Update.InstallCommand.CanExecute(null));
        Assert.False(vm.Update.CheckCommand.CanExecute(null));
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task WrongChecksum_ShowsTheProblem_ChangesNothing_AndDoesNotRestart()
    {
        Publish("1.0.0", checksum: new string('0', 64) + "  x.zip");
        MainViewModel vm = Create();
        Dictionary<string, string> before = _folders.Snapshot();

        await vm.Update.InstallAsync();

        Assert.Empty(_restarter.Calls);
        Assert.Equal(before, _folders.Snapshot());
        Assert.Equal(Texts.UpdateChecksumMismatch, vm.ErrorMessage);
        Assert.Equal(Texts.UpdateChecksumMismatch, vm.Update.StatusText);
        Assert.False(vm.Update.IsInstalling);
        Assert.True(vm.Update.InstallCommand.CanExecute(null)); // can be tried again
    }

    [Fact]
    public async Task MissingChecksum_Installs_AndTheNewInstanceShowsANote()
    {
        Publish("1.0.0", withChecksum: false);
        MainViewModel vm = Create();

        await vm.Update.InstallAsync();

        Assert.False(Assert.Single(_restarter.Calls).Verified);

        MainViewModel restarted = _h.Create(Service("1.0.0"), _restarter);
        restarted.Update.NoteJustUpdated(checksumVerified: false);
        Assert.Equal(Texts.UpdateDone("1.0.0") + " " + Texts.UpdateNoChecksumNote, restarted.StatusMessage);
        Assert.Equal(restarted.StatusMessage, restarted.Update.StatusText);
    }

    [Fact]
    public async Task LockedProgramFolder_IsRolledBack_AndExplained()
    {
        ReleaseInfo release = Packages.Release("1.0.0");
        _source.Releases.Add(release);
        _downloader.Serve(release, Packages.Zip(Packages.Standard()));
        var service = new UpdateService(_source, _downloader, SemVersion.Parse("1.0.0-rc3"), _folders.App,
            new UpdateInstaller(new FailingFileOperations { FailCopyTo = "NEW-FILE.txt", AccessDenied = true }), _folders.Temp);
        MainViewModel vm = _h.Create(service, _restarter);
        Dictionary<string, string> before = _folders.Snapshot();

        await vm.Update.InstallAsync();

        Assert.Equal(before, _folders.Snapshot());
        Assert.Equal(Texts.UpdateNoWriteAccess, vm.ErrorMessage);
        Assert.Empty(_restarter.Calls);
    }

    [Fact]
    public async Task FailedRestart_IsReported_AndTheProgramKeepsRunning()
    {
        Publish("1.0.0");
        _restarter.Failure = new InvalidOperationException("blocked");
        MainViewModel vm = Create();

        await vm.Update.InstallAsync();

        Assert.Equal(Texts.UpdateRestartFailed("blocked"), vm.ErrorMessage);
        Assert.False(vm.Update.IsInstalling);
        Assert.False(vm.Update.IsUpdateAvailable); // the new version is already on disk
        Assert.Equal("exe v2", _folders.ReadApp("EarSaxophone.exe"));
    }

    [Fact]
    public async Task Later_HidesTheBanner_UntilAnEvenNewerVersionAppears()
    {
        Publish("1.0.0");
        MainViewModel vm = Create();

        vm.Update.DismissCommand.Execute(null);
        Assert.False(vm.Update.ShowBanner);
        Assert.True(vm.Update.IsUpdateAvailable); // still offered behind the gear

        await vm.Update.CheckNowAsync(automatic: false);
        Assert.False(vm.Update.ShowBanner);

        Publish("1.0.1");
        await vm.Update.CheckNowAsync(automatic: false);
        Assert.True(vm.Update.ShowBanner);
        Assert.Equal("1.0.1", vm.Update.AvailableVersion);
    }

    [Fact]
    public async Task UnknownOwnVersion_HasNoUpdateService_AndSaysSo()
    {
        MainViewModel vm = _h.Create(updates: null, restarter: _restarter);
        vm.Tick();

        Assert.False(vm.Update.IsSupported);
        Assert.False(vm.Update.CheckCommand.CanExecute(null));
        await vm.Update.CheckNowAsync(automatic: false);
        Assert.Equal(Texts.UpdateNotAvailableHere, vm.Update.StatusText);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void OldSettingsFile_WithoutUpdateFields_LoadsWithUpdateCheckOn()
    {
        File.WriteAllText(_h.SettingsPath, "{ \"keepInTray\": false, \"showOsd\": false }");

        AppSettings loaded = new SettingsStore(_h.SettingsPath).Load();

        Assert.True(loaded.CheckForUpdates);
        Assert.Null(loaded.LastUpdateCheck);
        Assert.False(loaded.KeepInTray);
    }

    [Fact]
    public void UpdateFields_SurviveSaving()
    {
        var store = new SettingsStore(_h.SettingsPath);
        var time = new DateTimeOffset(2026, 10, 31, 8, 15, 0, TimeSpan.Zero);

        store.Save(new AppSettings(CheckForUpdates: false, LastUpdateCheck: time));

        Assert.Equal(new AppSettings(CheckForUpdates: false, LastUpdateCheck: time), store.Load());
        Assert.Contains("\"lastUpdateCheck\"", File.ReadAllText(_h.SettingsPath));
        Assert.NotEqual(new AppSettings(), new AppSettings(LastUpdateCheck: time));
    }
}
