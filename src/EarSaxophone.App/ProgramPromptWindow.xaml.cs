using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.App;

/// <summary>
/// The question for a new program with sound (design V2, section 6): always on top, clickable, but never takes the focus
/// (WS_EX_NOACTIVATE), so games and other programs keep working. It closes itself when the view model is answered.
/// </summary>
public partial class ProgramPromptWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private const double ScreenMargin = 18;

    /// <summary>Distance from the top edge of the work area; keeps the window below the on-screen display.</summary>
    private const double TopOffsetBelowOsd = 120;

    private readonly ProgramPromptViewModel _viewModel;
    private bool _closing;

    public ProgramPromptWindow(ProgramPromptViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        viewModel.Closed += OnClosed;
    }

    /// <summary>Shows the window at the top right, below the on-screen display, without activating it.</summary>
    public void Present()
    {
        Show();
        UpdateLayout();
        Rect area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - ScreenMargin;
        Top = area.Top + TopOffsetBelowOsd;
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

    private void OnClosed(ProgramPromptAnswer answer)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Close();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
