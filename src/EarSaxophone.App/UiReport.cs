using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EarSaxophone.App;

/// <summary>
/// CI only (--demo-screenshot): a text report of what the rendered window contains, so the layout can be checked
/// without looking at the picture: missing theme resources, every visible text/control with its position and size,
/// elements outside the window, and a coarse ASCII preview.
/// </summary>
internal static class UiReport
{
    private static readonly string[] ResourceKeys =
    [
        "ApplicationBackgroundBrush", "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush", "TextFillColorTertiaryBrush",
        "CardBackgroundFillColorDefaultBrush", "CardStrokeColorDefaultBrush", "ControlFillColorSecondaryBrush",
        "ControlStrongStrokeColorDefaultBrush", "SystemAccentColorPrimaryBrush", "SystemFillColorAttentionBackgroundBrush",
        "SystemFillColorCriticalBackgroundBrush",
    ];

    public static string Build(Window window, BitmapSource bitmap)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Window: {window.ActualWidth:0}x{window.ActualHeight:0}, title '{window.Title}'");
        sb.AppendLine($"Screen: {SystemParameters.PrimaryScreenWidth:0}x{SystemParameters.PrimaryScreenHeight:0}, work area {SystemParameters.WorkArea.Width:0}x{SystemParameters.WorkArea.Height:0}");

        sb.AppendLine("== Theme resources used in XAML");
        foreach (string key in ResourceKeys)
        {
            object? resource = window.TryFindResource(key);
            string detail = resource is SolidColorBrush brush ? brush.Color.ToString() : resource?.GetType().Name ?? "MISSING";
            sb.AppendLine($"{(resource is null ? "!!" : "ok")} {key} = {detail}");
        }

        sb.AppendLine("== Visible elements (x,y wxh) text");
        int outside = 0;
        Walk(window, window, sb, ref outside);
        sb.AppendLine($"== Elements outside the window (not inside a ScrollViewer): {outside}");

        AppendPixels(bitmap, sb);
        return sb.ToString();
    }

    private static void Walk(DependencyObject node, Window window, StringBuilder sb, ref int outside)
    {
        if (node is FrameworkElement element && element.IsVisible && Describe(element) is { } description)
        {
            Rect bounds = element.TransformToAncestor(window).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            bool inScroll = IsInsideScrollViewer(element);
            bool isOutside = !inScroll && (bounds.Right > window.ActualWidth + 1 || bounds.Bottom > window.ActualHeight + 1 || bounds.Left < -1 || bounds.Top < -1);
            outside += isOutside ? 1 : 0;
            sb.AppendLine($"{(isOutside ? "!!" : "  ")} ({bounds.X:0},{bounds.Y:0} {bounds.Width:0}x{bounds.Height:0}) {description}");
        }

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), window, sb, ref outside);
        }
    }

    private static string? Describe(FrameworkElement element) => element switch
    {
        TextBlock { Text: "nicht belegt" } => null,
        TextBlock { Text.Length: > 0 } t when IsLeafText(t) => $"Text '{Trim(t.Text)}' size {t.FontSize:0.#}",
        ProgressBar p => $"ProgressBar {p.Value:0.00}",
        Slider s => $"Slider {s.Value:0}",
        ComboBox c => $"ComboBox '{Trim(c.Text)}'",
        TextBox t => $"TextBox '{Trim(t.Text)}'",
        ToggleButton { Content: string text } toggle => $"{toggle.GetType().Name} '{text}' checked={toggle.IsChecked}",
        ButtonBase { Content: string text } b => $"Button '{text}' enabled={b.IsEnabled}",
        _ => null,
    };

    // Texts that are part of a template of another control (e.g. inside a ComboBox or a Button label) are reported by that control.
    private static bool IsLeafText(TextBlock text)
    {
        for (DependencyObject? parent = VisualTreeHelper.GetParent(text); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ComboBox or TextBox or ButtonBase { Content: string })
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInsideScrollViewer(DependencyObject element)
    {
        for (DependencyObject? parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer)
            {
                return true;
            }
        }

        return false;
    }

    private static string Trim(string text) => text.Length > 48 ? text[..48] + "…" : text;

    private static void AppendPixels(BitmapSource bitmap, StringBuilder sb)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);

        double Luminance(int x, int y)
        {
            int i = (y * width + x) * 4;
            return (0.2126 * pixels[i + 2] + 0.7152 * pixels[i + 1] + 0.0722 * pixels[i]) / 255.0;
        }

        long bright = 0;
        double sum = 0;
        for (int y = 0; y < height; y += 2)
        {
            for (int x = 0; x < width; x += 2)
            {
                double l = Luminance(x, y);
                sum += l;
                bright += l > 0.8 ? 1 : 0;
            }
        }

        double samples = Math.Ceiling(height / 2.0) * Math.Ceiling(width / 2.0);
        sb.AppendLine($"== Pixels: average brightness {sum / samples:0.00} (0 = black, 1 = white), share of near-white pixels {bright / samples:P1}");
        sb.AppendLine("== ASCII preview (10x20 px per character; ' .:-=+*#%@' = darker to brighter; 'A' accent/teal, 'R' red)");

        const string ramp = " .:-=+*#%@";
        for (int cy = 0; cy < height; cy += 20)
        {
            var line = new StringBuilder();
            for (int cx = 0; cx < width; cx += 10)
            {
                double r = 0, g = 0, b = 0;
                int n = 0;
                for (int y = cy; y < Math.Min(cy + 20, height); y += 4)
                {
                    for (int x = cx; x < Math.Min(cx + 10, width); x += 2)
                    {
                        int i = (y * width + x) * 4;
                        b += pixels[i];
                        g += pixels[i + 1];
                        r += pixels[i + 2];
                        n++;
                    }
                }

                r /= n * 255.0;
                g /= n * 255.0;
                b /= n * 255.0;
                double l = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                if (g > r + 0.12 && b > r + 0.05)
                {
                    line.Append('A');
                }
                else if (r > g + 0.25 && r > b + 0.25)
                {
                    line.Append('R');
                }
                else
                {
                    line.Append(ramp[Math.Min(ramp.Length - 1, (int)(l * ramp.Length * 1.6))]);
                }
            }

            sb.AppendLine(line.ToString().TrimEnd());
        }
    }
}
