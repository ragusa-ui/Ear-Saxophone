namespace EarSaxophone.Core.ViewModels;

using EarSaxophone.Core.Midi;

/// <summary>One tile of the controller overview (a knob, fader or button).</summary>
public sealed class ControllerElementViewModel(ControlId control, string label) : ObservableObject
{
    private string _summary = string.Empty;
    private bool _hasMapping;
    private bool _isContinuous;
    private bool _isSelected;
    private int _lastValue;

    public ControlId Control { get; } = control;

    public string Label { get; } = label;

    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    public bool HasMapping
    {
        get => _hasMapping;
        set => SetField(ref _hasMapping, value);
    }

    /// <summary>True when the mapped action follows the control position (volume), so a level bar makes sense.</summary>
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
            }
        }
    }

    public double LevelFraction => _lastValue / 127.0;
}

public sealed class ElementRowViewModel(string title, IReadOnlyList<ControllerElementViewModel> elements)
{
    public string Title { get; } = title;

    public IReadOnlyList<ControllerElementViewModel> Elements { get; } = elements;

    /// <summary>Strips have 8 columns; the longer transport row is wrapped into two rows of equal width.</summary>
    public int Columns => Elements.Count <= 8 ? Elements.Count : (Elements.Count + 1) / 2;
}
