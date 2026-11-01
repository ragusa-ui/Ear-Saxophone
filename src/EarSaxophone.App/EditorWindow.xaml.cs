using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.ViewModels;
using Wpf.Ui.Controls;

namespace EarSaxophone.App;

/// <summary>
/// The second window: what is assigned to the selected control of the device. It opens next to the device window as soon as
/// a control is clicked or moved, and shows the same view model as the main window.
/// </summary>
public partial class EditorWindow : FluentWindow
{
    private const double Gap = 8;

    private readonly MainViewModel _viewModel;

    public EditorWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        ShowActivated = false;
    }

    /// <summary>Right of <paramref name="main"/>; left of it or against the screen edge when the work area has no room.</summary>
    internal void PlaceNextTo(Window main)
    {
        Rect area = SystemParameters.WorkArea;
        double left = area.Left, top = area.Top, right = area.Right, bottom = area.Bottom;

        double height = Math.Min(Math.Max(main.ActualHeight, 640), bottom - top);
        Height = height;
        double x = main.Left + main.ActualWidth + Gap;
        if (x + Width > right)
        {
            x = main.Left - Width - Gap;
        }

        if (x < left)
        {
            x = Math.Max(left, right - Width);
        }

        Left = x;
        Top = Math.Min(Math.Max(top, main.Top), bottom - height);
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

    private bool EnterHasOwnMeaning() =>
        ProgramNameBox.IsKeyboardFocusWithin
        || Keyboard.FocusedElement is ComboBoxItem or ComboBox { IsDropDownOpen: true };

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
