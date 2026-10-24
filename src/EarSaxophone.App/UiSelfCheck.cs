using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.App;

/// <summary>
/// CI only (--demo-screenshot): operates the real window through UI-Automation (the same path a screen reader or a click takes)
/// and checks the effects on the view model and on what is displayed. Proves that the XAML bindings and commands are wired up.
/// </summary>
internal static class UiSelfCheck
{
    public sealed record Result(string Name, bool Ok, string Detail);

    public static List<Result> Run(Window window, MainViewModel vm)
    {
        var results = new List<Result>();

        void Check(string name, Func<(bool Ok, string Detail)> action)
        {
            try
            {
                (bool ok, string detail) = action();
                results.Add(new Result(name, ok, detail));
            }
            catch (Exception ex)
            {
                results.Add(new Result(name, false, ex.GetType().Name + ": " + ex.Message));
            }
        }

        Check("Klick auf die Kachel 'Fader 2' waehlt sie aus und zeigt sie im Editor", () =>
        {
            Click(window, FindTile(window, "Fader 2"));
            Flush(window);
            bool title = FindText(window, t => t.Text == "Fader 2" && t.FontSize > 16) is not null;
            return (vm.SelectedElement?.Label == "Fader 2" && title, $"selected={vm.SelectedElement?.Label}, editorTitleVisible={title}");
        });

        Check("MIDI-Learn-Schaltflaeche startet das Lernen und zeigt den Hinweis", () =>
        {
            Click(window, FindButton(window, "Regler wählen (MIDI-Learn)"));
            Flush(window);
            bool hint = FindText(window, t => t.Text.StartsWith("Bewegen oder drücken Sie jetzt", StringComparison.Ordinal)) is not null;
            return (vm.IsLearning && hint, $"isLearning={vm.IsLearning}, hintVisible={hint}");
        });

        Check("'Abbrechen' beendet das Lernen und blendet den Hinweis aus", () =>
        {
            Click(window, FindButton(window, "Abbrechen"));
            Flush(window);
            bool hint = FindText(window, t => t.Text.StartsWith("Bewegen oder drücken Sie jetzt", StringComparison.Ordinal)) is not null;
            return (!vm.IsLearning && !hint, $"isLearning={vm.IsLearning}, hintVisible={hint}");
        });

        Check("'Neues Profil' legt ein Profil an und schaltet darauf um", () =>
        {
            int before = vm.ProfileNames.Count;
            Click(window, FindButton(window, "Neues Profil"));
            Flush(window);
            return (vm.ProfileNames.Count == before + 1 && vm.ActiveProfileName.StartsWith("Profil ", StringComparison.Ordinal),
                $"profiles={vm.ProfileNames.Count}, active={vm.ActiveProfileName}");
        });

        Check("'Profil loeschen' entfernt es wieder", () =>
        {
            int before = vm.ProfileNames.Count;
            Click(window, FindButton(window, "Profil löschen"));
            Flush(window);
            return (vm.ProfileNames.Count == before - 1, $"profiles={vm.ProfileNames.Count}, active={vm.ActiveProfileName}");
        });

        Check("Fehlerleiste zeigt eine Meldung an und 'Schliessen' blendet sie aus", () =>
        {
            vm.ShowError("Testfehler im Selbsttest");
            Flush(window);
            bool shown = FindText(window, t => t.Text == "Testfehler im Selbsttest") is not null;
            Click(window, FindButton(window, "Schließen"));
            Flush(window);
            bool hidden = FindText(window, t => t.Text == "Testfehler im Selbsttest") is null;
            return (shown && hidden && !vm.HasError, $"shown={shown}, hiddenAfterClose={hidden}");
        });

        Check("Schalter 'Im Infobereich bleiben' aendert die Einstellung", () =>
        {
            bool before = vm.KeepInTray;
            var toggle = FindAll<System.Windows.Controls.Primitives.ToggleButton>(window)
                .First(t => t.IsVisible && t.Content is string s && s == "Im Infobereich bleiben");
            var peer = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(toggle)!.GetPattern(PatternInterface.Toggle)!;
            peer.Toggle();
            Flush(window);
            bool changed = vm.KeepInTray != before;
            peer.Toggle();
            Flush(window);
            return (changed && vm.KeepInTray == before, $"changedByClick={changed}, restored={vm.KeepInTray == before}");
        });

        return results;
    }

    private static void Click(Window window, ButtonBase button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button) ?? throw new InvalidOperationException("no automation peer");
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
    }

    private static void Flush(Window window)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        window.UpdateLayout();
    }

    private static ButtonBase FindButton(Window window, string content) =>
        FindAll<ButtonBase>(window).FirstOrDefault(b => b.IsVisible && b.Content is string s && s == content)
        ?? throw new InvalidOperationException($"Button '{content}' not found or not visible");

    private static ButtonBase FindTile(Window window, string label) =>
        FindAll<Button>(window).FirstOrDefault(b => b.IsVisible && b.DataContext is ControllerElementViewModel e && e.Label == label)
        ?? throw new InvalidOperationException($"Tile '{label}' not found");

    private static TextBlock? FindText(Window window, Func<TextBlock, bool> predicate) =>
        FindAll<TextBlock>(window).FirstOrDefault(t => t.IsVisible && predicate(t));

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T nested in FindAll<T>(child))
            {
                yield return nested;
            }
        }
    }
}
