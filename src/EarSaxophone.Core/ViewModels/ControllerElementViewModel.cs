using EarSaxophone.Core.Devices;
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
    private int _lastValue;

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

    public string Summary
    {
        get => _summary;
        set
        {
            if (SetField(ref _summary, value))
            {
                OnPropertyChanged(nameof(TooltipText));
                OnPropertyChanged(nameof(ListText));
            }
        }
    }

    public string TooltipText => $"{Label}: {_summary}";

    /// <summary>Text for the list of assignments, for example "Fader 1: Lautstärke: Discord.exe".</summary>
    public string ListText => $"{Label} · {_summary}";

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
