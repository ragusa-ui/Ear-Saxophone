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
        sb.AppendLine($"Window: {window.ActualWidth:0}x{window.ActualHeight:0}, title '{window.Title}', minimum {window.MinWidth:0}x{window.MinHeight:0}");
        sb.AppendLine($"Screen: {SystemParameters.PrimaryScreenWidth:0}x{SystemParameters.PrimaryScreenHeight:0}, work area {SystemParameters.WorkArea.Width:0}x{SystemParameters.WorkArea.Height:0}");

        sb.AppendLine("== Theme resources used in XAML");
        foreach (string key in ResourceKeys)
        {
            object? resource = window.TryFindResource(key);
            string detail = resource is SolidColorBrush brush ? brush.Color.ToString() : resource?.GetType().Name ?? "MISSING";
            sb.AppendLine($"{(resource is null ? "!!" : "ok")} {key} = {detail}");
        }

        sb.AppendLine("== Visible elements (x,y wxh) text");
        List<Found> found = AppendElements(window, sb);

        sb.AppendLine($"== Elements outside the window (not inside a ScrollViewer): {found.Count(f => f.Outside)}");
        AppendOverlaps(found, sb);

        double headerBottom = window.FindName("HeaderBar") is FrameworkElement header && header.IsVisible
            ? header.TransformToAncestor(window).TransformBounds(new Rect(0, 0, header.ActualWidth, header.ActualHeight)).Bottom
            : 110;
        sb.AppendLine($"== Header area (y < {headerBottom:0})");
        foreach (Found item in found.Where(f => f.Bounds.Y < headerBottom))
        {
            sb.AppendLine($"   ({item.Bounds.X:0},{item.Bounds.Y:0} {item.Bounds.Width:0}x{item.Bounds.Height:0}) {item.Description}");
        }

        // The popups (profile menu, settings behind the gear) are closed in the screenshot; open them for a moment and list their content.
        foreach (string name in new[] { "ProfilePopup", "SettingsPopup" })
        {
            if (window.FindName(name) is not Popup { Child: FrameworkElement child } popup)
            {
                sb.AppendLine($"!! Popup '{name}' not found");
                continue;
            }

            bool stays = popup.StaysOpen;
            try
            {
                popup.StaysOpen = true;
                popup.IsOpen = true;
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                child.UpdateLayout();
                sb.AppendLine($"== Popup '{name}' (open={popup.IsOpen}, size {child.ActualWidth:0}x{child.ActualHeight:0}; positions relative to the popup)");
                AppendOverlaps(AppendElements(child, sb), sb);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"!! Popup '{name}' could not be listed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                popup.IsOpen = false;
                popup.StaysOpen = stays;
            }
        }

        AppendPixels(bitmap, sb, DeviceArea(window, bitmap));
        return sb.ToString();
    }

    private static List<Found> AppendElements(FrameworkElement root, StringBuilder sb)
    {
        var found = new List<Found>();
        Walk(root, root, found);
        foreach (Found item in found)
        {
            sb.AppendLine($"{(item.Outside ? "!!" : "  ")} ({item.Bounds.X:0},{item.Bounds.Y:0} {item.Bounds.Width:0}x{item.Bounds.Height:0}) {item.Description}");
        }

        return found;
    }

    private static void AppendOverlaps(List<Found> found, StringBuilder sb)
    {
        var overlaps = new List<string>();
        for (int i = 0; i < found.Count; i++)
        {
            for (int j = i + 1; j < found.Count; j++)
            {
                Rect common = Rect.Intersect(found[i].Bounds, found[j].Bounds);
                bool nested = found[i].Element.IsAncestorOf(found[j].Element) || found[j].Element.IsAncestorOf(found[i].Element);
                if (!common.IsEmpty && common.Width * common.Height > 4 && !nested)
                {
                    overlaps.Add($"!! OVERLAP {found[i].Description} ({found[i].Bounds.X:0},{found[i].Bounds.Y:0} {found[i].Bounds.Width:0}x{found[i].Bounds.Height:0})  <->  {found[j].Description} ({found[j].Bounds.X:0},{found[j].Bounds.Y:0} {found[j].Bounds.Width:0}x{found[j].Bounds.Height:0})");
                }
            }
        }

        sb.AppendLine($"== Overlapping elements: {overlaps.Count}");
        overlaps.ForEach(o => sb.AppendLine(o));
    }

    /// <summary>Pixel area of the device picture (falls back to the left part of the window).</summary>
    private static Int32Rect DeviceArea(Window window, BitmapSource bitmap)
    {
        Rect area = window.FindName("DeviceView") is FrameworkElement view && view.IsVisible
            ? view.TransformToAncestor(window).TransformBounds(new Rect(0, 0, view.ActualWidth, view.ActualHeight))
            : new Rect(0, 100, bitmap.PixelWidth * 0.7, bitmap.PixelHeight * 0.75);
        area.Intersect(new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        return area.IsEmpty || area.Width < 12 || area.Height < 24
            ? new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)
            : new Int32Rect((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
    }

    private sealed record Found(FrameworkElement Element, Rect Bounds, string Description, bool Outside);

    private static void Walk(DependencyObject node, FrameworkElement root, List<Found> found)
    {
        if (node is FrameworkElement element && element.IsVisible && Describe(element) is { } description)
        {
            Rect bounds = element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            bool scrolled = false;
            for (DependencyObject? parent = VisualTreeHelper.GetParent(element); parent is not null && parent != root; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is ScrollViewer viewer)
                {
                    // Only the part inside the scroll viewer's viewport is visible; the rest is clipped.
                    Rect viewport = viewer.TransformToAncestor(root).TransformBounds(new Rect(0, 0, viewer.ViewportWidth, viewer.ViewportHeight));
                    bounds = Rect.Intersect(bounds, viewport);
                    scrolled = true;
                    break;
                }
            }

            if (bounds.IsEmpty)
            {
                goto children; // scrolled out of view: not visible
            }

            bool outside = !scrolled
                && (bounds.Right > root.ActualWidth + 1 || bounds.Bottom > root.ActualHeight + 1 || bounds.Left < -1 || bounds.Top < -1);
            found.Add(new Found(element, bounds, description, outside));
        }

        children:
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), root, found);
        }
    }

    private static string? Describe(FrameworkElement element) => element switch
    {
        Button { DataContext: EarSaxophone.Core.ViewModels.ControllerElementViewModel e } =>
            $"Element '{e.Label}' {e.Kind}{(e.HasMapping ? $" belegt [{e.BadgeKind} x{e.ActionCount}]" : string.Empty)}{(e.IsSelected ? " gewaehlt" : string.Empty)}",
        TextBlock { Text: "nicht belegt" } => null,
        TextBlock { Text.Length: > 0 } glyph when glyph.Text.All(c => c is >= '\uE000' and <= '\uF8FF') => null, // icon-font glyphs (symbols, scroll bar arrows)
        TextBlock { Text.Length: > 0 } t when IsLeafText(t) => $"Text{Id(t)} '{Trim(t.Text)}' size {t.FontSize:0.#}",
        ProgressBar p => $"ProgressBar{Id(p)} {p.Value:0.00}",
        Slider s => $"Slider{Id(s)} {s.Value:0}",
        ComboBox c => $"ComboBox{Id(c)} '{Trim(c.Text)}'",
        ListBox l => $"ListBox{Id(l)} items={l.Items.Count} selected='{Trim(l.SelectedItem?.ToString() ?? string.Empty)}'",
        TextBox t => $"TextBox{Id(t)} '{Trim(t.Text)}'",
        ToggleButton { Content: string text } toggle => $"{toggle.GetType().Name}{Id(toggle)} '{text}' checked={toggle.IsChecked}",
        ToggleButton toggle when Id(toggle).Length > 0 => $"{toggle.GetType().Name}{Id(toggle)} checked={toggle.IsChecked}",
        ButtonBase { Content: string text } b => $"Button{Id(b)} '{text}' enabled={b.IsEnabled}",
        ButtonBase b when Id(b).Length > 0 => $"Button{Id(b)} (Symbol) enabled={b.IsEnabled}",
        Border b when Id(b) is " #ApplyBar" or " #ErrorBar" or " #LearnBanner" or " #PendingBanner" or " #UpdateBanner" or " #SidePanel" => $"Border{Id(b)}",
        _ => null,
    };

    private static string Id(FrameworkElement element) =>
        System.Windows.Automation.AutomationProperties.GetAutomationId(element) is { Length: > 0 } id ? $" #{id}" : string.Empty;

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

    private static void AppendPixels(BitmapSource bitmap, StringBuilder sb, Int32Rect device)
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
        AppendAscii(pixels, width, height, 0, 0, width, height, 10, 20, "whole window, 10x20 px per character", sb);
        AppendAscii(pixels, width, height, device.X, device.Y, device.X + device.Width, device.Y + device.Height, 6, 12,
            $"device picture area at {device.X},{device.Y} {device.Width}x{device.Height}, 6x12 px per character", sb);
    }

    private static void AppendAscii(byte[] pixels, int width, int height, int x0, int y0, int x1, int y1, int cellWidth, int cellHeight, string title, StringBuilder sb)
    {
        sb.AppendLine($"== ASCII preview ({title}; ' .:-=+*#%@' = darker to brighter; 'A' accent/teal, 'R' red)");
        const string ramp = " .:-=+*#%@";
        for (int cy = y0; cy < y1; cy += cellHeight)
        {
            var line = new StringBuilder();
            for (int cx = x0; cx < x1; cx += cellWidth)
            {
                double r = 0, g = 0, b = 0;
                int n = 0;
                for (int y = cy; y < Math.Min(cy + cellHeight, y1); y += 2)
                {
                    for (int x = cx; x < Math.Min(cx + cellWidth, x1); x += 2)
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
