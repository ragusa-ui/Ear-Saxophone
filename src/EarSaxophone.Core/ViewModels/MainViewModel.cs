using System.Collections.ObjectModel;
using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.Updates;

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
    private readonly ControlShapeTracker _shapes = new(Nanokontrol2Layout.ShapeOf);
    private readonly DeviceGestureDetector _gestures = new();
    private readonly TimeProvider _time;
    private readonly ProgramPromptService _programs;
    private readonly LedController? _leds;
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
    private bool _followDevice = true;
    private ControllerElementViewModel? _pending;
    private ProgramPromptViewModel? _prompt;
    private DateTimeOffset _promptShownAt;
    private DateTimeOffset _blinkStart;
    private ITimer? _blinkTimer;
    private bool _disposed;

    public MainViewModel(
        ProfileStore store,
        MidiConnectionManager midi,
        ActionExecutor executor,
        IAudioSystem audio,
        IUiThread ui,
        SettingsStore settingsStore,
        IAutostart autostart,
        TimeProvider? time = null,
        ProgramPromptService? programs = null,
        UpdateService? updates = null,
        IAppRestarter? restarter = null,
        IMidiOutputProvider? midiOutput = null)
    {
        _store = store;
        _midi = midi;
        _executor = executor;
        _audio = audio;
        _ui = ui;
        _settingsStore = settingsStore;
        _autostart = autostart;
        _time = time ?? TimeProvider.System;
        _programs = programs ?? new ProgramPromptService(audio, _time,
            Path.GetFileName(Environment.ProcessPath) ?? "EarSaxophone.exe", Environment.ProcessId);

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

        if (midiOutput is not null)
        {
            _leds = new LedController(new MidiOutputConnection(midiOutput, LedOptions.Default.OutputPortFragment), executor.Audio.ReadGroupMuted,
                LedOptions.Default, _time) { Enabled = _settings.LedFeedback, Blink = _settings.LedBlink };
            _leds.SetProfile(Current);
            _leds.StatusChanged += OnLedStatus;
        }

        _router = new ControlRouter(Current);
        _pump = new MidiMessagePump(_router.Handle, ex => _ui.Post(() => ShowError(Texts.ActionFailed(ex.Message))));
        _midi.MessageReceived += OnMidiMessage;
        _midi.StateChanged += OnConnectionChanged;
        _midi.Warning += OnMidiWarning;
        _router.MessageObserved += OnMessageObserved;
        _router.ActionsRequested += OnActionsRequested;
        _router.ControlLearned += OnControlLearned;

        LearnCommand = new RelayCommand(_ => StartLearning());
        CancelLearnCommand = new RelayCommand(_ => CancelLearning());
        SelectCommand = new RelayCommand(p => Select(p as ControllerElementViewModel));
        ApplyCommand = new RelayCommand(_ => ApplyEditor(), _ => _selected is not null && Editor.CanApply);
        DiscardCommand = new RelayCommand(_ => DiscardEditor(), _ => _selected is not null && Editor.IsDirty);
        RemoveCommand = new RelayCommand(_ => RemoveSelectedMapping(), _ => _selected is { HasMapping: true });
        SelectPendingCommand = new RelayCommand(_ => Select(_pending), _ => _pending is not null);
        ResetIgnoredProgramsCommand = new RelayCommand(_ => ResetIgnoredPrograms());
        Editor.PropertyChanged += OnEditorChanged;
        NewProfileCommand = new RelayCommand(_ => NewProfile());
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile());
        DismissErrorCommand = new RelayCommand(_ => ErrorMessage = null);
        RefreshAppsCommand = new RelayCommand(_ => RefreshApps());
        TestOsdCommand = new RelayCommand(_ => OsdRequested?.Invoke(new OsdMessage(Texts.TargetSpeakersShort, null, 0.65)));
        ReconnectCommand = new RelayCommand(_ => ReconnectDevice());

        Update = new UpdateViewModel(updates, ui, _time, () => _settings, s => ChangeSettings(s, null), ShowError,
            text => StatusMessage = text, restarter);

        RefreshSummaries();
        RefreshApps();
    }

    /// <summary>Update check and installation (gear and banner).</summary>
    public UpdateViewModel Update { get; }

    public ObservableCollection<string> ProfileNames { get; } = [];

    /// <summary>All controls of the device in the order of the layout (the picture binds to this).</summary>
    public ObservableCollection<ControllerElementViewModel> Elements { get; } = [];

    /// <summary>Controls found by MIDI-Learn that are not part of the standard layout.</summary>
    public ObservableCollection<ControllerElementViewModel> ExtraElements { get; } = [];

    public MappingEditorViewModel Editor { get; } = new();

    public RelayCommand LearnCommand { get; }

    public RelayCommand CancelLearnCommand { get; }

    public RelayCommand SelectCommand { get; }

    public RelayCommand ApplyCommand { get; }

    /// <summary>Throws the unsaved changes of the editor away.</summary>
    public RelayCommand DiscardCommand { get; }

    public RelayCommand RemoveCommand { get; }

    /// <summary>Selects the control that was operated at the device while the editor had unsaved changes.</summary>
    public RelayCommand SelectPendingCommand { get; }

    public RelayCommand ResetIgnoredProgramsCommand { get; }

    public RelayCommand NewProfileCommand { get; }

    public RelayCommand DeleteProfileCommand { get; }

    public RelayCommand DismissErrorCommand { get; }

    public RelayCommand RefreshAppsCommand { get; }

    public RelayCommand ReconnectCommand { get; }

    public RelayCommand TestOsdCommand { get; }

    /// <summary>Raised on the UI thread when the on-screen display should show something (only when the display is switched on).</summary>
    public event Action<OsdMessage>? OsdRequested;

    /// <summary>Raised on the UI thread when a new program plays sound and should be put on a control (design V2, section 6).</summary>
    public event Action<ProgramPromptViewModel>? ProgramPromptRequested;

    /// <summary>
    /// The selection follows the device (default): operating a control at the device selects it. Off = the selection is
    /// pinned and only changes by click.
    /// </summary>
    public bool FollowDevice
    {
        get => _followDevice;
        set
        {
            if (SetField(ref _followDevice, value))
            {
                SetPending(null);
            }
        }
    }

    /// <summary>A control that was operated at the device while the editor had unsaved changes (the selection did not jump).</summary>
    public ControllerElementViewModel? PendingElement => _pending;

    public bool HasPendingSelection => _pending is not null;

    /// <summary>The open question for a new program, if any.</summary>
    public ProgramPromptViewModel? ActivePrompt
    {
        get => _prompt;
        private set => SetField(ref _prompt, value);
    }

    /// <summary>Setting "Bei neuem Programm mit Ton fragen".</summary>
    public bool AskForNewPrograms
    {
        get => _settings.AskForNewPrograms;
        set => ChangeSettings(_settings with { AskForNewPrograms = value }, nameof(AskForNewPrograms));
    }

    /// <summary>Setting: a short system sound when the question for a new program appears (default on).</summary>
    public bool PromptSound
    {
        get => _settings.PromptSound;
        set => ChangeSettings(_settings with { PromptSound = value }, nameof(PromptSound));
    }

    /// <summary>Programs that are never asked about ("Nie fragen").</summary>
    public IReadOnlyList<string> IgnoredPrograms => _settings.IgnoredProgramList;

    public bool HasIgnoredPrograms => _settings.IgnoredProgramList.Count > 0;

    public IReadOnlyList<Option<OsdPosition>> OsdPositionOptions { get; } =
        Enum.GetValues<OsdPosition>().Select(p => new Option<OsdPosition>(Texts.OsdPositionName(p), p)).ToList();

    public IReadOnlyList<Option<AppStyle>> StyleOptions { get; } =
        Enum.GetValues<AppStyle>().Select(s => new Option<AppStyle>(Texts.StyleName(s), s)).ToList();

    /// <summary>Setting: visual style of the window (switchable in the gear menu).</summary>
    public AppStyle Style
    {
        get => _settings.Style;
        set => ChangeSettings(_settings with { Style = value }, nameof(Style));
    }

    public bool ShowOsd
    {
        get => _settings.ShowOsd;
        set => ChangeSettings(_settings with { ShowOsd = value }, nameof(ShowOsd));
    }

    public OsdPosition OsdPosition
    {
        get => _settings.OsdPosition;
        set => ChangeSettings(_settings with { OsdPosition = value }, nameof(OsdPosition));
    }

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
                if (!LeaveEditor())
                {
                    OnPropertyChanged();
                    return;
                }

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

    /// <summary>Setting "LEDs am Gerät anzeigen": keys with a mute action light up while their target is muted.</summary>
    public bool LedFeedback
    {
        get => _settings.LedFeedback;
        set
        {
            ChangeSettings(_settings with { LedFeedback = value }, nameof(LedFeedback));
            if (_leds is not null)
            {
                _leds.Enabled = _settings.LedFeedback;
            }
        }
    }

    /// <summary>
    /// Setting "LEDs blinken bei Stumm" (default on): muted keys blink on the device and in the picture instead of a steady light.
    /// </summary>
    public bool LedBlink
    {
        get => _settings.LedBlink;
        set
        {
            ChangeSettings(_settings with { LedBlink = value }, nameof(LedBlink));
            if (_leds is not null)
            {
                _leds.Blink = _settings.LedBlink;
            }

            _blinkStart = _time.GetUtcNow();
            UpdatePictureBlink();
        }
    }

    /// <summary>Shape of a control: from the device layout, for other controls what their values have shown so far.</summary>
    public ControlShape ShapeOf(ControlId control) => _shapes.ShapeOf(control);

    /// <summary>
    /// Called about once per second (any thread): finds, reconnects and checks the MIDI device, looks for new programs with
    /// sound and starts the automatic update check when it is due (at most once per 24 h).
    /// </summary>
    public void Tick()
    {
        _midi.Poll();
        PollLeds();
        _ui.Post(RefreshMuteStates);
        _ui.Post(CheckForNewPrograms);
        _ui.Post(Update.CheckIfDue);
    }

    /// <summary>
    /// Mirrors the mute state on the LED areas of the keys in the picture (UI thread; called by <see cref="Tick"/>, after key actions
    /// and after mapping changes): a key is lit when its mute target (group) is muted, the same rule as the LEDs on the device. An
    /// unreadable state leaves the key as it is.
    /// </summary>
    public void RefreshMuteStates()
    {
        Profile profile = Current;
        bool newlyLit = false;
        foreach (ControllerElementViewModel element in Elements)
        {
            if (!element.HasLed)
            {
                continue;
            }

            try
            {
                bool lit = LedController.IsLit(profile, element.Control, _executor.Audio.ReadGroupMuted);
                newlyLit |= lit && !element.IsMuteLit;
                element.IsMuteLit = lit;
            }
            catch (Exception)
            {
                // State unreadable right now (audio device busy or gone): keep what the picture shows.
            }
        }

        if (newlyLit)
        {
            _blinkStart = _time.GetUtcNow(); // like the device: the blinking restarts with the on phase, so a press shows at once
        }

        UpdatePictureBlink();
    }

    /// <summary>
    /// Sets the blink phase of the lit LEDs in the picture (UI thread): with "LEDs blinken bei Stumm" all muted-lit keys are dark
    /// together in every second phase of <see cref="BlinkPhase.DefaultHalfPeriod"/> (<see cref="ControllerElementViewModel.IsLedBlinkOff"/>).
    /// A one-shot timer brings the next phase change; it only runs while something blinks.
    /// </summary>
    public void UpdatePictureBlink()
    {
        if (_disposed)
        {
            return;
        }

        DateTimeOffset now = _time.GetUtcNow();
        TimeSpan half = BlinkPhase.DefaultHalfPeriod;
        bool anyLit = Elements.Any(e => e.IsMuteLit);
        bool blinking = LedBlink && anyLit;
        bool dark = blinking && !BlinkPhase.IsOn(_blinkStart, now, half);
        foreach (ControllerElementViewModel element in Elements)
        {
            element.IsLedBlinkOff = dark && element.IsMuteLit;
        }

        if (!blinking)
        {
            _blinkTimer?.Dispose();
            _blinkTimer = null;
            return;
        }

        TimeSpan due = BlinkPhase.UntilNextChange(_blinkStart, now, half);
        if (_blinkTimer is null)
        {
            _blinkTimer = _time.CreateTimer(_ => _ui.Post(UpdatePictureBlink), null, due, Timeout.InfiniteTimeSpan);
        }
        else
        {
            _blinkTimer.Change(due, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Opens the MIDI device again (button, or return from sleep).</summary>
    public void ReconnectDevice() => _midi.Reconnect();

    /// <summary>Waits until all MIDI messages received so far were processed (tests).</summary>
    public bool WaitUntilIdle(TimeSpan timeout) => _pump.WaitUntilIdle(timeout);

    public void Dispose()
    {
        _disposed = true;
        _blinkTimer?.Dispose();
        _blinkTimer = null;
        _midi.MessageReceived -= OnMidiMessage;
        if (_leds is not null)
        {
            _leds.StatusChanged -= OnLedStatus;
            _leds.Dispose();
        }

        _midi.StateChanged -= OnConnectionChanged;
        _midi.Warning -= OnMidiWarning;
        _pump.Dispose();
        _router.MessageObserved -= OnMessageObserved;
        _router.ActionsRequested -= OnActionsRequested;
        _router.ControlLearned -= OnControlLearned;
        Editor.PropertyChanged -= OnEditorChanged;
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

    /// <summary>
    /// Selects a control (click, or the device when it is followed). Unsaved valid changes of the editor are applied first;
    /// unsaved invalid ones are thrown away with a note in the status line, so nothing is lost silently. Selecting the
    /// control that is already selected changes nothing (the editor keeps its input).
    /// </summary>
    public void Select(ControllerElementViewModel? element)
    {
        if (element == _selected)
        {
            SetPending(null);
            return;
        }

        if (!LeaveEditor())
        {
            return;
        }

        ShowSelection(element);
    }

    private void ShowSelection(ControllerElementViewModel? element)
    {
        if (_selected is not null)
        {
            _selected.IsSelected = false;
        }

        _selected = element;
        SetPending(null);
        if (element is not null)
        {
            element.IsSelected = true;
            LoadEditor(element);
            RefreshApps();
        }

        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(HasSelection));
        RaiseEditorCommands();
    }

    public void RefreshApps()
    {
        List<ProgramInfo> programs;
        try
        {
            programs = _audio.GetSessions()
                .Where(s => !s.Info.IsSystemSounds && !string.IsNullOrWhiteSpace(s.Info.ExePath))
                .Select(s => new ProgramInfo(AppMatcher.FileNameOf(s.Info.ExePath!), s.Info.ExePath))
                .DistinctBy(p => p.ExeName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p.ExeName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return;
        }

        Editor.SetAvailablePrograms(programs);

        try
        {
            Editor.SetAvailableDevices(
                new[] { DeviceFlow.Render, DeviceFlow.Capture }
                    .SelectMany(flow => _audio.GetEndpoints(flow).Select(e => (e.Id, e.Name, flow))));
        }
        catch (Exception)
        {
            // Devices are only suggestions; the list stays as it was.
        }
    }

    /// <summary>Saves the editor input for the selected control ("Übernehmen", also Enter). Invalid input stays with its message.</summary>
    public void ApplyEditor() => ApplyEditor(automatic: false);

    /// <summary>Throws the unsaved changes away and shows the saved mapping again ("Verwerfen").</summary>
    public void DiscardEditor()
    {
        if (_selected is null || !Editor.IsDirty)
        {
            return;
        }

        Editor.Revert();
        SetPending(null);
        StatusMessage = Texts.UiChangesReverted(_selected.Label);
        RaiseEditorCommands();
    }

    public void RemoveSelectedMapping()
    {
        if (_selected is null)
        {
            return;
        }

        ControllerElementViewModel element = _selected;
        if (ChangeProfile(Current.WithoutMapping(element.Control)))
        {
            LoadEditor(element);
            SetPending(null);
            StatusMessage = Texts.UiMappingRemoved(element.Label);
            RaiseEditorCommands();
        }
    }

    /// <summary>
    /// Drag and drop in the picture: moves the mapping of one control onto another (a mapped target swaps), or copies it
    /// (<paramref name="copy"/>, Ctrl held). Mappings that do not fit the kind of control are refused without change.
    /// Unsaved editor changes are applied (or, when invalid, thrown away) first. Returns the text for the status line,
    /// which is also shown there.
    /// </summary>
    public string TransferMapping(ControlId from, ControlId to, bool copy)
    {
        string fromLabel = LabelOf(from);
        string toLabel = LabelOf(to);
        if (!LeaveEditor())
        {
            return ErrorMessage ?? string.Empty;
        }

        TransferResult result = Current.TransferMapping(from, to, copy ? TransferMode.Copy : TransferMode.Move, _shapes.ShapeOf);
        string message = result.Outcome switch
        {
            TransferOutcome.Moved => Texts.UiTransferMoved(fromLabel, toLabel),
            TransferOutcome.Swapped => Texts.UiTransferSwapped(fromLabel, toLabel),
            TransferOutcome.Copied => Texts.UiTransferCopied(fromLabel, toLabel),
            TransferOutcome.Incompatible => Texts.UiTransferIncompatible(fromLabel, toLabel),
            _ => Texts.UiTransferNothing(fromLabel),
        };

        if (result.Changed && !ChangeProfile(result.Profile))
        {
            return ErrorMessage ?? string.Empty;
        }

        if (result.Changed)
        {
            // The dropped mapping is selected at its new place, so it can be checked at once.
            if (_elements.TryGetValue(to, out ControllerElementViewModel? target) && target != _selected)
            {
                ShowSelection(target);
            }
            else if (_selected is { } selected)
            {
                LoadEditor(selected);
            }
        }

        StatusMessage = message;
        return message;
    }

    public string TransferMapping(ControllerElementViewModel from, ControllerElementViewModel to, bool copy) =>
        TransferMapping(from.Control, to.Control, copy);

    /// <summary>Whether dropping <paramref name="from"/> onto <paramref name="to"/> would change something (drag feedback).</summary>
    public bool CanTransfer(ControlId from, ControlId to, bool copy) =>
        Current.TransferMapping(from, to, copy ? TransferMode.Copy : TransferMode.Move, _shapes.ShapeOf).Changed;

    /// <summary>
    /// Looks for a new program with sound and raises <see cref="ProgramPromptRequested"/> (UI thread; called by <see cref="Tick"/>).
    /// Nobody is asked while no window listens to the event. An open question without answer counts as "Nicht jetzt"
    /// after <see cref="ProgramPromptViewModel.AutoDismissAfter"/>.
    /// </summary>
    public void CheckForNewPrograms()
    {
        if (_prompt is { } open)
        {
            if (_time.GetUtcNow() - _promptShownAt >= ProgramPromptViewModel.AutoDismissAfter)
            {
                open.NotNow();
            }

            return;
        }

        if (!AskForNewPrograms || ProgramPromptRequested is null)
        {
            return;
        }

        if (_programs.Check(Current, _settings.IgnoredProgramList) is not { } prompt)
        {
            return;
        }

        List<ControllerElementViewModel> choices = Elements.Concat(ExtraElements)
            .Where(e => _shapes.ShapeOf(e.Control) == ControlShape.Continuous)
            .ToList();
        _promptShownAt = _time.GetUtcNow();
        ActivePrompt = new ProgramPromptViewModel(prompt, choices, OnPromptAnswered) { PlaySound = _settings.PromptSound };
        ProgramPromptRequested?.Invoke(_prompt!);
    }

    /// <summary>Puts a program's volume on a control as a further target (an unmapped control gets a new mapping).</summary>
    public bool AssignProgram(string exeName, ControlId control)
    {
        Profile updated = ProgramPromptService.AddProgram(Current, control, exeName);
        if (updated != Current && !ChangeProfile(updated))
        {
            return false;
        }

        string label = LabelOf(control);
        if (_selected is { } selected && selected.Control == control)
        {
            if (Editor.IsDirty)
            {
                Editor.AddApplication(exeName); // the open edit keeps the new program when it is applied
            }
            else
            {
                LoadEditor(selected);
            }
        }

        StatusMessage = Texts.UiProgramAssigned(AppMatcher.FileNameOf(exeName), label);
        if (ShowOsd)
        {
            OsdRequested?.Invoke(new OsdMessage(AppMatcher.FileNameOf(exeName), Texts.OsdProgramAssigned(label)));
        }

        return true;
    }

    /// <summary>Empties the list of programs that are never asked about.</summary>
    public void ResetIgnoredPrograms()
    {
        if (_settings.IgnoredProgramList.Count == 0)
        {
            return;
        }

        ChangeSettings(_settings with { IgnoredPrograms = null }, nameof(IgnoredPrograms));
        OnPropertyChanged(nameof(HasIgnoredPrograms));
        StatusMessage = Texts.UiIgnoredProgramsReset;
    }

    public void NewProfile()
    {
        if (!LeaveEditor())
        {
            return;
        }

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
        _leds?.SetProfile(Current);
        _profileNameEdit = Current.Name;
        RefreshSummaries();
        if (_selected is not null)
        {
            LoadEditor(_selected);
        }

        SetPending(null);
        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(ProfileNameEdit));
        RaiseEditorCommands();
        Save();
    }

    private void RefreshSummaries()
    {
        foreach (ControllerElementViewModel element in _elements.Values)
        {
            element.ShowMapping(StripTargets.ResolveOrNull(Current, Current.Find(element.Control)));
        }

        RefreshMuteStates();
    }

    private void LoadEditor(ControllerElementViewModel element) =>
        Editor.Load(element.Control, _shapes.ShapeOf(element.Control), Current.Find(element.Control), StripContextOf(element.Control));

    /// <summary>The strip of a key or knob for "Wie Fader" (its fader and the fader's current targets); null for faders and other controls.</summary>
    private StripContext? StripContextOf(ControlId control) => StripTargets.SourceFader(control) is { } fader
        ? new StripContext(fader, LabelOf(fader), StripTargets.FaderTargets(Current, control))
        : null;

    private string LabelOf(ControlId control) =>
        _elements.TryGetValue(control, out ControllerElementViewModel? element) ? element.Label : control.ToString();

    /// <summary>
    /// Before the selection (or the profile) changes: applies unsaved valid editor changes, throws unsaved invalid ones
    /// away with a note. Returns false only when applying failed (save error); then everything stays as it is.
    /// </summary>
    private bool LeaveEditor()
    {
        if (_selected is null || !Editor.IsDirty)
        {
            return true;
        }

        if (Editor.IsValid)
        {
            return ApplyEditor(automatic: true);
        }

        string reason = Editor.ValidationMessage ?? string.Empty;
        string label = _selected.Label;
        Editor.Revert();
        StatusMessage = Texts.UiChangesDiscardedInvalid(label, reason);
        return true;
    }

    private bool ApplyEditor(bool automatic)
    {
        if (_selected is null || !Editor.TryBuild(out ControlMapping? mapping, out _))
        {
            return false;
        }

        if (!Editor.IsDirty)
        {
            return true;
        }

        ControllerElementViewModel element = _selected;
        if (!ChangeProfile(mapping is null ? Current.WithoutMapping(element.Control) : Current.WithMapping(mapping)))
        {
            return false;
        }

        LoadEditor(element);
        SetPending(null);
        string summary = MappingDescriber.Describe(Current, mapping);
        StatusMessage = mapping is null ? Texts.UiMappingRemoved(element.Label)
            : automatic ? Texts.UiAutoApplied(element.Label, summary)
            : Texts.UiMappingSaved(element.Label, summary);
        RaiseEditorCommands();
        return true;
    }

    /// <summary>Makes <paramref name="updated"/> the active profile when it could be saved; otherwise nothing changes.</summary>
    private bool ChangeProfile(Profile updated)
    {
        Profile previous = Current;
        _profiles[_active] = updated;
        if (!Save())
        {
            _profiles[_active] = previous;
            return false;
        }

        _router.SetProfile(Current);
        _leds?.SetProfile(Current);
        RefreshSummaries();
        return true;
    }

    private void SetPending(ControllerElementViewModel? element)
    {
        if (_pending == element)
        {
            return;
        }

        _pending = element;
        OnPropertyChanged(nameof(PendingElement));
        OnPropertyChanged(nameof(HasPendingSelection));
        SelectPendingCommand.RaiseCanExecuteChanged();
    }

    private void RaiseEditorCommands()
    {
        ApplyCommand.RaiseCanExecuteChanged();
        DiscardCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
    }

    private void OnEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MappingEditorViewModel.IsDirty) or nameof(MappingEditorViewModel.CanApply))
        {
            RaiseEditorCommands();
        }
        else if (e.PropertyName == nameof(MappingEditorViewModel.IsChoosingProgram) && Editor.IsChoosingProgram)
        {
            RefreshApps(); // "Programm auswählen" shows the programs that use sound right now
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

    // Driver thread: our own LED messages looped back by a driver must never act like a key press.
    private void OnMidiMessage(MidiMessage message)
    {
        if (_leds is not null && _leds.IsOwnEcho(message))
        {
            return;
        }

        _pump.Post(message);
    }

    private void OnLedStatus(string text) => _ui.Post(() => StatusMessage = text);

    /// <summary>Opens the LED output next to the input and follows mute changes; a problem there never reaches the input or the tick.</summary>
    private void PollLeds()
    {
        try
        {
            _leds?.Poll(_midi.State == MidiConnectionState.Connected, _midi.ConnectionGeneration);
        }
        catch (Exception)
        {
            // LED feedback is a convenience; the controller already reports port problems in the status line.
        }
    }

    // Runs on the MIDI worker thread (the shape tracker is thread-safe); the picture is updated on the UI thread.
    private void OnMessageObserved(MidiMessage message)
    {
        ControlShape shape = _shapes.Observe(message);
        DateTimeOffset now = _time.GetUtcNow();
        _ui.Post(() => ShowValue(message, shape, now));
    }

    private void ShowValue(MidiMessage message, ControlShape shape, DateTimeOffset now)
    {
        if (_elements.TryGetValue(message.Control, out ControllerElementViewModel? element))
        {
            element.LastValue = message.Value;
            element.Shape = shape;
        }

        if (_gestures.Observe(message, shape, now) is { } control)
        {
            OnDeviceGesture(control);
        }
    }

    /// <summary>A control was deliberately operated at the device (see <see cref="DeviceGestureDetector"/>).</summary>
    private void OnDeviceGesture(ControlId control)
    {
        if (_prompt is { } prompt && prompt.Assign(control))
        {
            return; // moving a fader or knob answers the open question for a new program
        }

        if (!_followDevice || _isLearning)
        {
            return;
        }

        if (!_elements.TryGetValue(control, out ControllerElementViewModel? element))
        {
            element = AddExtraElement(control, Texts.UiExtraControl(control.ToString()));
            element.Shape = _shapes.ShapeOf(control);
            ExtraElements.Add(element);
            OnPropertyChanged(nameof(HasExtraElements));
            element.ShowMapping(StripTargets.ResolveOrNull(Current, Current.Find(control)));
        }

        if (element == _selected)
        {
            return; // moving the selected fader changes nothing
        }

        if (_selected is not null && Editor.IsDirty)
        {
            // Touching the device must not commit or lose a half-done edit: only point at the control.
            SetPending(element);
            StatusMessage = Texts.UiPendingSelection(element.Label, _selected.Label);
            return;
        }

        ShowSelection(element);
    }

    private void OnPromptAnswered(ProgramPromptViewModel prompt, ProgramPromptAnswer answer, ControlId? control)
    {
        _programs.Close();
        if (_prompt == prompt)
        {
            ActivePrompt = null;
        }

        switch (answer)
        {
            case ProgramPromptAnswer.Assigned when control is { } chosen:
                AssignProgram(prompt.ExeName, chosen);
                break;
            case ProgramPromptAnswer.NeverAsk:
                if (!_settings.IgnoredProgramList.Any(p => AppMatcher.NormalizeName(p) == AppMatcher.NormalizeName(prompt.ExeName)))
                {
                    ChangeSettings(_settings with { IgnoredPrograms = [.. _settings.IgnoredProgramList, prompt.ExeName] }, nameof(IgnoredPrograms));
                    OnPropertyChanged(nameof(HasIgnoredPrograms));
                }

                StatusMessage = Texts.UiProgramIgnored(prompt.ExeName);
                break;
        }
    }

    private void OnControlLearned(ControlId control) => _ui.Post(() =>
    {
        IsLearning = false;
        if (!_elements.TryGetValue(control, out ControllerElementViewModel? element))
        {
            element = AddExtraElement(control, Texts.UiExtraControl(control.ToString()));
            element.Shape = _shapes.ShapeOf(control);
            ExtraElements.Add(element);
            OnPropertyChanged(nameof(HasExtraElements));
            element.ShowMapping(StripTargets.ResolveOrNull(Current, Current.Find(control)));
        }

        Select(element);
        StatusMessage = Texts.UiLearned(element.Label);
    });

    // Runs on the MIDI worker thread; only the results are passed on to the UI thread.
    private void OnActionsRequested(IReadOnlyList<ActionInvocation> invocations)
    {
        IReadOnlyList<ActionResult> results = _executor.ExecuteAll(invocations);
        if (_leds is not null && invocations.Any(i => !i.Action.IsContinuous))
        {
            _leds.Refresh(); // a pressed mute key shows its new state at once, not only with the next tick
        }

        _ui.Post(() =>
        {
            ControlId control = invocations[0].Mapping.Control;
            string label = _elements.TryGetValue(control, out ControllerElementViewModel? element)
                ? element.Label
                : control.ToString();
            ShowResults(label, invocations, results);
            if (invocations.Any(i => i.Action is MuteAction))
            {
                RefreshMuteStates(); // a pressed mute key lights up in the picture at once
            }
        });
    }

    /// <summary>Shows the outcome of one control message: the display summary (see <see cref="OsdMessageFactory"/>), the first failure (if any) or else the first message.</summary>
    private void ShowResults(string label, IReadOnlyList<ActionInvocation> invocations, IReadOnlyList<ActionResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        if (ShowOsd && OsdMessageFactory.Create(invocations, results) is { } osd)
        {
            OsdRequested?.Invoke(osd);
        }

        ActionResult result = results.FirstOrDefault(r => r.IsFailure) ?? results[0];

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

    /// <summary>Saves changed settings; <paramref name="property"/> is the property of this view model to announce (null = none).</summary>
    private void ChangeSettings(AppSettings updated, string? property)
    {
        if (updated == _settings)
        {
            return;
        }

        _settings = updated;
        if (property is not null)
        {
            OnPropertyChanged(property);
        }

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
