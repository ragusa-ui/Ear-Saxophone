using System.ComponentModel;
using EarSaxophone.Core.ViewModels;
using Wpf.Ui.Controls;

namespace EarSaxophone.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>Shows the window again (from the tray or when a second instance was started) and brings it to the front.</summary>
    public void ShowAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == System.Windows.WindowState.Minimized)
        {
            WindowState = System.Windows.WindowState.Normal;
        }

        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // With "stay in tray" the close button only hides the window; the tray menu quits the program.
        if (!App.Instance.IsExiting && _viewModel.KeepInTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
        if (!e.Cancel)
        {
            App.Instance.Quit();
        }
    }
}
