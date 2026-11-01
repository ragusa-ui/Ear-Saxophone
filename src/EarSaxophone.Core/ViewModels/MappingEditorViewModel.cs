using System.Collections.ObjectModel;
using EarSaxophone.Core.Input;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.ViewModels;

public enum ActionKind
{
    None,
    SetVolume,
    ToggleMute,
    Media,
    Hotkey,
    SwitchDevice,
}

public enum TargetKind
{
    Speakers,
    Microphone,
    Application,
}

public sealed record Option<T>(string Text, T Value);

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

/// <summary>Edits the action of the selected control. Nothing is applied until <see cref="TryBuild"/> succeeds.</summary>
public sealed class MappingEditorViewModel : ObservableObject
{
    private ActionKind _kind;
    private TargetKind _target;
    private string _appName = string.Empty;
    private MediaCommand _media = MediaCommand.PlayPause;
    private string _hotkeyText = string.Empty;
    private bool _invert;
    private double _minPercent;
    private double _maxPercent = 100;
    private string? _validationMessage;
    private DeviceFlow _switchFlow = DeviceFlow.Render;
    private List<string> _selectedDeviceIds = [];
    private List<(string Id, string Name, DeviceFlow Flow)> _devices = [];

    public IReadOnlyList<Option<ActionKind>> KindOptions { get; } =
    [
        new(Texts.UiKindNone, ActionKind.None),
        new(Texts.UiKindVolume, ActionKind.SetVolume),
        new(Texts.UiKindMute, ActionKind.ToggleMute),
        new(Texts.UiKindMedia, ActionKind.Media),
        new(Texts.UiKindHotkey, ActionKind.Hotkey),
        new(Texts.UiKindSwitch, ActionKind.SwitchDevice),
    ];

    public IReadOnlyList<Option<TargetKind>> TargetOptions { get; } =
    [
        new(Texts.UiTargetSpeakers, TargetKind.Speakers),
        new(Texts.UiTargetMicrophone, TargetKind.Microphone),
        new(Texts.UiTargetApplication, TargetKind.Application),
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

    /// <summary>Programs that currently produce or hold audio sessions, offered as suggestions.</summary>
    public ObservableCollection<string> AvailableApps { get; } = [];

    public ControlId Control { get; private set; }

    public ActionKind Kind
    {
        get => _kind;
        set
        {
            if (SetField(ref _kind, value))
            {
                ValidationMessage = null;
                RaiseVisibility();
            }
        }
    }

    public TargetKind Target
    {
        get => _target;
        set
        {
            if (SetField(ref _target, value))
            {
                ValidationMessage = null;
                RaiseVisibility();
            }
        }
    }

    public string AppName
    {
        get => _appName;
        set => SetField(ref _appName, value ?? string.Empty);
    }

    public MediaCommand Media
    {
        get => _media;
        set => SetField(ref _media, value);
    }

    public string HotkeyText
    {
        get => _hotkeyText;
        set => SetField(ref _hotkeyText, value ?? string.Empty);
    }

    public bool Invert
    {
        get => _invert;
        set => SetField(ref _invert, value);
    }

    public double MinPercent
    {
        get => _minPercent;
        set => SetField(ref _minPercent, Math.Clamp(value, 0, 100));
    }

    public double MaxPercent
    {
        get => _maxPercent;
        set => SetField(ref _maxPercent, Math.Clamp(value, 0, 100));
    }

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

    public bool IsAudio => _kind is ActionKind.SetVolume or ActionKind.ToggleMute;

    public bool IsVolume => _kind == ActionKind.SetVolume;

    public bool IsApplication => IsAudio && _target == TargetKind.Application;

    public bool IsMedia => _kind == ActionKind.Media;

    public bool IsHotkey => _kind == ActionKind.Hotkey;

    public bool IsSwitchDevice => _kind == ActionKind.SwitchDevice;

    public DeviceFlow SwitchFlow
    {
        get => _switchFlow;
        set
        {
            if (_switchFlow == value)
            {
                return;
            }

            CaptureSelection();
            _switchFlow = value;
            OnPropertyChanged();
            RebuildDeviceChoices();
        }
    }

    /// <summary>Tells the editor which devices exist right now (both kinds). Ticked devices stay ticked.</summary>
    public void SetAvailableDevices(IEnumerable<(string Id, string Name, DeviceFlow Flow)> devices)
    {
        CaptureSelection();
        _devices = devices.ToList();
        RebuildDeviceChoices();
    }

    public void Load(ControlId control, ControlMapping? mapping)
    {
        Control = control;
        OnPropertyChanged(nameof(Control));
        ValidationMessage = null;
        AppName = string.Empty;
        HotkeyText = string.Empty;
        Media = MediaCommand.PlayPause;
        Target = TargetKind.Speakers;
        Invert = false;
        MinPercent = 0;
        MaxPercent = 100;
        _selectedDeviceIds = [];
        _switchFlow = DeviceFlow.Render;

        switch (mapping?.Action)
        {
            case SetVolumeAction volume:
                LoadAudio(volume.Target);
                Invert = mapping.EffectiveScaling.Invert;
                MinPercent = Math.Round(mapping.EffectiveScaling.Min * 100);
                MaxPercent = Math.Round(mapping.EffectiveScaling.Max * 100);
                Kind = ActionKind.SetVolume;
                break;
            case ToggleMuteAction mute:
                LoadAudio(mute.Target);
                Kind = ActionKind.ToggleMute;
                break;
            case MediaAction media:
                Media = media.Command;
                Kind = ActionKind.Media;
                break;
            case HotkeyAction hotkey:
                HotkeyText = hotkey.Keys;
                Kind = ActionKind.Hotkey;
                break;
            case SwitchDeviceAction switchDevice:
                _switchFlow = switchDevice.Flow;
                _selectedDeviceIds = switchDevice.SelectedIds.ToList();
                Kind = ActionKind.SwitchDevice;
                break;
            default:
                Kind = ActionKind.None;
                break;
        }

        OnPropertyChanged(nameof(SwitchFlow));
        RebuildDeviceChoices();
        RaiseVisibility();
    }

    /// <summary>
    /// Builds the mapping from the current input. Returns true with a null mapping for "no action" (remove the binding);
    /// returns false and a readable message when the input is incomplete or invalid.
    /// </summary>
    public bool TryBuild(out ControlMapping? mapping, out string? error)
    {
        mapping = null;
        error = null;
        switch (_kind)
        {
            case ActionKind.None:
                break;
            case ActionKind.SetVolume:
            case ActionKind.ToggleMute:
                if (!TryBuildTarget(out AudioTarget? target, out error))
                {
                    break;
                }

                if (_kind == ActionKind.ToggleMute)
                {
                    mapping = new ControlMapping(Control, new ToggleMuteAction(target!));
                    break;
                }

                if (_minPercent > _maxPercent)
                {
                    error = Texts.UiEditorRangeInvalid;
                    break;
                }

                var scaling = new ValueScaling((float)(_minPercent / 100), (float)(_maxPercent / 100), _invert);
                bool isDefault = scaling is { Min: 0f, Max: 1f, Invert: false };
                mapping = new ControlMapping(Control, new SetVolumeAction(target!), isDefault ? null : scaling);
                break;
            case ActionKind.Media:
                mapping = new ControlMapping(Control, new MediaAction(_media));
                break;
            case ActionKind.SwitchDevice:
                CaptureSelection();
                IEnumerable<string> ids = _selectedDeviceIds.Where(id =>
                    _devices.FirstOrDefault(d => d.Id == id) is { } known ? known.Flow == _switchFlow : true);
                mapping = new ControlMapping(Control, new SwitchDeviceAction(_switchFlow, ids.ToList()));
                break;
            case ActionKind.Hotkey:
                if (Hotkey.TryParse(_hotkeyText, out Hotkey hotkey, out string hotkeyError))
                {
                    mapping = new ControlMapping(Control, new HotkeyAction(hotkey.ToString()));
                }
                else
                {
                    error = hotkeyError;
                }

                break;
        }

        ValidationMessage = error;
        return error is null;
    }

    private bool TryBuildTarget(out AudioTarget? target, out string? error)
    {
        error = null;
        target = null;
        switch (_target)
        {
            case TargetKind.Speakers:
                target = AudioTarget.Speakers;
                break;
            case TargetKind.Microphone:
                target = AudioTarget.Microphone;
                break;
            default:
                string name = _appName.Trim();
                if (name.Length == 0)
                {
                    error = Texts.UiEditorAppRequired;
                    return false;
                }

                target = AudioTarget.Application(name);
                break;
        }

        return true;
    }

    private void LoadAudio(AudioTarget target)
    {
        if (target.Kind == AudioTargetKind.Application)
        {
            Target = TargetKind.Application;
            AppName = target.App?.ExeName ?? string.Empty;
        }
        else
        {
            Target = target.Flow == DeviceFlow.Capture ? TargetKind.Microphone : TargetKind.Speakers;
        }
    }

    private void RaiseVisibility()
    {
        OnPropertyChanged(nameof(IsAudio));
        OnPropertyChanged(nameof(IsVolume));
        OnPropertyChanged(nameof(IsApplication));
        OnPropertyChanged(nameof(IsMedia));
        OnPropertyChanged(nameof(IsHotkey));
        OnPropertyChanged(nameof(IsSwitchDevice));
    }

    /// <summary>Remembers the ticked devices, including ones that are not connected right now (so an unplugged headset stays in the rotation).</summary>
    private void CaptureSelection()
    {
        if (DeviceChoices.Count == 0 && _devices.Count == 0)
        {
            return;
        }

        var shown = DeviceChoices.Select(c => c.Id).ToHashSet();
        var ticked = DeviceChoices.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        var kept = _selectedDeviceIds.Where(id => !shown.Contains(id)).ToList();
        _selectedDeviceIds = kept.Concat(ticked).Distinct().ToList();
    }

    private void RebuildDeviceChoices()
    {
        DeviceChoices.Clear();
        foreach ((string id, string name, DeviceFlow flow) in _devices.Where(d => d.Flow == _switchFlow).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            DeviceChoices.Add(new DeviceChoice(id, name, flow) { IsSelected = _selectedDeviceIds.Contains(id) });
        }

        OnPropertyChanged(nameof(HasNoDevices));
    }
}
