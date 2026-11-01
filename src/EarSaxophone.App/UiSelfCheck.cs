using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.App;

/// <summary>
/// CI only (--demo-screenshot): operates the real window through UI-Automation (the same path a screen reader or a click takes)
/// and checks the effects on the view model and on what is displayed. Proves that the XAML bindings and commands are wired up.
/// Controls are found by their AutomationId (see MainWindow.xaml). Expects the demo profile and the demo state of
/// <see cref="DemoData.PrepareScreenshot"/> (Fader 1 selected with an open, valid change).
/// </summary>
internal static class UiSelfCheck
{
    public sealed record Result(string Name, bool Ok, string Detail);

    public static List<Result> Run(Window window, MainViewModel vm, OsdWindow osd)
    {
        var results = new List<Result>();
        var main = (MainWindow)window;

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

        Check("Bild zeigt fuer jedes belegte Element ein Symbol, bei mehreren Aktionen mit Zahl; unbelegte ohne Symbol", () =>
        {
            var problems = new List<string>();
            int badges = 0;
            int counts = 0;
            foreach (ControllerElementViewModel element in vm.Elements)
            {
                Button tile = FindTile(window, element.Label);
                bool badge = FindById<Border>(tile, "Badge") is { IsVisible: true };
                TextBlock? count = FindById<TextBlock>(tile, "BadgeCount");
                bool countShown = count is { IsVisible: true } && count.Text == element.ActionCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                badges += badge ? 1 : 0;
                counts += countShown ? 1 : 0;
                if (badge != element.HasMapping || countShown != element.ShowsActionCount)
                {
                    problems.Add($"{element.Label}: badge={badge}, count={count?.Text}/{count?.IsVisible}, mapped={element.HasMapping}, actions={element.ActionCount}");
                }
            }

            return (problems.Count == 0 && counts > 0, problems.Count == 0 ? $"symbole={badges}, mitZahl={counts}" : string.Join("; ", problems));
        });

        Check("Demo: geaenderte Bearbeitung zeigt die Uebernehmen-Leiste (aktiv) und Verwerfen", () =>
        {
            bool bar = FindById<Border>(window, "ApplyBar") is { IsVisible: true };
            bool apply = FindById<ButtonBase>(window, "ApplyButton") is { IsVisible: true, IsEnabled: true };
            bool discard = FindById<ButtonBase>(window, "DiscardButton") is { IsVisible: true, IsEnabled: true };
            return (vm.Editor.IsDirty && bar && apply && discard,
                $"dirty={vm.Editor.IsDirty}, leiste={bar}, uebernehmen={apply}, verwerfen={discard}, gewaehlt={vm.SelectedElement?.Label}");
        });

        Check("Klick auf die Kachel 'Fader 2' waehlt sie aus, zeigt den Titel und uebernimmt die offene Aenderung", () =>
        {
            ControlId fader1 = vm.Elements.First(e => e.Label == "Fader 1").Control;
            int before = vm.Current.Find(fader1)?.Actions.Count ?? 0;
            Click(FindTile(window, "Fader 2"));
            Flush(window);
            bool title = FindById<TextBlock>(window, "EditorTitle") is { IsVisible: true, Text: "Fader 2" };
            int after = vm.Current.Find(fader1)?.Actions.Count ?? 0;
            bool bar = FindById<Border>(window, "ApplyBar") is { IsVisible: true };
            return (vm.SelectedElement?.Label == "Fader 2" && title && after == before + 1 && !bar,
                $"selected={vm.SelectedElement?.Label}, titel={title}, aktionenFader1={before}->{after}, leisteSichtbar={bar}");
        });

        Check("Fader bietet nur 'Lautstaerke' an (keine Auswahl der Art)", () =>
        {
            bool picker = FindById<ListBox>(window, "KindPicker") is { IsVisible: true };
            bool fixedKind = FindById<TextBlock>(window, "FixedKindText") is { IsVisible: true, Text: Texts.UiKindVolume };
            return (!picker && fixedKind, $"auswahlSichtbar={picker}, festeArt={fixedKind}");
        });

        Check("Taste 'M 1' bietet nur passende Arten an und zeigt den Stumm-Modus", () =>
        {
            Click(FindTile(window, "M 1"));
            Flush(window);
            ListBox picker = FindById<ListBox>(window, "KindPicker") ?? throw new InvalidOperationException("KindPicker not found");
            List<ActionKind> kinds = picker.Items.OfType<Option<ActionKind>>().Select(o => o.Value).ToList();
            bool mode = FindById<ComboBox>(window, "MuteModePicker") is { IsVisible: true };
            bool ok = picker.IsVisible && !kinds.Contains(ActionKind.SetVolume) && kinds.Contains(ActionKind.Mute)
                && kinds.Count == vm.Editor.KindOptions.Count && Equals(picker.SelectedValue, ActionKind.Mute) && mode;
            return (ok, $"arten=[{string.Join(",", kinds)}], gewaehlt={picker.SelectedValue}, modus={mode}");
        });

        Check("Ziele-Liste: Entfernen per Kreuz macht die Bearbeitung geaendert, 'Verwerfen' stellt alles wieder her", () =>
        {
            Click(FindTile(window, "Fader 1"));
            Flush(window);
            int before = vm.Editor.Targets.Count;
            ItemsControl list = FindById<ItemsControl>(window, "TargetList") ?? throw new InvalidOperationException("TargetList not found");
            int shown = FindAll<TextBlock>(list).Count(t => t.IsVisible && AutomationProperties.GetAutomationId(t) == "TargetName");
            Click(FindAll<ButtonBase>(list).First(b => b.IsVisible && AutomationProperties.GetAutomationId(b) == "RemoveTargetButton"));
            Flush(window);
            int removed = vm.Editor.Targets.Count;
            bool bar = FindById<Border>(window, "ApplyBar") is { IsVisible: true };
            Click(FindById<ButtonBase>(window, "DiscardButton") ?? throw new InvalidOperationException("DiscardButton not found"));
            Flush(window);
            bool barAfter = FindById<Border>(window, "ApplyBar") is { IsVisible: true };
            return (before > 1 && shown == before && removed == before - 1 && bar && vm.Editor.Targets.Count == before && !barAfter && !vm.Editor.IsDirty,
                $"ziele={before}, angezeigt={shown}, nachEntfernen={removed}, leiste={bar}, nachVerwerfen={vm.Editor.Targets.Count}, leisteDanach={barAfter}");
        });

        Check("Reihenfolge der Ziele aendert sich per Pfeil", () =>
        {
            string first = vm.Editor.Targets[0].Name;
            ItemsControl list = FindById<ItemsControl>(window, "TargetList")!;
            Click(FindAll<ButtonBase>(list).First(b => b.IsVisible && AutomationProperties.GetAutomationId(b) == "MoveTargetDownButton"));
            Flush(window);
            bool moved = vm.Editor.Targets[1].Name == first && vm.Editor.IsDirty;
            vm.DiscardEditor();
            Flush(window);
            return (moved && vm.Editor.Targets[0].Name == first, $"vorher erstes={first}, danach zweites={vm.Editor.Targets[1].Name}");
        });

        Check("Programm eintippen und '+' fuegt ein Ziel hinzu; 'Uebernehmen' speichert es", () =>
        {
            ControlId control = vm.SelectedElement!.Control;
            var box = FindById<ComboBox>(window, "NewProgramBox") ?? throw new InvalidOperationException("NewProgramBox not found");
            box.Text = "Selbsttest.exe";
            Flush(window);
            Click(FindById<ButtonBase>(window, "AddProgramButton")!);
            Flush(window);
            bool listed = vm.Editor.Targets.Any(t => t.Name == "Selbsttest.exe");
            bool applyEnabled = FindById<ButtonBase>(window, "ApplyButton") is { IsVisible: true, IsEnabled: true };
            Click(FindById<ButtonBase>(window, "ApplyButton")!);
            Flush(window);
            bool saved = vm.Current.Find(control)?.Actions.OfType<SetVolumeAction>().Any(a => a.Target.App?.ExeName == "Selbsttest.exe") == true;
            bool barAfter = FindById<Border>(window, "ApplyBar") is { IsVisible: true };

            // Clean up: take it out again.
            vm.Editor.RemoveTarget(vm.Editor.Targets.First(t => t.Name == "Selbsttest.exe"));
            vm.ApplyEditor();
            Flush(window);
            return (listed && applyEnabled && saved && !barAfter && box.Text.Length == 0,
                $"inListe={listed}, uebernehmenAktiv={applyEnabled}, gespeichert={saved}, leisteDanach={barAfter}, eingabeLeer={box.Text.Length == 0}");
        });

        Check("MIDI-Learn startet das Lernen und zeigt den Hinweis; 'Abbrechen' beendet es", () =>
        {
            Click(FindById<ButtonBase>(window, "LearnButton")!);
            Flush(window);
            bool learning = vm.IsLearning;
            bool hint = FindById<Border>(window, "LearnBanner") is { IsVisible: true };
            Click(FindById<ButtonBase>(window, "CancelLearnButton")!);
            Flush(window);
            bool hintAfter = FindById<Border>(window, "LearnBanner") is { IsVisible: true };
            return (learning && hint && !vm.IsLearning && !hintAfter, $"lernt={learning}, hinweis={hint}, danach lernt={vm.IsLearning}, hinweis={hintAfter}");
        });

        Check("Profil-Menue: Neu, Umbenennen, Loeschen und Wechsel ueber das Auswahlfeld", () =>
        {
            string original = vm.ActiveProfileName;
            int before = vm.ProfileNames.Count;
            Popup popup = main.ProfilePopup;
            popup.StaysOpen = true; // no real mouse in CI: keep it open while the check works in it
            try
            {
                Toggle(FindById<ToggleButton>(window, "ProfileMenuButton")!);
                Flush(window);
                bool open = popup.IsOpen;
                Click(FindById<ButtonBase>(window, "NewProfileButton")!);
                Flush(window);
                bool created = vm.ProfileNames.Count == before + 1 && vm.ActiveProfileName.StartsWith("Profil ", StringComparison.Ordinal);
                var name = FindById<TextBox>(window, "ProfileNameBox")!;
                name.Text = "Selbsttest";
                name.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Flush(window);
                bool renamed = vm.ActiveProfileName == "Selbsttest" && main.ProfilePicker.SelectedItem as string == "Selbsttest";
                Click(FindById<ButtonBase>(window, "DeleteProfileButton")!);
                Flush(window);
                bool deleted = vm.ProfileNames.Count == before && !vm.ProfileNames.Contains("Selbsttest");
                main.ProfilePicker.SelectedItem = original;
                Flush(window);
                return (open && created && renamed && deleted && vm.ActiveProfileName == original,
                    $"offen={open}, neu={created}, umbenannt={renamed}, geloescht={deleted}, aktiv={vm.ActiveProfileName}");
            }
            finally
            {
                popup.IsOpen = false;
                popup.StaysOpen = false;
            }
        });

        Check("Fehlerleiste zeigt eine Meldung an und das Kreuz blendet sie aus", () =>
        {
            vm.ShowError("Testfehler im Selbsttest");
            Flush(window);
            bool shown = FindById<TextBlock>(window, "ErrorText") is { IsVisible: true, Text: "Testfehler im Selbsttest" };
            Click(FindById<ButtonBase>(window, "DismissErrorButton")!);
            Flush(window);
            bool hidden = FindById<Border>(window, "ErrorBar") is not { IsVisible: true };
            return (shown && hidden && !vm.HasError, $"shown={shown}, hiddenAfterClose={hidden}");
        });

        Check("Zahnrad oeffnet die Einstellungen mit allen Schaltern; Schalter aendern die Einstellungen", () =>
        {
            Popup popup = main.SettingsPopup;
            popup.StaysOpen = true;
            try
            {
                Toggle(FindById<ToggleButton>(window, "SettingsButton")!);
                Flush(window);
                bool open = popup.IsOpen;
                string[] ids = ["ShowOsdToggle", "OsdPositionPicker", "TestOsdButton", "StartWithWindowsToggle", "KeepInTrayToggle",
                    "StartMinimizedToggle", "AskForNewProgramsToggle", "ResetIgnoredButton"];
                List<string> missing = ids.Where(id => FindById<FrameworkElement>(popup.Child, id) is not { IsVisible: true }).ToList();

                var changes = new List<string>();
                void Flip(string id, Func<bool> read)
                {
                    bool before = read();
                    var toggle = FindById<ToggleButton>(popup.Child, id)!;
                    Toggle(toggle);
                    Flush(window);
                    bool changed = read() != before;
                    Toggle(toggle);
                    Flush(window);
                    if (!changed || read() != before)
                    {
                        changes.Add(id);
                    }
                }

                Flip("KeepInTrayToggle", () => vm.KeepInTray);
                Flip("StartMinimizedToggle", () => vm.StartMinimized);
                Flip("AskForNewProgramsToggle", () => vm.AskForNewPrograms);

                var positions = FindById<ListBox>(popup.Child, "OsdPositionPicker")!;
                OsdPosition original = vm.OsdPosition;
                ((ListBoxItem)positions.ItemContainerGenerator.ContainerFromIndex(0)).IsSelected = true;
                Flush(window);
                bool position = vm.OsdPosition == OsdPosition.TopLeft && positions.Items.Count == 6;
                vm.OsdPosition = original;
                Flush(window);

                return (open && missing.Count == 0 && changes.Count == 0 && position,
                    $"offen={open}, fehlt=[{string.Join(",", missing)}], ohneWirkung=[{string.Join(",", changes)}], position={position}");
            }
            finally
            {
                popup.IsOpen = false;
                popup.StaysOpen = false;
            }
        });

        Check("'Anzeige testen' (im Zahnrad) zeigt das Overlay mit Ziel und Prozentwert", () =>
        {
            Popup popup = main.SettingsPopup;
            popup.StaysOpen = true;
            try
            {
                popup.IsOpen = true;
                Flush(window);
                Click(FindById<ButtonBase>(popup.Child, "TestOsdButton")!);
                Flush(window);
                bool ok = osd.IsVisible && osd.TitleValue == "Lautsprecher" && osd.ValueTextValue == "65 %";
                return (ok, $"sichtbar={osd.IsVisible}, titel={osd.TitleValue}, wert={osd.ValueTextValue}");
            }
            finally
            {
                popup.IsOpen = false;
                popup.StaysOpen = false;
            }
        });

        Check("Overlay ist klickdurchlässig und nimmt keinen Fokus (Fensterstil)", () =>
            (osd.IsClickThroughAndNoFocus, $"exStyle=0x{osd.ExtendedStyle:X}"));

        Check("Overlay liegt in allen sechs Positionen vollständig auf dem Bildschirm", () =>
        {
            var area = SystemParameters.WorkArea;
            var problems = new List<string>();
            foreach (OsdPosition position in Enum.GetValues<OsdPosition>())
            {
                osd.Present(new OsdMessage("Discord.exe", "Ton an", 0.3), position, TimeSpan.FromSeconds(5));
                Flush(window);
                bool inside = osd.Left >= area.Left - 1 && osd.Top >= area.Top - 1
                    && osd.Left + osd.ActualWidth <= area.Right + 1 && osd.Top + osd.ActualHeight <= area.Bottom + 1;
                if (!inside)
                {
                    problems.Add($"{position}: {osd.Left:0},{osd.Top:0} {osd.ActualWidth:0}x{osd.ActualHeight:0} (Arbeitsbereich {area.Width:0}x{area.Height:0})");
                }
            }

            return (problems.Count == 0, problems.Count == 0 ? $"arbeitsbereich={area.Width:0}x{area.Height:0}" : string.Join("; ", problems));
        });

        Check("Overlay lässt sich sofort ausblenden", () =>
        {
            osd.HideNow();
            Flush(window);
            return (!osd.IsVisible, $"sichtbar={osd.IsVisible}");
        });

        Check("Aktion 'Audiogerät wechseln' (Symbol-Auswahl) zeigt Gerätetyp, Geräteliste und Hinweis", () =>
        {
            Click(FindTile(window, "Cycle"));
            Flush(window);
            ActionKind before = vm.Editor.Kind;
            ListBox picker = FindById<ListBox>(window, "KindPicker")!;
            int index = picker.Items.OfType<Option<ActionKind>>().ToList().FindIndex(o => o.Value == ActionKind.SwitchDevice);
            ((ListBoxItem)picker.ItemContainerGenerator.ContainerFromIndex(index)).IsSelected = true;
            Flush(window);
            bool kind = vm.Editor.Kind == ActionKind.SwitchDevice;
            bool flow = FindById<ComboBox>(window, "SwitchFlowPicker") is { IsVisible: true };
            bool list = FindById<ItemsControl>(window, "DeviceChoiceList") is { IsVisible: true };
            bool hint = FindById<TextBlock>(window, "SwitchHint") is { IsVisible: true };
            bool kindText = FindById<TextBlock>(window, "KindText") is { IsVisible: true, Text: Texts.UiKindSwitch };
            Click(FindById<ButtonBase>(window, "DiscardButton")!);
            Flush(window);
            return (kind && flow && list && hint && kindText && vm.Editor.Kind == before,
                $"art={kind}, gerätetyp={flow}, liste={list}, hinweis={hint}, artText={kindText}, nachVerwerfen={vm.Editor.Kind}");
        });

        Check("Ziehen: passendes Ziel wird markiert, unpassendes nicht; Kopieren, Entfernen, Verschieben und Zurueck", () =>
        {
            ControllerElementViewModel Vm(string label) => vm.Elements.First(e => e.Label == label);
            ControllerElementViewModel fader2 = Vm("Fader 2"), fader8 = Vm("Fader 8"), play = Vm("Play");
            Button tile8 = FindTile(window, "Fader 8");
            Button tilePlay = FindTile(window, "Play");
            bool fits = main.PreviewMappingDrop(tile8, fader2, fader8, copy: false);
            Flush(window);
            bool ring = tile8.Template.FindName("DropRing", tile8) is FrameworkElement { IsVisible: true };
            bool misfit = main.PreviewMappingDrop(tilePlay, fader2, play, copy: false);
            Flush(window);
            bool ringGone = tile8.Template.FindName("DropRing", tile8) is FrameworkElement { IsVisible: false } && !DropTarget.GetIsActive(tilePlay);

            main.DropMapping(fader2, fader8, copy: true);
            Flush(window);
            bool copied = fader8.HasMapping && fader2.HasMapping && vm.SelectedElement == fader8
                && FindById<Border>(tile8, "Badge") is { IsVisible: true };
            Click(FindById<ButtonBase>(window, "RemoveButton")!);
            Flush(window);
            bool removed = !fader8.HasMapping;

            main.DropMapping(fader2, fader8, copy: false);
            Flush(window);
            bool moved = fader8.HasMapping && !fader2.HasMapping;
            main.DropMapping(fader8, fader2, copy: false);
            Flush(window);
            bool back = fader2.HasMapping && !fader8.HasMapping;
            return (fits && ring && !misfit && ringGone && copied && removed && moved && back,
                $"passt={fits}, ring={ring}, unpassend={misfit}, ringWeg={ringGone}, kopiert={copied}, entfernt={removed}, verschoben={moved}, zurueck={back}, status={vm.StatusMessage}");
        });

        Check("Nadel pinnt die Auswahl (Auswahl folgt dem Geraet aus) und loest sie wieder", () =>
        {
            var pin = FindById<ToggleButton>(window, "FollowDeviceToggle")!;
            bool before = vm.FollowDevice;
            Toggle(pin);
            Flush(window);
            bool pinned = !vm.FollowDevice && pin.IsChecked == true;
            Toggle(pin);
            Flush(window);
            return (before && pinned && vm.FollowDevice, $"vorher={before}, angepinnt={pinned}, danach={vm.FollowDevice}");
        });

        Check("Mindestgroesse: Kopfzeile, Bild, Seitenpanel und Uebernehmen-Leiste liegen im Fenster und ueberlappen nicht", () =>
        {
            double width = window.Width, height = window.Height;
            try
            {
                window.Width = window.MinWidth;
                window.Height = window.MinHeight;
                Click(FindTile(window, "Fader 1"));
                Flush(window);
                vm.Editor.Name = "Selbsttest";
                Flush(window);
                var problems = new List<string>();
                Rect Bounds(string id)
                {
                    var element = FindById<FrameworkElement>(window, id) ?? throw new InvalidOperationException($"{id} not found");
                    if (!element.IsVisible)
                    {
                        problems.Add($"{id} unsichtbar");
                    }

                    Rect r = element.TransformToAncestor(window).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                    if (r.Left < -1 || r.Top < -1 || r.Right > window.ActualWidth + 1 || r.Bottom > window.ActualHeight + 1)
                    {
                        problems.Add($"{id} ausserhalb ({r.X:0},{r.Y:0} {r.Width:0}x{r.Height:0})");
                    }

                    return r;
                }

                void Apart(params string[] ids)
                {
                    var rects = ids.Select(Bounds).ToList();
                    for (int i = 0; i < rects.Count; i++)
                    {
                        for (int j = i + 1; j < rects.Count; j++)
                        {
                            Rect common = Rect.Intersect(rects[i], rects[j]);
                            if (!common.IsEmpty && common.Width * common.Height > 1)
                            {
                                problems.Add($"{ids[i]} ueberlappt {ids[j]}");
                            }
                        }
                    }
                }

                Apart("ConnectionStatus", "ProfilePicker", "ProfileMenuButton", "ReconnectButton", "LearnButton", "SettingsButton");
                Apart("DeviceView", "SidePanel");
                Apart("ApplyButton", "DiscardButton", "RemoveButton");
                Bounds("StatusText");
                vm.DiscardEditor();
                Flush(window);
                return (problems.Count == 0, $"fenster={window.ActualWidth:0}x{window.ActualHeight:0}{(problems.Count == 0 ? string.Empty : ": " + string.Join("; ", problems))}");
            }
            finally
            {
                window.Width = width;
                window.Height = height;
                Flush(window);
            }
        });

        return results;
    }

    private static void Click(ButtonBase button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button) ?? throw new InvalidOperationException("no automation peer");
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
    }

    private static void Toggle(ToggleButton toggle)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(toggle) ?? throw new InvalidOperationException("no automation peer");
        ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();
    }

    private static void Flush(Window window)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        window.UpdateLayout();
    }

    private static Button FindTile(Window window, string label) =>
        FindAll<Button>(window).FirstOrDefault(b => b.IsVisible && b.DataContext is ControllerElementViewModel e && e.Label == label)
        ?? throw new InvalidOperationException($"Tile '{label}' not found");

    /// <summary>First element with this AutomationId (also inside open or closed popups), or null.</summary>
    private static T? FindById<T>(DependencyObject root, string id) where T : FrameworkElement =>
        FindAll<T>(root).FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id);

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T self)
        {
            yield return self;
        }

        if (root is Popup { Child: { } child })
        {
            // The content of a popup lives in its own window; it is searched as if it were a child of the popup.
            foreach (T nested in FindAll<T>(child))
            {
                yield return nested;
            }
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            foreach (T nested in FindAll<T>(VisualTreeHelper.GetChild(root, i)))
            {
                yield return nested;
            }
        }
    }
}
