using System.Windows;
using System.Windows.Media;
using EarSaxophone.Core.Settings;
using Wpf.Ui.Appearance;

namespace EarSaxophone.App;

/// <summary>
/// Applies one of the visual styles: swaps the style dictionary (<c>Themes/Theme.*.xaml</c>, referenced everywhere with
/// DynamicResource) and tells WPF-UI whether the base is light or dark.
/// </summary>
public static class StyleManager
{
    private const string ThemePrefix = "Themes/Theme.";

    private static readonly (ApplicationTheme Base, Color Accent) Fallback = (ApplicationTheme.Dark, Color.FromRgb(0x3F, 0xB6, 0xA8));

    public static ApplicationTheme BaseOf(AppStyle style) => style is AppStyle.Papier or AppStyle.Minimal or AppStyle.Rack
        ? ApplicationTheme.Light
        : ApplicationTheme.Dark;

    public static void Apply(AppStyle style)
    {
        ApplicationTheme baseTheme = BaseOf(style);
        ApplicationThemeManager.Apply(baseTheme);

        var merged = Application.Current.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Source?.OriginalString.Contains(ThemePrefix, StringComparison.Ordinal) == true)
            {
                merged.RemoveAt(i);
            }
        }

        // Added last so its values win over the WPF-UI system colours of the same name.
        var dictionary = new ResourceDictionary { Source = new Uri($"pack://application:,,,/{ThemePrefix}{style}.xaml", UriKind.Absolute) };
        merged.Add(dictionary);

        Color accent = dictionary["AccentColor"] is Color c ? c : Fallback.Accent;
        ApplicationAccentColorManager.Apply(accent, baseTheme, false, false);
    }
}
