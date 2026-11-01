using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.App;

/// <summary>
/// The question for a new program with sound (design V2, section 6): always on top, clickable, but never takes the focus
/// (WS_EX_NOACTIVATE), so games and other programs keep working. It slides in with a teal glow that pulses a few times,
/// shows a countdown bar until it closes itself, and optionally plays a short system sound. It closes itself when the
/// view model is answered.
/// </summary>
public partial class ProgramPromptWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private const double ScreenMargin = 18;

    /// <summary>Distance from the top edge of the work area when the on-screen display sits at the top (stays below it).</summary>
    private const double TopOffsetBelowOsd = 170;

    /// <summary>Distance from the top edge of the work area when the on-screen display sits at the bottom.</summary>
    private const double TopOffset = 48;

    /// <summary>Horizontal centre of the window as a share of the work area width ("mittig-rechts").</summary>
    private const double HorizontalCentre = 0.64;

    private readonly ProgramPromptViewModel _viewModel;
    private bool _closing;

    public ProgramPromptWindow(ProgramPromptViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        viewModel.Closed += OnClosed;
        LoadProgramIcon(viewModel.ExePath);
    }

    /// <summary>Shows the window at the top, centre-right and clear of the on-screen display, without activating it.</summary>
    public void Present(OsdPosition osdPosition)
    {
        Show();
        UpdateLayout();
        Rect area = SystemParameters.WorkArea;
        double left = area.Left + (area.Width * HorizontalCentre) - (ActualWidth / 2);
        Left = Math.Max(area.Left + ScreenMargin, Math.Min(left, area.Right - ActualWidth - ScreenMargin));
        bool osdAtTop = osdPosition is OsdPosition.TopLeft or OsdPosition.TopCenter or OsdPosition.TopRight;
        Top = area.Top + (osdAtTop ? TopOffsetBelowOsd : TopOffset);
        Animate();
        if (_viewModel.PlaySound)
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExNoActivate | WsExToolWindow);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Closed -= OnClosed;
        base.OnClosed(e);
    }

    /// <summary>Slide-in and fade-in, a glow that pulses three times, and the countdown bar.</summary>
    private void Animate()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        SlideTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(36, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });

        var pulse = new DoubleAnimation(0.55, 1.0, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3),
            BeginTime = TimeSpan.FromMilliseconds(250),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, pulse);
        var blur = new DoubleAnimation(18, 30, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3),
            BeginTime = TimeSpan.FromMilliseconds(250),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, blur);

        CountdownScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0, _viewModel.AutoDismissDuration));
    }

    /// <summary>Shows the program's own icon; keeps the first-letter fallback when the file or its icon cannot be read.</summary>
    private void LoadProgramIcon(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            return;
        }

        var info = new ShFileInfo();
        IntPtr result = SHGetFileInfo(exePath, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return;
        }

        try
        {
            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            ProgramIcon.Source = source;
            ProgramIcon.Visibility = Visibility.Visible;
            IconFallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            // Keep the first-letter fallback.
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private void OnClosed(ProgramPromptAnswer answer)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Close();
    }

    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiLargeIcon = 0x0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShFileInfo info, uint size, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
