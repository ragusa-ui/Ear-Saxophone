using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Settings;

namespace EarSaxophone.App;

/// <summary>
/// Small on-screen display for volume changes and device switches. It is always on top, ignores the mouse (clicks go to the window
/// below) and never takes the keyboard focus, so it does not disturb games or other programs.
/// </summary>
public partial class OsdWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private const double ScreenMargin = 18;

    private static readonly Brush ActiveFill = new SolidColorBrush(Color.FromRgb(0x3F, 0xB6, 0xA8));
    private static readonly Brush MutedFill = new SolidColorBrush(Color.FromRgb(0x6B, 0x73, 0x90));

    private readonly DispatcherTimer _hideTimer = new();
    private int _generation;

    public OsdWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            FadeOut();
        };
    }

    public string TitleValue => TitleText.Text;

    public string ValueTextValue => ValueText.Text;

    public string DetailValue => DetailText.Text;

    /// <summary>Extended window style bits, to verify click-through and no-activate behaviour.</summary>
    public int ExtendedStyle => GetWindowLong(new WindowInteropHelper(this).Handle, GwlExStyle);

    public bool IsClickThroughAndNoFocus =>
        (ExtendedStyle & (WsExTransparent | WsExNoActivate)) == (WsExTransparent | WsExNoActivate);

    /// <summary>Shows the message and (re)starts the hide timer. Calling it again while visible just updates the content.</summary>
    public void Present(OsdMessage message, OsdPosition position, TimeSpan duration)
    {
        TitleText.Text = message.Title;
        DetailText.Text = message.Detail ?? string.Empty;
        DetailText.Visibility = string.IsNullOrEmpty(message.Detail) ? Visibility.Collapsed : Visibility.Visible;

        bool hasBar = message.Level is not null;
        BarHost.Visibility = hasBar ? Visibility.Visible : Visibility.Collapsed;
        ValueText.Text = message.Muted && hasBar ? Texts.OsdMuted : hasBar ? $"{Math.Round(message.Level!.Value * 100)} %" : string.Empty;
        Fill.Width = BarHost.Width * Math.Clamp(message.Level ?? 0, 0, 1);
        Fill.Background = message.Muted ? MutedFill : ActiveFill;
        ValueText.Foreground = message.Muted ? MutedFill : ActiveFill;

        _generation++;
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }

        UpdateLayout();
        Place(position);
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(110)));
        _hideTimer.Interval = duration;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    /// <summary>Hides immediately (used when the display is switched off).</summary>
    public void HideNow()
    {
        _hideTimer.Stop();
        _generation++;
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        if (IsVisible)
        {
            Hide();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExTransparent | WsExNoActivate | WsExToolWindow);
    }

    private void Place(OsdPosition position)
    {
        Rect area = SystemParameters.WorkArea; // device-independent units of the primary screen without the taskbar
        double width = ActualWidth;
        double height = ActualHeight;
        Left = position switch
        {
            OsdPosition.TopLeft or OsdPosition.BottomLeft => area.Left + ScreenMargin,
            OsdPosition.TopCenter or OsdPosition.BottomCenter => area.Left + (area.Width - width) / 2,
            _ => area.Right - width - ScreenMargin,
        };
        Top = position switch
        {
            OsdPosition.TopLeft or OsdPosition.TopCenter or OsdPosition.TopRight => area.Top + ScreenMargin,
            _ => area.Bottom - height - ScreenMargin,
        };
    }

    private void FadeOut()
    {
        int generation = _generation;
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
        animation.Completed += (_, _) =>
        {
            if (generation == _generation && IsVisible)
            {
                Hide();
            }
        };
        BeginAnimation(OpacityProperty, animation);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
