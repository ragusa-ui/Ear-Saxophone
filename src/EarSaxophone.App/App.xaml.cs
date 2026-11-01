using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;
using EarSaxophone.Windows.Audio;
using EarSaxophone.Windows.Input;
using EarSaxophone.Windows.Midi;
using EarSaxophone.Windows.Startup;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Wpf.Ui.Appearance;

namespace EarSaxophone.App;

public partial class App : Application
{
    private const string MutexName = @"Local\EarSaxophone.SingleInstance";
    private const string ActivateEventName = @"Local\EarSaxophone.Activate";

    private Mutex? _singleInstance;
    private EventWaitHandle? _activate;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private OsdWindow? _osd;
    private TaskbarIcon? _tray;
    private System.Threading.Timer? _timer;
    private MidiConnectionManager? _midi;
    private IDisposable? _audioToDispose;
    private string _dataFolder = AppContext.BaseDirectory;
    private int _ticking;

    public static App Instance => (App)Current;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string? screenshotPath = ArgumentValue(e.Args, "--demo-screenshot");
        bool demo = screenshotPath is not null;

        if (!demo && !AcquireSingleInstance())
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogError(args.ExceptionObject as Exception ?? new InvalidOperationException(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError(args.Exception);
            args.SetObserved();
        };
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        _dataFolder = demo
            ? Path.Combine(Path.GetTempPath(), "earsax-demo-" + Guid.NewGuid().ToString("N"))
            : DataLocation.Resolve(AppContext.BaseDirectory);
        Directory.CreateDirectory(_dataFolder);
        var profileStore = new ProfileStore(Path.Combine(_dataFolder, "profiles.json"));
        if (demo)
        {
            profileStore.Save(DemoData.Create());
        }

        IAudioSystem audio;
        string? audioProblem = null;
        try
        {
            var real = new NAudioAudioSystem();
            _audioToDispose = real;
            audio = real;
            audioProblem = real.Problem; // the system keeps retrying by itself; the hint explains why audio actions may fail meanwhile
        }
        catch (Exception ex)
        {
            audio = new UnavailableAudioSystem();
            audioProblem = Texts.AudioUnavailable(ex.Message);
        }

        _midi = new MidiConnectionManager(new NAudioMidiInputProvider());
        var executor = new ActionExecutor(new AudioController(audio), new SendInputKeyboardSender());
        IAutostart autostart = demo ? new MemoryAutostart() : CreateAutostart();
        _viewModel = new MainViewModel(profileStore, _midi, executor, audio, new DispatcherUiThread(Dispatcher),
            new SettingsStore(Path.Combine(_dataFolder, "settings.json")), autostart);
        if (audioProblem is not null)
        {
            _viewModel.ShowError(audioProblem);
        }

        _window = new MainWindow(_viewModel);
        _osd = new OsdWindow();
        _viewModel.OsdRequested += message => _osd.Present(message, _viewModel.OsdPosition, TimeSpan.FromSeconds(1.8));
        _viewModel.ProgramPromptRequested += prompt => new ProgramPromptWindow(prompt).Present();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.ShowOsd) && !_viewModel.ShowOsd)
            {
                _osd.HideNow();
            }
        };
        _timer = new System.Threading.Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        if (demo)
        {
            RunDemoScreenshot(screenshotPath!);
            return;
        }

        CreateTrayIcon();
        bool startHidden = e.Args.Contains("--minimized") || _viewModel.StartMinimized;
        if (startHidden && _viewModel.KeepInTray)
        {
            return; // lives in the tray until the user opens it
        }

        if (startHidden)
        {
            _window.WindowState = WindowState.Minimized;
        }

        _window.Show();
    }

    /// <summary>Quits the program for real (tray menu, or closing the window when "stay in tray" is off).</summary>
    public void Quit()
    {
        if (IsExiting)
        {
            return;
        }

        IsExiting = true;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _timer?.Dispose();
        _tray?.Dispose();
        _viewModel?.Dispose();
        _midi?.Dispose();
        _audioToDispose?.Dispose();
        _singleInstance?.Dispose();
        Shutdown();
    }

    // After sleep the open MIDI handle can belong to the old USB instance although the device name is still listed.
    private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode != Microsoft.Win32.PowerModes.Resume)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false); // let USB re-enumerate first
            try
            {
                _viewModel?.ReconnectDevice();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        });
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private bool AcquireSingleInstance()
    {
        _singleInstance = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            try
            {
                using EventWaitHandle existing = EventWaitHandle.OpenExisting(ActivateEventName);
                existing.Set();
            }
            catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
            {
                // The first instance has not finished starting; nothing to wake up.
            }

            return false;
        }

        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        var listener = new Thread(() =>
        {
            while (_activate.WaitOne())
            {
                Dispatcher.BeginInvoke(() => _window?.ShowAndActivate());
            }
        })
        { IsBackground = true, Name = "ActivateListener" };
        listener.Start();
        return true;
    }

    private static IAutostart CreateAutostart()
    {
        var autostart = new RegistryAutostart(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "EarSaxophone.exe"));
        try
        {
            autostart.Refresh(); // repairs the entry when the program folder was moved
        }
        catch (Exception)
        {
            // Not worth blocking startup; the settings switch reports real problems.
        }

        return autostart;
    }

    private void Tick()
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1)
        {
            return;
        }

        try
        {
            _viewModel?.Tick();
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _ticking, 0);
        }
    }

    private void CreateTrayIcon()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var open = new System.Windows.Controls.MenuItem { Header = Texts.TrayOpen };
        open.Click += (_, _) => _window?.ShowAndActivate();
        var exit = new System.Windows.Controls.MenuItem { Header = Texts.TrayExit };
        exit.Click += (_, _) => Quit();
        menu.Items.Add(open);
        menu.Items.Add(exit);

        _tray = new TaskbarIcon
        {
            ToolTipText = Texts.TrayTooltip(_viewModel!.ConnectionText),
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = menu,
            MenuActivation = PopupActivationMode.RightClick,
        };
        _tray.TrayMouseDoubleClick += (_, _) => _window?.ShowAndActivate();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.ConnectionText) && _tray is not null)
            {
                string text = Texts.TrayTooltip(_viewModel.ConnectionText);
                _tray.ToolTipText = text.Length > 120 ? text[..120] : text;
            }
        };
        _tray.ForceCreate();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        _viewModel?.ShowError(Texts.UnexpectedError(e.Exception.Message));
        e.Handled = true;
    }

    private void LogError(Exception ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(_dataFolder, "error.log"), $"{DateTime.Now:s} {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging must never cause another failure.
        }
    }

    /// <summary>CI only: shows the window with sample data, saves it as PNG and quits. Proves the UI starts and renders.</summary>
    private void RunDemoScreenshot(string path)
    {
        var window = _window!;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = 0;
        window.Top = 0;
        window.Width = 1280;
        window.Height = 860;
        window.Show();
        DemoData.PrepareScreenshot(_viewModel!);
        var wait = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        wait.Tick += (_, _) =>
        {
            wait.Stop();
            int exitCode = 0;
            try
            {
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream stream = File.Create(path);
                encoder.Save(stream);
                stream.Close();
                File.WriteAllText(path + ".report.txt", UiReport.Build(window, bitmap));

                // Operate the window like a user would and record what happened.
                var checks = UiSelfCheck.Run(window, _viewModel!, _osd!);
                var lines = checks.Select(c => $"{(c.Ok ? "ok" : "FAILED")} {c.Name} [{c.Detail}]");
                File.AppendAllText(path + ".report.txt", Environment.NewLine + "== Interaction checks" + Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine);
                if (checks.Any(c => !c.Ok))
                {
                    exitCode = 1;
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(path + ".error.txt", ex.ToString());
                exitCode = 1;
            }

            Quit();
            Environment.Exit(exitCode);
        };
        wait.Start();
    }

    private sealed class MemoryAutostart : IAutostart
    {
        public bool IsEnabled { get; private set; }

        public void SetEnabled(bool enabled) => IsEnabled = enabled;
    }
}
