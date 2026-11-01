using System.Globalization;
using System.Windows;
using System.Windows.Data;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;
using Wpf.Ui.Controls;

namespace EarSaxophone.App;

/// <summary>Symbol of an action kind (badges in the picture, kind picker in the side panel).</summary>
public sealed class ActionKindSymbolConverter : IValueConverter
{
    public static SymbolRegular SymbolOf(ActionKind kind) => kind switch
    {
        ActionKind.SetVolume => SymbolRegular.Speaker220,
        ActionKind.Mute => SymbolRegular.SpeakerMute20,
        ActionKind.Media => SymbolRegular.MusicNote220,
        ActionKind.Hotkey => SymbolRegular.Keyboard20,
        ActionKind.SwitchDevice => SymbolRegular.ArrowSwap20,
        _ => SymbolRegular.Prohibited20,
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ActionKind kind ? SymbolOf(kind) : SymbolRegular.Prohibited20;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Symbol of an entry in the target list.</summary>
public sealed class TargetSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        TargetSymbol.Speakers => SymbolRegular.Speaker220,
        TargetSymbol.Microphone => SymbolRegular.Mic20,
        TargetSymbol.Device => SymbolRegular.DeviceEq20,
        TargetSymbol.Strip => SymbolRegular.Link20,
        _ => SymbolRegular.AppGeneric20,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Negates a bool (for example "pinned" = not following the device).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>Collapsed for true, visible for false.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Where the dot sits in the small screen symbol of an overlay position (parameter "H" or "V").</summary>
public sealed class OsdPositionAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var position = value is OsdPosition p ? p : OsdPosition.BottomRight;
        if (parameter as string == "V")
        {
            return position is OsdPosition.TopLeft or OsdPosition.TopCenter or OsdPosition.TopRight
                ? VerticalAlignment.Top
                : VerticalAlignment.Bottom;
        }

        return position switch
        {
            OsdPosition.TopLeft or OsdPosition.BottomLeft => HorizontalAlignment.Left,
            OsdPosition.TopCenter or OsdPosition.BottomCenter => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Right,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
