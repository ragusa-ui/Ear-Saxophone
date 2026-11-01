using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace EarSaxophone.App;

public partial class MainWindow : FluentWindow
{
    /// <summary>Accent colour of the whole window (the colour of the program icon, design V2).</summary>
    public static readonly Color Accent = Color.FromRgb(0x3F, 0xB6, 0xA8);

    private const string DragFormat = "EarSaxophone.ControllerElement";

    private readonly MainViewModel _viewModel;
    private ControllerElementViewModel? _pressed;
    private Point _pressedAt;
    private FrameworkElement? _dropTarget;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        ApplicationAccentColorManager.Apply(Accent, ApplicationTheme.Dark, false, false);
        DataContext = viewModel;
        InitializeComponent();
        Title = $"Ear Saxophone {AppInfo.Version}";
        WindowTitleBar.Title = Title;
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

    /// <summary>
    /// Drag feedback for one element of the picture: whether dropping <paramref name="source"/> on <paramref name="target"/>
    /// would change something; the element is marked as drop target when it would.
    /// </summary>
    internal bool PreviewMappingDrop(FrameworkElement tile, ControllerElementViewModel source, ControllerElementViewModel target, bool copy)
    {
        bool fits = source != target && _viewModel.CanTransfer(source.Control, target.Control, copy);
        ShowDropTarget(fits ? tile : null);
        return fits;
    }

    /// <summary>Drop of a dragged mapping: move (a mapped target swaps) or copy; returns the text of the status line.</summary>
    internal string DropMapping(ControllerElementViewModel source, ControllerElementViewModel target, bool copy)
    {
        ShowDropTarget(null);
        return _viewModel.TransferMapping(source, target, copy);
    }

    /// <summary>Marks one element as drop target (null = none).</summary>
    internal void ShowDropTarget(FrameworkElement? tile)
    {
        if (_dropTarget == tile)
        {
            return;
        }

        if (_dropTarget is not null)
        {
            DropTarget.SetIsActive(_dropTarget, false);
        }

        _dropTarget = tile;
        if (tile is not null)
        {
            DropTarget.SetIsActive(tile, true);
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Enter applies the open changes ("Übernehmen"), except where Enter has its own meaning.
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && !EnterHasOwnMeaning() && _viewModel.ApplyCommand.CanExecute(null))
        {
            _viewModel.ApplyCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
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

    private bool EnterHasOwnMeaning() =>
        ProfileNameBox.IsKeyboardFocusWithin
        || ProgramNameBox.IsKeyboardFocusWithin
        || Keyboard.FocusedElement is ComboBoxItem or ComboBox { IsDropDownOpen: true };

    private void Element_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = (sender as FrameworkElement)?.DataContext as ControllerElementViewModel;
        _pressedAt = e.GetPosition(this);
    }

    private void Element_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _pressed = null;

    private void Element_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is not { HasMapping: true } source || e.LeftButton != MouseButtonState.Pressed || sender is not DependencyObject origin)
        {
            return;
        }

        Vector moved = e.GetPosition(this) - _pressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pressed = null;
        try
        {
            DragDrop.DoDragDrop(origin, new DataObject(DragFormat, source), DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            ShowDropTarget(null);
        }
    }

    private void Element_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (sender is FrameworkElement { DataContext: ControllerElementViewModel target } tile
            && e.Data.GetData(DragFormat) is ControllerElementViewModel source)
        {
            bool copy = (e.KeyStates & DragDropKeyStates.ControlKey) != 0;
            if (PreviewMappingDrop(tile, source, target, copy))
            {
                e.Effects = copy ? DragDropEffects.Copy : DragDropEffects.Move;
            }
        }
    }

    private void Element_DragLeave(object sender, DragEventArgs e)
    {
        if (sender == _dropTarget)
        {
            ShowDropTarget(null);
        }
    }

    private void Element_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        ShowDropTarget(null);
        if (sender is FrameworkElement { DataContext: ControllerElementViewModel target }
            && e.Data.GetData(DragFormat) is ControllerElementViewModel source && source != target)
        {
            DropMapping(source, target, (e.KeyStates & DragDropKeyStates.ControlKey) != 0);
        }
    }

    private void ProfileNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox box)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
            e.Handled = true;
        }
    }

    private void ProgramNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.Editor.AddAppCommand.CanExecute(null))
        {
            _viewModel.Editor.AddAppCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>An entry of the "Hinzufügen" menu was chosen: the menu closes (the entry's command does the work).</summary>
    private void AddTargetItem_Click(object sender, RoutedEventArgs e) => AddTargetButton.IsChecked = false;

    /// <summary>"Datei öffnen …": any program file; its file name becomes the program identity (also for programs that are not running).</summary>
    private void OpenProgramFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Texts.UiChooseProgram,
            Filter = Texts.UiOpenProgramFileFilter,
            CheckFileExists = true,
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (string file in dialog.FileNames)
        {
            _viewModel.Editor.AddProgramFile(file);
        }
    }
}
