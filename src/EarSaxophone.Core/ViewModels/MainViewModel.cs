using System.Collections.ObjectModel;
using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.Settings;

namespace EarSaxophone.Core.ViewModels;

/// <summary>
/// Everything the main window shows and does. It contains no WPF types, so it is tested without a display.
/// MIDI and action callbacks arrive on driver threads and are moved to the UI thread through <see cref="IUiThread"/>.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ProfileStore _store;
    private readonly MidiConnectionManager _midi;
    private readonly ActionExecutor _executor;
    private readonly IAudioSystem _audio;
    private readonly IUiThread _ui;
    private readonly SettingsStore _settingsStore;
    private readonly IAutostart _autostart;
    private readonly ControlRouter _router;
    private readonly MidiMessagePump _pump;
    private readonly List<Profile> _profiles;
    private readonly Dictionary<ControlId, ControllerElementViewModel> _elements = [];
    private int _active;
    private AppSettings _settings;
    private ControllerElementViewModel? _selected;
    private string _connectionText = Texts.UiSearchingDevice;
    private bool _isConnected;
    private bool _isLearning;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _errorFromAction;
    private string _profileNameEdit;

    public MainViewModel(
        ProfileStore store,
        MidiConnectionManager midi,
        ActionExecutor executor,
        IAudioSystem audio,
        IUiThread ui,
        SettingsStore settingsStore,
        IAutostart autostart)
    {
        _store = store;
        _midi = midi;
        _executor = executor;
        _audio = audio;
        _ui = ui;
        _settingsStore = settingsStore;
        _autostart = autostart;

        ProfileLoadResult loaded = store.Load();
        _profiles = loaded.Document.Profiles.Count > 0 ? loaded.Document.Profiles.ToList() : [new Profile("Default", [])];
        _active = Math.Max(0, _profiles.FindIndex(p => p.Name == loaded.Document.ActiveProfile));
        _profileNameEdit = Current.Name;
        if (loaded.Warning is not null)
        {
            ErrorMessage = loaded.Warning;
        }

        _settings = settingsStore.Load();
        bool autostartActual = SafeAutostartState();
        if (_settings.StartWithWindows != autostartActual)
        {
            _settings = _settings with { StartWithWindows = autostartActual };
        }

        foreach (Profile profile in _profiles)
        {
            ProfileNames.Add(profile.Name);
        }

        foreach (LayoutElement layout in Nanokontrol2Layout.Elements)
        {
            var element = new ControllerElementViewModel(layout);
            _elements[layout.Control] = element;
            Elements.Add(element);
        }

        _router = new ControlRouter(Current);
        _pump = new MidiMessagePump(_router.Handle, ex => _ui.Post(() => ShowError(Texts.ActionFailed(ex.Message))));
        _midi.MessageReceived += _pump.Post;
        _midi.StateChanged += OnConnectionChanged;
        _midi.Warning += OnMidiWarning;
        _router.MessageObserved += OnMessageObserved;
        _router.ActionRequested += OnActionRequested;
        _router.ControlLearned += OnControlLearned;

        LearnCommand = new RelayCommand(_ => StartLearning());
        CancelLearnCommand = new RelayCommand(_ => CancelLearning());
        SelectCommand = new RelayCommand(p => Select(p as ControllerElementViewModel));
        ApplyCommand = new RelayCommand(_ => ApplyEditor(), _ => _selected is not null);
        RemoveCommand = new RelayCommand(_ => RemoveSelectedMapping(), _ => _selected is { HasMapping: true });
        TestCommand = new RelayCommand(_ => TestEditor(), _ => _selected is not null);
        NewProfileCommand = new RelayCommand(_ => NewProfile());
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile());
        DismissErrorCommand = new RelayCommand(_ => ErrorMessage = null);
        RefreshAppsCommand = new RelayCommand(_ => RefreshApps());
        ReconnectCommand = new RelayCommand(_ => ReconnectDevice());

        RefreshSummaries();
        RefreshApps();
    }

    public ObservableCollection<string> ProfileNames { get; } = [];

    /// <summary>All controls of the device in the order of the layout (the picture binds to this).</summary>
    public ObservableCollection<ControllerElementViewModel> Elements { get; } = [];

    /// <summary>Controls that currently have an action, for the compact list below the picture.</summary>
    public ObservableCollection<ControllerElementViewModel> MappedElements { get; } = [];

    /// <summary>Controls found by MIDI-Learn that are not part of the standard layout.</summary>
    public ObservableCollection<ControllerElementViewModel> ExtraElements { get; } = [];

    public MappingEditorViewModel Editor { get; } = new();

    public RelayCommand LearnCommand { get; }

    public RelayCommand CancelLearnCommand { get; }

    public RelayCommand SelectCommand { get; }

    public RelayCommand ApplyCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand TestCommand { get; }

    public RelayCommand NewProfileCommand { get; }

    public RelayCommand DeleteProfileCommand { get; }

    public RelayCommand DismissErrorCommand { get; }

    public RelayCommand RefreshAppsCommand { get; }

    public RelayCommand ReconnectCommand { get; }

    public Profile Current => _profiles[_active];

    public ControllerElementViewModel? SelectedElement => _selected;

    public bool HasSelection => _selected is not null;

    public bool HasExtraElements => ExtraElements.Count > 0;

    public string ConnectionText
    {
        get => _connectionText;
        private set => SetField(ref _connectionText, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetField(ref _isConnected, value);
    }

    public bool IsLearning
    {
        get => _isLearning;
        private set
        {
            if (SetField(ref _isLearning, value))
            {
                OnPropertyChanged(nameof(LearnHint));
            }
        }
    }

    public string LearnHint => _isLearning ? Texts.UiLearnHint : string.Empty;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _errorMessage is not null;

    public string ActiveProfileName
    {
        get => Current.Name;
        set
        {
            int index = _profiles.FindIndex(p => p.Name == value);
            if (index >= 0 && index != _active)
            {
                SwitchProfile(index);
            }
        }
    }

    /// <summary>Editing this text renames the active profile when it is committed (focus lost / Enter).</summary>
    public string ProfileNameEdit
    {
        get => _profileNameEdit;
        set => RenameActive(value);
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set => ChangeStartWithWindows(value);
    }

    public bool KeepInTray
    {
        get => _settings.KeepInTray;
        set => ChangeSettings(_settings with { KeepInTray = value }, nameof(KeepInTray));
    }

    public bool StartMinimized
    {
        get => _settings.StartMinimized;
        set => ChangeSettings(_settings with { StartMinimized = value }, nameof(StartMinimized));
    }

    /// <summary>Called about once per second: finds, reconnects and checks the MIDI device.</summary>
    public void Tick() => _midi.Poll();

    /// <summary>Opens the MIDI device again (button, or return from sleep).</summary>
    public void ReconnectDevice() => _midi.Reconnect();

    /// <summary>Waits until all MIDI messages received so far were processed (tests).</summary>
    public bool WaitUntilIdle(TimeSpan timeout) => _pump.WaitUntilIdle(timeout);

    public void Dispose()
    {
        _midi.MessageReceived -= _pump.Post;
        _midi.StateChanged -= OnConnectionChanged;
        _midi.Warning -= OnMidiWarning;
        _pump.Dispose();
        _router.MessageObserved -= OnMessageObserved;
        _router.ActionRequested -= OnActionRequested;
        _router.ControlLearned -= OnControlLearned;
    }

    /// <summary>Shows a problem that was detected outside the view model (for example during startup).</summary>
    public void ShowError(string message)
    {
        ErrorMessage = message;
        _errorFromAction = false;
    }

    public void StartLearning()
    {
        _router.StartLearning();
        IsLearning = true;
    }

    public void CancelLearning()
    {
        _router.CancelLearning();
        IsLearning = false;
    }

    public void Select(ControllerElementViewModel? element)
    {
        if (_selected is not null)
        {
            _selected.IsSelected = false;
        }

        _selected = element;
        if (element is not null)
        {
            element.IsSelected = true;
            Editor.Load(element.Control, Current.Find(element.Control));
            RefreshApps();
        }

        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(HasSelection));
        ApplyCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
        TestCommand.RaiseCanExecuteChanged();
    }

    public void RefreshApps()
    {
        IEnumerable<string> names;
        try
        {
            names = _audio.GetSessions()
                .Where(s => !s.Info.IsSystemSounds && !string.IsNullOrWhiteSpace(s.Info.ExePath))
                .Select(s => AppMatcher.FileNameOf(s.Info.ExePath!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return;
        }

        Editor.AvailableApps.Clear();
        foreach (string name in names)
        {
            Editor.AvailableApps.Add(name);
        }
    }

    public void ApplyEditor()
    {
        if (_selected is null || !Editor.TryBuild(out ControlMapping? mapping, out _))
        {
            return;
        }

        ControllerElementViewModel element = _selected;
        _profiles[_active] = mapping is null ? Current.WithoutMapping(element.Control) : Current.WithMapping(mapping);
        if (Save())
        {
            _router.SetProfile(Current);
            RefreshSummaries();
            StatusMessage = mapping is null
                ? Texts.UiMappingRemoved(element.Label)
                : Texts.UiMappingSaved(element.Label, MappingDescriber.Describe(mapping));
            RemoveCommand.RaiseCanExecuteChanged();
        }
    }

    public void RemoveSelectedMapping()
    {
        if (_selected is null)
        {
            return;
        }

        ControllerElementViewModel element = _selected;
        _profiles[_active] = Current.WithoutMapping(element.Control);
        if (Save())
        {
            _router.SetProfile(Current);
            RefreshSummaries();
            Editor.Load(element.Control, null);
            StatusMessage = Texts.UiMappingRemoved(element.Label);
            RemoveCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Runs the action as currently entered once (volume actions at 50 %), without saving it.</summary>
    public void TestEditor()
    {
        if (_selected is null || !Editor.TryBuild(out ControlMapping? mapping, out _) || mapping is null)
        {
            return;
        }

        ActionResult result = _executor.Execute(new ActionInvocation(mapping, 0.5f));
        ShowResult(_selected.Label, result);
    }

    public void NewProfile()
    {
        int number = _profiles.Count + 1;
        while (_profiles.Any(p => p.Name == Texts.UiNewProfileName(number)))
        {
            number++;
        }

        _profiles.Add(new Profile(Texts.UiNewProfileName(number), []));
        ProfileNames.Add(_profiles[^1].Name);
        SwitchProfile(_profiles.Count - 1);
        Save();
    }

    public void DeleteProfile()
    {
        if (_profiles.Count <= 1)
        {
            ErrorMessage = Texts.UiLastProfile;
            _errorFromAction = false;
            return;
        }

        string removed = Current.Name;
        _profiles.RemoveAt(_active);
        ProfileNames.Remove(removed);
        _active = Math.Min(_active, _profiles.Count - 1);
        SwitchProfile(_active, force: true);
        Save();
    }

    public void RenameActive(string newName)
    {
        string name = (newName ?? string.Empty).Trim();
        if (name == Current.Name)
        {
            return;
        }

        string? problem = name.Length == 0
            ? Texts.UiProfileNameEmpty
            : _profiles.Any(p => p.Name == name) ? Texts.UiProfileNameTaken(name) : null;
        if (problem is not null)
        {
            ErrorMessage = problem;
            _errorFromAction = false;
            OnPropertyChanged(nameof(ProfileNameEdit));
            return;
        }

        int index = ProfileNames.IndexOf(Current.Name);
        _profiles[_active] = Current with { Name = name };
        if (index >= 0)
        {
            ProfileNames[index] = name;
        }

        _profileNameEdit = name;
        OnPropertyChanged(nameof(ProfileNameEdit));
        OnPropertyChanged(nameof(ActiveProfileName));
        Save();
    }

    private ControllerElementViewModel AddExtraElement(ControlId control, string label)
    {
        var element = new ControllerElementViewModel(control, label);
        _elements[control] = element;
        return element;
    }

    private void SwitchProfile(int index, bool force = false)
    {
        if (index == _active && !force)
        {
            return;
        }

        _active = index;
        _router.SetProfile(Current);
        _profileNameEdit = Current.Name;
        RefreshSummaries();
        if (_selected is not null)
        {
            Editor.Load(_selected.Control, Current.Find(_selected.Control));
        }

        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(ProfileNameEdit));
        RemoveCommand.RaiseCanExecuteChanged();
        Save();
    }

    private void RefreshSummaries()
    {
        foreach (ControllerElementViewModel element in _elements.Values)
        {
            ControlMapping? mapping = Current.Find(element.Control);
            element.HasMapping = mapping is not null;
            element.IsContinuous = mapping?.Action.IsContinuous ?? false;
            element.Summary = MappingDescriber.Describe(mapping);
        }

        MappedElements.Clear();
        foreach (ControllerElementViewModel element in Elements.Concat(ExtraElements).Where(e => e.HasMapping))
        {
            MappedElements.Add(element);
        }
    }

    private bool Save()
    {
        try
        {
            _store.Save(new ProfileDocument(ProfileDocument.CurrentSchemaVersion, Current.Name, _profiles.ToList()));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Texts.UiSaveFailed(ex.Message);
            _errorFromAction = false;
            return false;
        }
    }

    private void OnConnectionChanged(MidiConnectionState state, string detail) => _ui.Post(() =>
    {
        IsConnected = state == MidiConnectionState.Connected;
        ConnectionText = detail;
    });

    private void OnMidiWarning(string text) => _ui.Post(() => StatusMessage = text);

    private void OnMessageObserved(MidiMessage message) => _ui.Post(() =>
    {
        if (_elements.TryGetValue(message.Control, out ControllerElementViewModel? element))
        {
            element.LastValue = message.Value;
        }
    });

    private void OnControlLearned(ControlId control) => _ui.Post(() =>
    {
        IsLearning = false;
        if (!_elements.TryGetValue(control, out ControllerElementViewModel? element))
        {
            element = AddExtraElement(control, Texts.UiExtraControl(control.ToString()));
            ExtraElements.Add(element);
            OnPropertyChanged(nameof(HasExtraElements));
            ControlMapping? existing = Current.Find(control);
            element.HasMapping = existing is not null;
            element.IsContinuous = existing?.Action.IsContinuous ?? false;
            element.Summary = MappingDescriber.Describe(existing);
        }

        Select(element);
        StatusMessage = Texts.UiLearned(element.Label);
    });

    // Runs on the MIDI driver thread; only the result is passed on to the UI thread.
    private void OnActionRequested(ActionInvocation invocation)
    {
        ActionResult result = _executor.Execute(invocation);
        _ui.Post(() =>
        {
            string label = _elements.TryGetValue(invocation.Mapping.Control, out ControllerElementViewModel? element)
                ? element.Label
                : invocation.Mapping.Control.ToString();
            ShowResult(label, result);
        });
    }

    private void ShowResult(string label, ActionResult result)
    {
        if (result.IsFailure)
        {
            ErrorMessage = Texts.UiActionResultFor(label, result.Message);
            _errorFromAction = true;
            return;
        }

        StatusMessage = Texts.UiActionResultFor(label, result.Message);
        if (_errorFromAction)
        {
            ErrorMessage = null;
            _errorFromAction = false;
        }
    }

    private void ChangeStartWithWindows(bool enabled)
    {
        if (enabled == _settings.StartWithWindows)
        {
            return;
        }

        try
        {
            _autostart.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            ErrorMessage = Texts.UiAutostartFailed(ex.Message);
            _errorFromAction = false;
            OnPropertyChanged(nameof(StartWithWindows));
            return;
        }

        ChangeSettings(_settings with { StartWithWindows = enabled }, nameof(StartWithWindows));
    }

    private void ChangeSettings(AppSettings updated, string property)
    {
        if (updated == _settings)
        {
            return;
        }

        _settings = updated;
        OnPropertyChanged(property);
        try
        {
            _settingsStore.Save(updated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Texts.UiSettingsSaveFailed(ex.Message);
            _errorFromAction = false;
        }
    }

    private bool SafeAutostartState()
    {
        try
        {
            return _autostart.IsEnabled;
        }
        catch (Exception)
        {
            return _settings.StartWithWindows;
        }
    }
}
