using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.Updates;

namespace EarSaxophone.Core.ViewModels;

/// <summary>Starts the newly installed program and quits this one (Windows: <c>App</c>; tests and the CI demo record the call).</summary>
public interface IAppRestarter
{
    /// <summary>Throws when the new program could not be started; this one then keeps running.</summary>
    void Restart(string executable, bool checksumVerified);
}

/// <summary>
/// The update area behind the gear and the banner in the main window: manual and automatic check (in the background, at most
/// once per 24 h, switchable), "Jetzt aktualisieren" with progress and automatic restart. An unreachable release list
/// (private repository, offline) only changes the text behind the gear; it never shows the error bar.
/// All state changes happen on the UI thread (<see cref="IUiThread"/>).
/// </summary>
public sealed class UpdateViewModel : ObservableObject
{
    private readonly UpdateService? _service;
    private readonly IUiThread _ui;
    private readonly TimeProvider _time;
    private readonly Func<AppSettings> _settings;
    private readonly Action<AppSettings> _saveSettings;
    private readonly Action<string> _showError;
    private readonly Action<string> _showStatus;
    private readonly IAppRestarter? _restarter;
    private ReleaseInfo? _available;
    private SemVersion? _dismissed;
    private bool _isChecking;
    private bool _isInstalling;
    private double _progress;
    private string? _statusText;
    private int _shownPercent = -1;

    public UpdateViewModel(UpdateService? service, IUiThread ui, TimeProvider time, Func<AppSettings> settings,
        Action<AppSettings> saveSettings, Action<string> showError, Action<string> showStatus, IAppRestarter? restarter)
    {
        _service = service;
        _ui = ui;
        _time = time;
        _settings = settings;
        _saveSettings = saveSettings;
        _showError = showError;
        _showStatus = showStatus;
        _restarter = restarter;
        CheckCommand = new RelayCommand(_ => _ = CheckNowAsync(automatic: false), _ => IsSupported && !IsBusy);
        InstallCommand = new RelayCommand(_ => _ = InstallAsync(), _ => CanInstall);
        DismissCommand = new RelayCommand(_ => Dismiss(), _ => !IsBusy);
    }

    public RelayCommand CheckCommand { get; }

    public RelayCommand InstallCommand { get; }

    /// <summary>Hides the banner for this version ("Später"); the gear keeps offering it.</summary>
    public RelayCommand DismissCommand { get; }

    public bool IsSupported => _service is not null;

    public string InstalledVersionText => Texts.UpdateInstalledVersion(_service?.CurrentVersion.ToString() ?? "?");

    /// <summary>Setting "Beim Start nach Updates suchen" (default on).</summary>
    public bool CheckOnStart
    {
        get => _settings().CheckForUpdates;
        set
        {
            if (value != _settings().CheckForUpdates)
            {
                _saveSettings(_settings() with { CheckForUpdates = value });
                OnPropertyChanged();
            }
        }
    }

    public bool IsChecking
    {
        get => _isChecking;
        private set
        {
            if (SetField(ref _isChecking, value))
            {
                RaiseState();
            }
        }
    }

    /// <summary>Download, check and install are running (and the restart was requested).</summary>
    public bool IsInstalling
    {
        get => _isInstalling;
        private set
        {
            if (SetField(ref _isInstalling, value))
            {
                RaiseState();
            }
        }
    }

    public bool IsBusy => _isChecking || _isInstalling;

    /// <summary>0..1 while installing.</summary>
    public double Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    public ReleaseInfo? AvailableRelease => _available;

    public bool IsUpdateAvailable => _available is not null;

    public string? AvailableVersion => _available?.Version.ToString();

    public bool CanInstall => _available is not null && !IsBusy && _restarter is not null;

    /// <summary>Result of the last check or step, shown behind the gear.</summary>
    public string? StatusText
    {
        get => _statusText;
        private set
        {
            if (SetField(ref _statusText, value))
            {
                OnPropertyChanged(nameof(HasStatusText));
                OnPropertyChanged(nameof(BannerText));
            }
        }
    }

    public bool HasStatusText => !string.IsNullOrEmpty(_statusText);

    /// <summary>The banner in the main window: a newer version that was not put off, or an update in progress.</summary>
    public bool ShowBanner => _isInstalling || (_available is not null && !Equals(_dismissed, _available.Version));

    public string BannerText => _isInstalling
        ? _statusText ?? Texts.UpdateInstalling
        : _available is null ? string.Empty : Texts.UpdateAvailable(_available.Version.ToString());

    /// <summary>Starts the automatic check when it is switched on and due (UI thread; called about once per second).</summary>
    public void CheckIfDue()
    {
        if (_service is null || IsBusy || !_settings().CheckForUpdates)
        {
            return;
        }

        if (UpdateService.IsAutomaticCheckDue(_settings().LastUpdateCheck, _time.GetUtcNow()))
        {
            _ = CheckNowAsync(automatic: true);
        }
    }

    /// <summary>Looks for a newer release (UI thread). Never throws; problems end up in <see cref="StatusText"/>.</summary>
    public async Task CheckNowAsync(bool automatic)
    {
        if (_service is null)
        {
            StatusText = Texts.UpdateNotAvailableHere;
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsChecking = true;
        if (!automatic)
        {
            StatusText = Texts.UpdateChecking;
        }

        _saveSettings(_settings() with { LastUpdateCheck = _time.GetUtcNow() });
        UpdateCheckResult result;
        try
        {
            result = await _service.CheckAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            result = new UpdateCheckResult(UpdateCheckStatus.NotReachable, Problem: UpdateSourceProblem.Failed);
        }

        var done = new TaskCompletionSource();
        _ui.Post(() =>
        {
            ShowCheckResult(result);
            IsChecking = false;
            done.TrySetResult();
        });
        await done.Task.ConfigureAwait(false);
    }

    /// <summary>Downloads and installs the available release, then asks for the restart (UI thread). Never throws.</summary>
    public async Task InstallAsync()
    {
        if (!CanInstall || _service is null || _available is not { } release)
        {
            return;
        }

        IsInstalling = true;
        Progress = 0;
        _shownPercent = -1;
        StatusText = Texts.UpdateDownloading(release.Version.ToString(), 0);
        var progress = new UiProgress(this, release.Version.ToString());
        Action finish;
        try
        {
            UpdateInstallResult result = await _service.InstallAsync(release, progress).ConfigureAwait(false);
            finish = () => Restart(result);
        }
        catch (UpdateException ex)
        {
            string message = Describe(ex);
            finish = () => Fail(message);
        }
        catch (Exception ex)
        {
            string message = Texts.UpdateInstallFailed(ex.Message);
            finish = () => Fail(message);
        }

        var done = new TaskCompletionSource();
        _ui.Post(() =>
        {
            finish();
            done.TrySetResult();
        });
        await done.Task.ConfigureAwait(false);
    }

    /// <summary>The program was just restarted by an update (command line of the new instance).</summary>
    public void NoteJustUpdated(bool checksumVerified)
    {
        string text = Texts.UpdateDone(_service?.CurrentVersion.ToString() ?? "?");
        if (!checksumVerified)
        {
            text += " " + Texts.UpdateNoChecksumNote;
        }

        StatusText = text;
        _showStatus(text);
    }

    internal static string Describe(UpdateException ex) => ex.Failure switch
    {
        UpdateFailure.Download => Texts.UpdateDownloadFailed(ex.Message),
        UpdateFailure.ChecksumMismatch => Texts.UpdateChecksumMismatch,
        UpdateFailure.InvalidPackage => Texts.UpdateInvalidPackage,
        _ when !ex.RolledBack => Texts.UpdateRollbackIncomplete(ex.Message),
        _ when ex.AccessDenied => Texts.UpdateNoWriteAccess,
        _ => Texts.UpdateInstallFailed(ex.Message),
    };

    private void ShowCheckResult(UpdateCheckResult result)
    {
        string current = _service!.CurrentVersion.ToString();
        switch (result.Status)
        {
            case UpdateCheckStatus.Available:
                SetAvailable(result.Release);
                StatusText = Texts.UpdateAvailable(result.Release!.Version.ToString());
                break;
            case UpdateCheckStatus.UpToDate:
                SetAvailable(null);
                StatusText = Texts.UpdateUpToDate(current);
                break;
            default:
                // Keep an already found version: a later failed check does not make it go away.
                StatusText = result.Problem switch
                {
                    UpdateSourceProblem.RateLimited => Texts.UpdateRateLimited,
                    UpdateSourceProblem.Failed => Texts.UpdateUnexpectedAnswer,
                    _ => Texts.UpdateNotReachable,
                };
                break;
        }
    }

    private void SetAvailable(ReleaseInfo? release)
    {
        _available = release;
        OnPropertyChanged(nameof(AvailableRelease));
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(AvailableVersion));
        RaiseState();
    }

    private void Restart(UpdateInstallResult result)
    {
        Progress = 1;
        StatusText = Texts.UpdateRestarting;
        try
        {
            _restarter!.Restart(result.Executable, result.ChecksumVerified);
        }
        catch (Exception ex)
        {
            // The new version is on disk; only the restart is missing.
            SetAvailable(null);
            IsInstalling = false;
            Fail(Texts.UpdateRestartFailed(ex.Message));
        }
    }

    private void Fail(string message)
    {
        IsInstalling = false;
        Progress = 0;
        StatusText = message;
        _showError(message);
    }

    private void Dismiss()
    {
        _dismissed = _available?.Version;
        OnPropertyChanged(nameof(ShowBanner));
    }

    private void RaiseState()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(ShowBanner));
        OnPropertyChanged(nameof(BannerText));
        CheckCommand.RaiseCanExecuteChanged();
        InstallCommand.RaiseCanExecuteChanged();
        DismissCommand.RaiseCanExecuteChanged();
    }

    private void ShowProgress(double value, string version)
    {
        Progress = value;
        int percent = (int)Math.Round(Math.Min(1.0, value / 0.9) * 100);
        StatusText = value >= 0.95 ? Texts.UpdateInstalling : Texts.UpdateDownloading(version, percent);
    }

    /// <summary>Passes progress to the UI thread, at most once per whole percent.</summary>
    private sealed class UiProgress(UpdateViewModel owner, string version) : IProgress<double>
    {
        public void Report(double value)
        {
            int percent = (int)(value * 100);
            if (Interlocked.Exchange(ref owner._shownPercent, percent) == percent)
            {
                return;
            }

            owner._ui.Post(() => owner.ShowProgress(value, version));
        }
    }
}
