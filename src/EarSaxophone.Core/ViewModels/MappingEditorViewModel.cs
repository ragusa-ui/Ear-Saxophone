using System.Collections.ObjectModel;
using System.ComponentModel;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Input;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.ViewModels;

/// <summary>Kind of action in the editor; also the symbol id of a badge (see <see cref="MappingBadge"/>).</summary>
public enum ActionKind
{
    None,
    SetVolume,
    Mute,
    Media,
    Hotkey,
    SwitchDevice,
}

/// <summary>Symbol id of an entry in the target list.</summary>
public enum TargetSymbol
{
    Speakers,
    Microphone,
    Application,
    Device,

    /// <summary>"Wie Fader n": the targets of the fader in the same strip (<see cref="AudioTarget.Strip"/>).</summary>
    Strip,
}

public sealed record Option<T>(string Text, T Value);

/// <summary>A program that currently has an audio session (offered when adding targets).</summary>
public sealed record ProgramInfo(string ExeName, string? ExePath = null);

/// <summary>
/// One entry of the target list (speakers, microphone, a program or one specific device). <see cref="Target"/> is kept
/// exactly as loaded, so nothing of a stored target is lost. <see cref="ExePath"/> is known when the program currently
/// has an audio session (for its icon).
/// </summary>
public sealed record TargetEntry(AudioTarget Target, string Name, TargetSymbol Symbol, string? ExePath = null);

/// <summary>
/// What the editor needs to know about the strip of the edited control for "Wie Fader": the strip's fader, its label and its current
/// volume targets. Null for the fader itself and for controls outside the strips.
/// </summary>
public sealed record StripContext(ControlId Fader, string FaderLabel, IReadOnlyList<AudioTarget> FaderTargets);

/// <summary>One device in the list of devices that take part in the rotation of a "switch device" key.</summary>
public sealed class DeviceChoice(string id, string name, DeviceFlow flow) : ObservableObject
{
    private bool _isSelected;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public DeviceFlow Flow { get; } = flow;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}

/// <summary>
/// Edits the mapping of the selected control: one kind of action, for volume and mute a list of targets (several
/// programs on one fader), the mute mode, an optional own name and the parameters of the other kinds. Only kinds that
/// fit the shape of the control are offered (<see cref="ActionRules"/>). The editor knows whether something was changed
/// (<see cref="IsDirty"/>) and whether it can be applied (<see cref="CanApply"/>); nothing is saved by the editor itself.
/// Actions of a loaded mapping that this editor cannot show (for example a media action next to mute targets) are kept
/// unchanged (<see cref="KeptActions"/>) until they are dropped explicitly.
/// On a key of a strip whose fader has volume targets, choosing "Stumm" preselects the target "Wie Fader n" (<see cref="AudioTarget.Strip"/>):
/// the key then mutes whatever the fader controls, also programs added to the fader later. It can be removed like any target and
/// added again from the add menu (<see cref="AddStripTargetCommand"/>). Knobs get no automatic target.
/// </summary>
public sealed class MappingEditorViewModel : ObservableObject
{
    private static readonly ActionKind[] KeyKinds = [ActionKind.Mute, ActionKind.Media, ActionKind.SwitchDevice, ActionKind.Hotkey];

    private ActionKind _kind;
    private ControlShape _shape;
    private MediaCommand _media = MediaCommand.PlayPause;
    private string _hotkeyText = string.Empty;
    private bool _invert;
    private double _minPercent;
    private double _maxPercent = 100;
    private string? _validationMessage;
    private DeviceFlow _switchFlow = DeviceFlow.Render;
    private List<string> _selectedDeviceIds = [];
    private List<(string Id, string Name, DeviceFlow Flow)> _devices = [];
    private Dictionary<string, string?> _programPaths = new(StringComparer.OrdinalIgnoreCase);
    private MuteMode _muteMode = MuteMode.Toggle;
    private string _name = string.Empty;
    private string _newAppName = string.Empty;
    private List<ActionDefinition> _kept = [];
    private ValueScaling? _loadedScaling;
    private IReadOnlyList<Option<ActionKind>> _kindOptions = [];
    private bool _loading;
    private bool _touched;
    private bool _baselineValid;
    private ControlMapping? _baseline;
    private ControlMapping? _loaded;
    private bool _isDirty;
    private bool _isValid = true;
    private bool _isChoosingProgram;
    private StripContext? _strip;

    public MappingEditorViewModel()
    {
        _kindOptions = OptionsFor(ControlShape.Unknown);
        Targets.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTargets));
            OnPropertyChanged(nameof(CanAddStripTarget));
            RebuildProgramChoices();
            Changed();
        };
        AddSpeakersCommand = new RelayCommand(_ => AddTarget(AudioTarget.Speakers));
        AddMicrophoneCommand = new RelayCommand(_ => AddTarget(AudioTarget.Microphone));
        AddStripTargetCommand = new RelayCommand(_ => AddTarget(AudioTarget.Strip));
        AddAppCommand = new RelayCommand(p => AddApplication(p as string ?? _newAppName));
        ShowProgramChoiceCommand = new RelayCommand(_ => IsChoosingProgram = true);
        HideProgramChoiceCommand = new RelayCommand(_ => IsChoosingProgram = false);
        RemoveTargetCommand = new RelayCommand(p => RemoveTarget(p as TargetEntry));
        MoveTargetUpCommand = new RelayCommand(p => MoveTarget(p as TargetEntry, -1));
        MoveTargetDownCommand = new RelayCommand(p => MoveTarget(p as TargetEntry, +1));
        DropKeptActionsCommand = new RelayCommand(_ => DropKeptActions());
    }

    /// <summary>The kinds that fit the shape of the control: stepless only volume, keys mute/media/switch/shortcut (and "none").</summary>
    public IReadOnlyList<Option<ActionKind>> KindOptions
    {
        get => _kindOptions;
        private set => SetField(ref _kindOptions, value);
    }

    /// <summary>False when there is nothing to choose (a fader only knows volume), so the picker can be hidden.</summary>
    public bool HasKindChoice => _kindOptions.Count > 1;

    public IReadOnlyList<Option<MuteMode>> MuteModeOptions { get; } =
    [
        new(Texts.UiMuteModeToggle, MuteMode.Toggle),
        new(Texts.UiMuteModeMuteWhileHeld, MuteMode.MuteWhileHeld),
        new(Texts.UiMuteModeUnmuteWhileHeld, MuteMode.UnmuteWhileHeld),
    ];

    public IReadOnlyList<Option<MediaCommand>> MediaOptions { get; } =
        Enum.GetValues<MediaCommand>().Select(c => new Option<MediaCommand>(Texts.MediaCommandName(c), c)).ToList();

    public IReadOnlyList<Option<DeviceFlow>> SwitchFlowOptions { get; } =
    [
        new(Texts.UiSwitchRender, DeviceFlow.Render),
        new(Texts.UiSwitchCapture, DeviceFlow.Capture),
    ];

    /// <summary>Devices of the chosen kind that can be part of the rotation (all connected ones).</summary>
    public ObservableCollection<DeviceChoice> DeviceChoices { get; } = [];

    public bool HasNoDevices => DeviceChoices.Count == 0;

    /// <summary>Programs that currently produce or hold audio sessions, offered as suggestions (names).</summary>
    public ObservableCollection<string> AvailableApps { get; } = [];

    /// <summary>
    /// The programs offered in "Programm auswählen": those of <see cref="AvailableApps"/> that are not in the target list yet
    /// (same order).
    /// </summary>
    public ObservableCollection<string> ProgramChoices { get; } = [];

    public bool HasProgramChoices => ProgramChoices.Count > 0;

    /// <summary>
    /// The "Programm auswählen" part is open (entry "Programm …" of the add menu): list of programs with sound, "Datei öffnen …"
    /// and a name field. Closed again by loading another control or by <see cref="HideProgramChoiceCommand"/>.
    /// </summary>
    public bool IsChoosingProgram
    {
        get => _isChoosingProgram;
        set => SetField(ref _isChoosingProgram, value);
    }

    /// <summary>Targets of the volume or mute actions, in order (the first one is shown in the overlay).</summary>
    public ObservableCollection<TargetEntry> Targets { get; } = [];

    public bool HasTargets => Targets.Count > 0;

    public RelayCommand AddSpeakersCommand { get; }

    public RelayCommand AddMicrophoneCommand { get; }

    /// <summary>Adds "Wie Fader n" (entry of the add menu, shown while <see cref="CanAddStripTarget"/>).</summary>
    public RelayCommand AddStripTargetCommand { get; }

    /// <summary>The strip of the edited control (fader, label, its targets); null for faders and controls outside the strips.</summary>
    public StripContext? Strip => _strip;

    /// <summary>"Wie Fader n" can be added now: the control is a key or knob of a strip, the kind uses targets and it is not listed yet.</summary>
    public bool CanAddStripTarget => _strip is not null && IsAudio && !Targets.Any(t => t.Target.IsStrip);

    /// <summary>Text of the entry "Wie Fader n" with what the fader controls, for example "Wie Fader 1 (opera.exe + 2)".</summary>
    public string StripTargetText => _strip is null ? Texts.TargetStripEmptyShort : Texts.UiStripTargetName(_strip.FaderLabel, DescribeTargets(_strip.FaderTargets));

    /// <summary>Adds the program given as parameter (a string), or else the typed <see cref="NewAppName"/>.</summary>
    public RelayCommand AddAppCommand { get; }

    /// <summary>Opens "Programm auswählen" (<see cref="IsChoosingProgram"/>).</summary>
    public RelayCommand ShowProgramChoiceCommand { get; }

    public RelayCommand HideProgramChoiceCommand { get; }

    /// <summary>Parameter: the <see cref="TargetEntry"/> to remove.</summary>
    public RelayCommand RemoveTargetCommand { get; }

    public RelayCommand MoveTargetUpCommand { get; }

    public RelayCommand MoveTargetDownCommand { get; }

    public RelayCommand DropKeptActionsCommand { get; }

    public ControlId Control { get; private set; }

    /// <summary>Shape of the edited control; decides which kinds are offered and allowed.</summary>
    public ControlShape Shape => _shape;

    public ActionKind Kind
    {
        get => _kind;
        set
        {
            if (!IsKindAllowed(value))
            {
                OnPropertyChanged(); // lets a bound picker fall back to the current kind
                return;
            }

            if (SetField(ref _kind, value))
            {
                RaiseVisibility();
                if (value == ActionKind.Mute)
                {
                    PreselectStripTarget();
                }

                Changed();
            }
        }
    }

    /// <summary>Text typed into the "add program" field.</summary>
    public string NewAppName
    {
        get => _newAppName;
        set => SetField(ref _newAppName, value ?? string.Empty);
    }

    /// <summary>Optional own title of the mapping (for example "Spiele"); empty means "describe by the actions".</summary>
    public string Name
    {
        get => _name;
        set => Set(ref _name, value ?? string.Empty);
    }

    public MediaCommand Media
    {
        get => _media;
        set => Set(ref _media, value);
    }

    public string HotkeyText
    {
        get => _hotkeyText;
        set => Set(ref _hotkeyText, value ?? string.Empty);
    }

    public bool Invert
    {
        get => _invert;
        set => Set(ref _invert, value);
    }

    public double MinPercent
    {
        get => _minPercent;
        set => Set(ref _minPercent, Math.Clamp(value, 0, 100));
    }

    public double MaxPercent
    {
        get => _maxPercent;
        set => Set(ref _maxPercent, Math.Clamp(value, 0, 100));
    }

    public MuteMode MuteMode
    {
        get => _muteMode;
        set => Set(ref _muteMode, Enum.IsDefined(value) ? value : MuteMode.Toggle);
    }

    public DeviceFlow SwitchFlow
    {
        get => _switchFlow;
        set
        {
            if (_switchFlow == value)
            {
                return;
            }

            _switchFlow = value;
            OnPropertyChanged();
            RebuildDeviceChoices();
            Changed();
        }
    }

    /// <summary>Descriptions of the actions that are kept unchanged because this editor cannot show them.</summary>
    public IReadOnlyList<string> KeptActions => _kept.Select(MappingDescriber.Describe).ToList();

    public bool HasKeptActions => _kept.Count > 0;

    public string KeptActionsText => _kept.Count == 0 ? string.Empty : Texts.UiKeptActions(_kept.Count);

    /// <summary>The current input or the last rejected input is not usable; the text says why.</summary>
    public string? ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetField(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationMessage));
            }
        }
    }

    public bool HasValidationMessage => _validationMessage is not null;

    /// <summary>Something differs from what was loaded (the "apply" bar is shown).</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetField(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    /// <summary>The current input builds a usable mapping (or "no mapping").</summary>
    public bool IsValid
    {
        get => _isValid;
        private set
        {
            if (SetField(ref _isValid, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public bool CanApply => _isDirty && _isValid;

    public bool IsAudio => _kind is ActionKind.SetVolume or ActionKind.Mute;

    public bool IsVolume => _kind == ActionKind.SetVolume;

    public bool IsMute => _kind == ActionKind.Mute;

    public bool IsMedia => _kind == ActionKind.Media;

    public bool IsHotkey => _kind == ActionKind.Hotkey;

    public bool IsSwitchDevice => _kind == ActionKind.SwitchDevice;

    /// <summary>Whether the kind may be chosen for the shape of the control (<see cref="ActionKind.None"/> always).</summary>
    public bool IsKindAllowed(ActionKind kind) => kind switch
    {
        ActionKind.None => true,
        ActionKind.SetVolume => _shape != ControlShape.Button,
        _ => Enum.IsDefined(kind) && _shape != ControlShape.Continuous,
    };

    /// <summary>Tells the editor which programs have audio sessions right now (suggestions and icons).</summary>
    public void SetAvailablePrograms(IEnumerable<ProgramInfo> programs)
    {
        List<ProgramInfo> list = programs.ToList();
        AvailableApps.Clear();
        foreach (ProgramInfo program in list)
        {
            AvailableApps.Add(program.ExeName);
        }

        _programPaths = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (ProgramInfo program in list)
        {
            _programPaths.TryAdd(AppMatcher.NormalizeName(program.ExeName), program.ExePath);
        }

        RebuildProgramChoices();
    }

    /// <summary>Tells the editor which devices exist right now (both kinds). Ticked devices stay ticked.</summary>
    public void SetAvailableDevices(IEnumerable<(string Id, string Name, DeviceFlow Flow)> devices)
    {
        _devices = devices.ToList();
        RebuildDeviceChoices();
        RefreshTargetNames();
        Reevaluate();
    }

    /// <summary>
    /// Shows the mapping of a control (null = unmapped). The editor is clean afterwards. <paramref name="strip"/> tells about the
    /// strip of the control ("Wie Fader"); null for the fader itself and for controls outside the strips.
    /// </summary>
    public void Load(ControlId control, ControlShape shape, ControlMapping? mapping, StripContext? strip = null)
    {
        _loading = true;
        try
        {
            Control = control;
            _shape = shape;
            _loaded = mapping;
            _strip = strip;
            OnPropertyChanged(nameof(Strip));
            OnPropertyChanged(nameof(StripTargetText));
            IsChoosingProgram = false;
            OnPropertyChanged(nameof(Control));
            OnPropertyChanged(nameof(Shape));
            KindOptions = OptionsFor(shape);
            OnPropertyChanged(nameof(HasKindChoice));
            ReadMapping(mapping);
        }
        finally
        {
            _loading = false;
        }

        _touched = false;
        Recompute(out ControlMapping? built, out bool ok, out _);
        _baselineValid = ok;
        _baseline = built;
        IsDirty = false;
        RaiseVisibility();
    }

    /// <summary>Throws away all changes and shows the loaded mapping again.</summary>
    public void Revert() => Load(Control, _shape, _loaded, _strip);

    /// <summary>Adds a target. Returns false (and says why) when it is invalid or already in the list.</summary>
    public bool AddTarget(AudioTarget target)
    {
        if (target is null || !target.IsValid)
        {
            ValidationMessage = Texts.UiEditorAppRequired;
            return false;
        }

        if (target.IsStrip && _strip is null)
        {
            ValidationMessage = Texts.UiStripTargetNotHere;
            return false;
        }

        TargetEntry entry = EntryFor(target);
        if (Targets.Any(t => SameTarget(t.Target, target)))
        {
            ValidationMessage = Texts.UiTargetAlreadyListed(entry.Name);
            return false;
        }

        Targets.Add(entry);
        if (_kind == ActionKind.None)
        {
            // Adding a target to a control without action starts the obvious kind for it.
            Kind = _shape == ControlShape.Button ? ActionKind.Mute : ActionKind.SetVolume;
        }

        return true;
    }

    /// <summary>Adds a program by its executable name (typed or chosen); clears <see cref="NewAppName"/> on success.</summary>
    public bool AddApplication(string? exeName)
    {
        string name = AppMatcher.FileNameOf(exeName ?? string.Empty);
        if (name.Length == 0)
        {
            ValidationMessage = Texts.UiEditorAppRequired;
            return false;
        }

        if (!AddTarget(AudioTarget.Application(name)))
        {
            return false;
        }

        NewAppName = string.Empty;
        return true;
    }

    /// <summary>
    /// Adds a program chosen as a file ("Datei öffnen …"): the file name (for example "Spiel.exe") becomes the program identity,
    /// so a program that is not running right now can be assigned too. Only program files (.exe) are accepted.
    /// </summary>
    public bool AddProgramFile(string? path)
    {
        string name = AppMatcher.FileNameOf(path ?? string.Empty);
        if (name.Length == 0)
        {
            ValidationMessage = Texts.UiEditorAppRequired;
            return false;
        }

        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.Length == ".exe".Length)
        {
            ValidationMessage = Texts.UiProgramFileNotExe;
            return false;
        }

        return AddTarget(AudioTarget.Application(name));
    }

    public bool RemoveTarget(TargetEntry? entry) => entry is not null && Targets.Remove(entry);

    /// <summary>Replaces the target at <paramref name="index"/> ("change"); false when the new one is invalid or already listed elsewhere.</summary>
    public bool ReplaceTarget(int index, AudioTarget target)
    {
        if (index < 0 || index >= Targets.Count || target is null || !target.IsValid)
        {
            return false;
        }

        if (Targets.Where((t, i) => i != index).Any(t => SameTarget(t.Target, target)))
        {
            ValidationMessage = Texts.UiTargetAlreadyListed(EntryFor(target).Name);
            return false;
        }

        Targets[index] = EntryFor(target);
        return true;
    }

    /// <summary>Moves a target to another position (the first one leads the overlay).</summary>
    public bool MoveTarget(int from, int to)
    {
        if (from < 0 || from >= Targets.Count || to < 0 || to >= Targets.Count || from == to)
        {
            return false;
        }

        Targets.Move(from, to);
        return true;
    }

    /// <summary>Removes the actions that were kept unchanged.</summary>
    public void DropKeptActions()
    {
        if (_kept.Count == 0)
        {
            return;
        }

        _kept = [];
        RaiseKept();
        Changed();
    }

    /// <summary>
    /// Builds the mapping from the current input. Returns true with a null mapping for "no action" (remove the binding);
    /// returns false and a readable message when the input is incomplete or does not fit the control.
    /// </summary>
    public bool TryBuild(out ControlMapping? mapping, out string? error)
    {
        bool ok = Build(out mapping, out error);
        ValidationMessage = error;
        return ok;
    }

    private static IReadOnlyList<Option<ActionKind>> OptionsFor(ControlShape shape)
    {
        var options = new List<Option<ActionKind>>();
        if (shape != ControlShape.Continuous)
        {
            options.Add(new(Texts.UiKindNone, ActionKind.None));
        }

        if (shape != ControlShape.Button)
        {
            options.Add(new(Texts.UiKindVolume, ActionKind.SetVolume));
        }

        if (shape != ControlShape.Continuous)
        {
            options.AddRange(KeyKinds.Select(k => new Option<ActionKind>(KindText(k), k)));
        }

        return options;
    }

    private static string KindText(ActionKind kind) => kind switch
    {
        ActionKind.SetVolume => Texts.UiKindVolume,
        ActionKind.Mute => Texts.UiKindMute,
        ActionKind.Media => Texts.UiKindMedia,
        ActionKind.Hotkey => Texts.UiKindHotkey,
        ActionKind.SwitchDevice => Texts.UiKindSwitch,
        _ => Texts.UiKindNone,
    };

    private static bool SameTarget(AudioTarget a, AudioTarget b) =>
        a.IsStrip || b.IsStrip ? a.IsStrip && b.IsStrip
        : a.Kind == AudioTargetKind.Application && b.Kind == AudioTargetKind.Application
            ? a.Flow == b.Flow && a.App is { } x && b.App is { } y && AppMatcher.NormalizeName(x.ExeName) == AppMatcher.NormalizeName(y.ExeName)
            : a == b;

    private void ReadMapping(ControlMapping? mapping)
    {
        ValidationMessage = null;
        NewAppName = string.Empty;
        HotkeyText = string.Empty;
        Media = MediaCommand.PlayPause;
        Invert = false;
        MinPercent = 0;
        MaxPercent = 100;
        MuteMode = MuteMode.Toggle;
        Name = mapping?.Name ?? string.Empty;
        _switchFlow = DeviceFlow.Render;
        _selectedDeviceIds = [];
        _loadedScaling = mapping?.Scaling;
        Targets.Clear();
        var kept = new List<ActionDefinition>();

        ActionDefinition? first = mapping?.FirstAction is { IsValid: true } valid && IsKindAllowed(MappingBadge.KindOf(valid)) ? valid : null;
        ActionKind kind = first is null ? DefaultKind() : MappingBadge.KindOf(first);
        switch (first)
        {
            case SetVolumeAction:
                Invert = mapping!.EffectiveScaling.Invert;
                MinPercent = Math.Round(mapping.EffectiveScaling.Min * 100);
                MaxPercent = Math.Round(mapping.EffectiveScaling.Max * 100);
                break;
            case MuteAction mute:
                MuteMode = mute.Mode;
                break;
            case MediaAction media:
                Media = media.Command;
                break;
            case HotkeyAction hotkey:
                HotkeyText = hotkey.Keys;
                break;
            case SwitchDeviceAction switchDevice:
                _switchFlow = switchDevice.Flow;
                _selectedDeviceIds = switchDevice.SelectedIds.ToList();
                break;
        }

        bool firstSeen = false;
        foreach (ActionDefinition action in mapping?.Actions ?? [])
        {
            bool isFirst = !firstSeen && ReferenceEquals(action, first);
            firstSeen |= isFirst;
            bool shown = isFirst && first is not SetVolumeAction and not MuteAction;
            if (first is SetVolumeAction && action is SetVolumeAction { IsValid: true } volume && !Targets.Any(t => SameTarget(t.Target, volume.Target)))
            {
                Targets.Add(EntryFor(volume.Target));
                shown = true;
            }
            else if (first is MuteAction firstMute && action is MuteAction { IsValid: true } mute && mute.Mode == firstMute.Mode
                && !Targets.Any(t => SameTarget(t.Target, mute.Target)))
            {
                Targets.Add(EntryFor(mute.Target));
                shown = true;
            }

            if (!shown)
            {
                kept.Add(action);
            }
        }

        _kept = kept;
        _kind = kind;
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(SwitchFlow));
        RaiseKept();
        RebuildDeviceChoices();
    }

    /// <summary>A fader or knob can only regulate volume, so an unmapped one starts there.</summary>
    private ActionKind DefaultKind() => _shape == ControlShape.Continuous ? ActionKind.SetVolume : ActionKind.None;

    private bool Build(out ControlMapping? mapping, out string? error)
    {
        mapping = null;
        error = null;
        var actions = new List<ActionDefinition>();
        ValueScaling? scaling = null;
        switch (_kind)
        {
            case ActionKind.SetVolume:
                if (_minPercent > _maxPercent)
                {
                    error = Texts.UiEditorRangeInvalid;
                    return false;
                }

                actions.AddRange(Targets.Select(t => new SetVolumeAction(t.Target)));
                var built = new ValueScaling((float)(_minPercent / 100), (float)(_maxPercent / 100), _invert);
                scaling = built is { Min: 0f, Max: 1f, Invert: false } ? null : built;
                break;
            case ActionKind.Mute:
                actions.AddRange(Targets.Select(t => new MuteAction(t.Target, _muteMode)));
                break;
            case ActionKind.Media:
                actions.Add(new MediaAction(_media));
                break;
            case ActionKind.Hotkey:
                if (!Hotkey.TryParse(_hotkeyText, out Hotkey hotkey, out string hotkeyError))
                {
                    error = hotkeyError;
                    return false;
                }

                actions.Add(new HotkeyAction(hotkey.ToString()));
                break;
            case ActionKind.SwitchDevice:
                actions.Add(new SwitchDeviceAction(_switchFlow, CurrentDeviceIds()));
                break;
        }

        if (_kind != ActionKind.SetVolume && _kept.Any(a => a.IsContinuous))
        {
            scaling = _loadedScaling;
        }

        actions.AddRange(_kept);
        if (_strip is null && _kind is ActionKind.SetVolume or ActionKind.Mute && Targets.Any(t => t.Target.IsStrip))
        {
            error = Texts.UiStripTargetNotHere;
            return false;
        }

        if (actions.FirstOrDefault(a => !a.IsValid) is not null)
        {
            error = Texts.UiEditorActionIncomplete;
            return false;
        }

        if (actions.FirstOrDefault(a => !ActionRules.IsAllowed(a, _shape)) is { } misfit)
        {
            error = Texts.UiEditorNotAllowed(MappingDescriber.KindName(misfit), _shape);
            return false;
        }

        if (actions.Count == 0)
        {
            return true;
        }

        string name = _name.Trim();
        mapping = new ControlMapping(Control, actions, scaling, name.Length == 0 ? null : name);
        return true;
    }

    /// <summary>The ticked devices of the chosen kind; devices that are not connected right now stay in the rotation.</summary>
    private List<string> CurrentDeviceIds() =>
        _selectedDeviceIds.Where(id => _devices.FindIndex(d => d.Id == id) is var i && (i < 0 || _devices[i].Flow == _switchFlow)).ToList();

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (SetField(ref field, value, name))
        {
            Changed();
        }
    }

    /// <summary>Re-evaluates validity and the dirty state after any change (not while loading).</summary>
    private void Changed()
    {
        if (_loading)
        {
            return;
        }

        _touched = true;
        Recompute(out ControlMapping? built, out bool ok, out _);
        IsDirty = ok ? !_baselineValid || !Equals(built, _baseline) : _touched;
    }

    /// <summary>Outside information changed (device list): an untouched editor stays clean, a touched one is compared again.</summary>
    private void Reevaluate()
    {
        if (_touched)
        {
            Changed();
            return;
        }

        Recompute(out ControlMapping? built, out bool ok, out _);
        _baselineValid = ok;
        _baseline = built;
        IsDirty = false;
    }

    private void Recompute(out ControlMapping? built, out bool ok, out string? error)
    {
        ok = Build(out built, out error);
        IsValid = ok;
        ValidationMessage = error;
    }

    private TargetEntry EntryFor(AudioTarget target)
    {
        switch (target.Kind)
        {
            case AudioTargetKind.Application:
                string exe = target.App?.ExeName ?? string.Empty;
                _programPaths.TryGetValue(AppMatcher.NormalizeName(exe), out string? path);
                return new TargetEntry(target, exe, TargetSymbol.Application, path ?? target.App?.ExePath);
            case AudioTargetKind.SpecificDevice:
                string? device = _devices.FirstOrDefault(d => d.Id == target.DeviceId).Name;
                return new TargetEntry(target, device ?? Texts.TargetDeviceShort, TargetSymbol.Device);
            case AudioTargetKind.Strip:
                return new TargetEntry(target, StripTargetText, TargetSymbol.Strip);
            default:
                return target.Flow == DeviceFlow.Capture
                    ? new TargetEntry(target, Texts.TargetMicrophoneShort, TargetSymbol.Microphone)
                    : new TargetEntry(target, Texts.TargetSpeakersShort, TargetSymbol.Speakers);
        }
    }

    /// <summary>Device names may become known later (device list refreshed); the targets themselves stay the same.</summary>
    private void RefreshTargetNames()
    {
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            for (int i = 0; i < Targets.Count; i++)
            {
                TargetEntry fresh = EntryFor(Targets[i].Target);
                if (fresh != Targets[i])
                {
                    Targets[i] = fresh;
                }
            }
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private void MoveTarget(TargetEntry? entry, int step)
    {
        int index = entry is null ? -1 : Targets.IndexOf(entry);
        if (index >= 0)
        {
            MoveTarget(index, index + step);
        }
    }

    private void RebuildProgramChoices()
    {
        var listed = Targets.Where(t => t.Target.Kind == AudioTargetKind.Application && t.Target.App is not null)
            .Select(t => AppMatcher.NormalizeName(t.Target.App!.ExeName))
            .ToHashSet();
        List<string> choices = AvailableApps.Where(a => !listed.Contains(AppMatcher.NormalizeName(a))).ToList();
        if (choices.SequenceEqual(ProgramChoices))
        {
            return;
        }

        ProgramChoices.Clear();
        foreach (string choice in choices)
        {
            ProgramChoices.Add(choice);
        }

        OnPropertyChanged(nameof(HasProgramChoices));
    }

    /// <summary>
    /// "Stumm" chosen on a key of a strip: when nothing is listed yet and the strip's fader has volume targets, "Wie Fader n" is the
    /// target, so the key mutes what the fader controls without choosing every program again.
    /// </summary>
    private void PreselectStripTarget()
    {
        if (_strip is { FaderTargets.Count: > 0 } && _shape == ControlShape.Button && Targets.Count == 0)
        {
            Targets.Add(EntryFor(AudioTarget.Strip));
        }
    }

    private static string DescribeTargets(IReadOnlyList<AudioTarget> targets) => targets.Count switch
    {
        0 => Texts.UiStripTargetNone,
        1 => MappingDescriber.Describe(targets[0]),
        _ => Texts.OsdMoreTargets(MappingDescriber.Describe(targets[0]), targets.Count - 1),
    };

    private void RaiseKept()
    {
        OnPropertyChanged(nameof(KeptActions));
        OnPropertyChanged(nameof(HasKeptActions));
        OnPropertyChanged(nameof(KeptActionsText));
    }

    private void RaiseVisibility()
    {
        OnPropertyChanged(nameof(IsAudio));
        OnPropertyChanged(nameof(IsVolume));
        OnPropertyChanged(nameof(IsMute));
        OnPropertyChanged(nameof(IsMedia));
        OnPropertyChanged(nameof(IsHotkey));
        OnPropertyChanged(nameof(IsSwitchDevice));
        OnPropertyChanged(nameof(CanAddStripTarget));
    }

    private void RebuildDeviceChoices()
    {
        foreach (DeviceChoice old in DeviceChoices)
        {
            old.PropertyChanged -= OnDeviceChoiceChanged;
        }

        DeviceChoices.Clear();
        foreach ((string id, string name, DeviceFlow flow) in _devices.Where(d => d.Flow == _switchFlow).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            var choice = new DeviceChoice(id, name, flow) { IsSelected = _selectedDeviceIds.Contains(id) };
            choice.PropertyChanged += OnDeviceChoiceChanged;
            DeviceChoices.Add(choice);
        }

        OnPropertyChanged(nameof(HasNoDevices));
    }

    private void OnDeviceChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DeviceChoice choice || e.PropertyName != nameof(DeviceChoice.IsSelected))
        {
            return;
        }

        _selectedDeviceIds.Remove(choice.Id);
        if (choice.IsSelected)
        {
            _selectedDeviceIds.Add(choice.Id);
        }

        Changed();
    }
}
