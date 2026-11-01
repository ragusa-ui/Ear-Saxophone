using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.ViewModels;

/// <summary>One control shown in the picture of the device (a knob, fader or button), or an additional control found by MIDI-Learn.</summary>
public sealed class ControllerElementViewModel : ObservableObject
{
    /// <summary>How far the fader cap can move inside the fader track (design units).</summary>
    public const double FaderTravel = 105;

    private string _summary = string.Empty;
    private bool _hasMapping;
    private bool _isContinuous;
    private bool _isSelected;
    private bool _isMuteLit;
    private bool _isLedBlinkOff;
    private int _lastValue;
    private ControlShape _shape;
    private MappingBadge _badge = MappingBadge.Empty;

    public ControllerElementViewModel(LayoutElement layout)
    {
        Control = layout.Control;
        Label = layout.Label;
        Kind = layout.Kind;
        X = layout.X;
        Y = layout.Y;
        Width = layout.Width;
        Height = layout.Height;
        Glyph = layout.Glyph;
        Caption = layout.Caption;
        GlyphColor = layout.GlyphColor;
        HasLed = Nanokontrol2Leds.HasLed(layout.Control);
        _shape = layout.Shape;
    }

    /// <summary>Additional control that is not part of the standard layout (found by MIDI-Learn).</summary>
    public ControllerElementViewModel(ControlId control, string label)
    {
        Control = control;
        Label = label;
        Kind = ElementKind.Button;
        Width = 150;
        Height = 40;
        GlyphColor = "#DDE1EE";
        _shape = ControlShape.Unknown;
    }

    public ControlId Control { get; }

    public string Label { get; }

    public ElementKind Kind { get; }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public string Glyph { get; } = string.Empty;

    public string Caption { get; } = string.Empty;

    public string GlyphColor { get; }

    /// <summary>The key has an LED on the device (S, M, R, transport keys and Cycle; see <see cref="Nanokontrol2Leds"/>).</summary>
    public bool HasLed { get; }

    /// <summary>
    /// The LED of this key is lit in the picture ("muted-lit", steady or blinking): the key carries a mute action and its target
    /// (group) is muted right now, the same rule as the LEDs on the device (<see cref="LedController.IsLit"/>). Independent of the
    /// blink phase (<see cref="IsLedBlinkOff"/>). Set by <see cref="MainViewModel"/>.
    /// </summary>
    public bool IsMuteLit
    {
        get => _isMuteLit;
        set
        {
            if (SetField(ref _isMuteLit, value))
            {
                OnPropertyChanged(nameof(IsLedShownOn));
            }
        }
    }

    /// <summary>
    /// The muted-lit LED is in the dark half of its blink rhythm right now (only while <see cref="IsMuteLit"/> and blinking is on).
    /// The picture dims the lit LED then; <see cref="IsMuteLit"/> stays true. Set by <see cref="MainViewModel"/>.
    /// </summary>
    public bool IsLedBlinkOff
    {
        get => _isLedBlinkOff;
        set
        {
            if (SetField(ref _isLedBlinkOff, value))
            {
                OnPropertyChanged(nameof(IsLedShownOn));
            }
        }
    }

    /// <summary>What the LED shows at this moment: muted-lit and not in the dark blink phase.</summary>
    public bool IsLedShownOn => _isMuteLit && !_isLedBlinkOff;

    public string Summary
    {
        get => _summary;
        set
        {
            if (SetField(ref _summary, value))
            {
                OnPropertyChanged(nameof(TooltipText));
            }
        }
    }

    public string TooltipText => $"{Label}: {_summary}";

    /// <summary>Stepless (fader, knob), key, or not known yet (an additional control that has not shown its behaviour).</summary>
    public ControlShape Shape
    {
        get => _shape;
        set
        {
            if (SetField(ref _shape, value))
            {
                OnPropertyChanged(nameof(IsStepless));
            }
        }
    }

    /// <summary>True for faders and knobs (the controls a program's volume can be put on).</summary>
    public bool IsStepless => _shape == ControlShape.Continuous;

    /// <summary>Badge in the picture: symbol ids of the action kinds and the number of actions.</summary>
    public MappingBadge Badge
    {
        get => _badge;
        private set
        {
            if (SetField(ref _badge, value))
            {
                OnPropertyChanged(nameof(BadgeKind));
                OnPropertyChanged(nameof(ActionCount));
                OnPropertyChanged(nameof(ShowsActionCount));
            }
        }
    }

    /// <summary>Symbol id of the first action (<see cref="ActionKind.None"/> when unmapped).</summary>
    public ActionKind BadgeKind => _badge.PrimaryKind;

    public int ActionCount => _badge.ActionCount;

    public bool ShowsActionCount => _badge.ShowsCount;

    /// <summary>Updates summary, badge and flags from the mapping of this control (null = unmapped).</summary>
    public void ShowMapping(ControlMapping? mapping)
    {
        HasMapping = mapping is not null;
        IsContinuous = mapping?.HasContinuousAction ?? false;
        Summary = MappingDescriber.Describe(mapping);
        Badge = MappingBadge.From(mapping);
    }

    public bool HasMapping
    {
        get => _hasMapping;
        set => SetField(ref _hasMapping, value);
    }

    /// <summary>True when the mapped action follows the control position (volume).</summary>
    public bool IsContinuous
    {
        get => _isContinuous;
        set => SetField(ref _isContinuous, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>Last MIDI value seen from this control (0-127), for live feedback.</summary>
    public int LastValue
    {
        get => _lastValue;
        set
        {
            if (SetField(ref _lastValue, value))
            {
                OnPropertyChanged(nameof(LevelFraction));
                OnPropertyChanged(nameof(KnobAngle));
                OnPropertyChanged(nameof(CapOffset));
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    public double LevelFraction => _lastValue / 127.0;

    /// <summary>Pointer angle of a knob in degrees (-135 at minimum to +135 at maximum).</summary>
    public double KnobAngle => -135 + 270 * LevelFraction;

    /// <summary>How far the fader cap is moved down from its top position (0 = fader at maximum).</summary>
    public double CapOffset => (1 - LevelFraction) * FaderTravel;

    /// <summary>A button is lit while it is held down (value 64 or more).</summary>
    public bool IsActive => _lastValue >= Mapping.MappingDispatcher.PressThreshold;
}
