# Design V2: Überarbeitung nach dem ersten Test (verbindliche Vorgabe für alle Beteiligten)

Auslöser: Rückmeldung des Nutzers zu rc2. Die App funktioniert, ist aber "simpel", unübersichtlich und kennt nicht die Logik des Geräts. Dieses Dokument legt fest, **was** gebaut wird. Wer daran arbeitet, hält sich daran und ändert nur den ihm zugewiesenen Teil.

## 1. Wünsche des Nutzers (in seinen Worten sinngemäß)
1. Kopfzeile (Profil, Name, "Neues Profil", "Löschen") hat zu viel Platz und Priorität. Profil klein halten.
2. Gerät als Bild ist gut, soll besser werden; das ganze Fenster soll ein durchgängiges, modernes Design bekommen. Die Bewegung von Drehreglern und Fadern bleibt.
3. **Auswählen = anklicken ODER am Gerät drücken/bewegen.** Drückt man z. B. Record am Gerät, wird Record im Bild gewählt und man kann direkt einstellen. (Ersetzt den MIDI-Learn-Knopf.)
4. Einstellungs-Panel: **zu viel Text, mit Symbolen arbeiten.** "Testen" entfällt. "Entfernen" bleibt. **"Übernehmen" muss sichtbar sein**, sobald etwas geändert wurde.
5. Die Liste "Belegungen" unten entfällt. Stattdessen zeigt das **Seitenpanel rechts** für das gewählte Element, was darauf liegt. Im Bild trägt jedes belegte Element ein kleines Symbol seiner Belegung.
6. **Logik je nach Reglerart:** Ein Fader/Drehregler ist stufenlos (0..127), ein Knopf ist 0/1 (drücken/loslassen) oder Umschalter. Bei einem Fader ist "Tastenkombination" praktisch nie sinnvoll. Es dürfen nur Aktionen angeboten werden, die zur Art des Elements passen.
7. **Mehrere Ziele auf einem Regler:** z. B. Fader 4 steuert *alle* Spiele gemeinsam (mehrere Programme in einer Liste). Man sieht, was darauf liegt, und kann Einträge hinzufügen/entfernen.
8. **Drag-and-Drop:** Eine Belegung lässt sich im Geräteabbild auf ein anderes passendes Element ziehen (verschieben; ist das Ziel belegt, tauschen; mit Strg kopieren).
9. Alle Einstellungen (Anzeige bei Änderungen, Position, Anzeige testen, Mit Windows starten, Infobereich, minimiert starten) wandern hinter **ein Zahnrad-Symbol**.
10. (Offen, nicht Teil dieser Stufe) LEDs am Gerät.

## 2. Datenmodell V2 (Core)
- `ControlMapping(ControlId Control, IReadOnlyList<ActionDefinition> Actions, ValueScaling? Scaling = null, string? Name = null)`. Mehrere Aktionen pro Element; `Name` ist ein optionaler eigener Titel (z. B. "Spiele") für Anzeige und Overlay.
- Aktionen:
  - `SetVolumeAction(AudioTarget)` nur für **stufenlose** Elemente. Mehrere Programme = mehrere `SetVolumeAction` mit je einem `AudioTarget.Application`.
  - `MuteAction(AudioTarget, MuteMode Mode = Toggle)` ersetzt `ToggleMuteAction` (JSON-Typ `mute`; alt: `toggleMute`). `MuteMode`: `Toggle`, `MuteWhileHeld` (stumm solange gedrückt), `UnmuteWhileHeld` (Push-to-Talk: Ton nur solange gedrückt). Nur für **Tasten**.
  - `MediaAction`, `HotkeyAction`, `SwitchDeviceAction`: nur für **Tasten**, lösen beim Drücken aus.
- Reglerart: `ControlShape { Unknown, Continuous, Button }`. Das Layout (`Nanokontrol2Layout`) kennt die Art jedes der 51 Elemente (Drehregler/Fader = Continuous, alle Tasten = Button). Per MIDI-Learn gefundene Zusatzregler sind `Unknown`, bis ein Wert außer 0/127 gesehen wurde (dann Continuous) oder Tastenverhalten klar ist (nur 0/127, dann Button); bei Unknown sind alle Aktionen erlaubt.
- Zulässigkeit: `ActionRules.IsAllowed(ActionDefinition, ControlShape)`; `Profile.Validate()` meldet unzulässige Kombinationen (deutscher Text über `Texts`). Der Editor bietet nur Zulässiges an.
- Gruppen-Stumm: Mehrere `MuteAction`-Ziele im selben Mapping im Modus `Toggle` schalten gemeinsam: ist irgendein Ziel hörbar, werden alle stumm, sonst alle laut (kein Auseinanderlaufen).
- Dispatcher/Router: Stufenlos: jede Nachricht erzeugt je Aktion eine Ausführung mit skaliertem Pegel. Tasten: steigende Flanke (Wert >= 64) = `Pressed`, fallende Flanke = `Released`. `Released` erreicht nur Aktionen im Modus `*WhileHeld`. Der Ausführungs-Datensatz trägt Mapping, Aktion, Pegel und Phase.
- Profile-Operationen: `Profile.TransferMapping(ControlId from, ControlId to, TransferMode mode, ShapeLookup)` mit `Move` (belegtes Ziel wird getauscht) und `Copy`; unzulässige Paarungen (z. B. Fader auf Taste mit `SetVolumeAction`) liefern ein Ergebnis `Incompatible` ohne Änderung.
- **Speicherformat:** Schema-Version 2 (`actions: [...]`). Dateien der Version 1 (einzelne `action`, `toggleMute`) werden beim Laden verlustfrei migriert; vor dem ersten Überschreiben wird einmalig `profiles.json.v1.bak` angelegt. Eine neuere Version wird abgelehnt wie bisher.

## 3. Verhalten der Oberfläche (ViewModels, ohne WPF-Typen, Linux-testbar)
- **Auswahl folgt dem Gerät** (`FollowDevice`, Standard an, Nadel-Symbol zum Anpinnen): Die erste Nachricht eines Elements nach >= 600 ms Ruhe wählt es aus (auch unbelegte). Während man ein Element bedient, springt die Auswahl nicht. Mausklick wählt immer.
- **Änderungen übernehmen:** Der Editor kennt `IsDirty`. Sobald etwas geändert und gültig ist, erscheint eine **auffällige** Übernehmen-Leiste (Akzentfarbe, Enter geht auch) und ein Verwerfen. Wechselt die Auswahl bei gültigen, ungespeicherten Änderungen, werden sie **automatisch übernommen**; ungültige werden verworfen mit Hinweis in der Statuszeile. Nichts geht still verloren.
- **Mehrere Ziele:** Der Editor führt eine Liste der Ziele (Lautsprecher, Mikrofon, Programme mit Symbol und Namen). Hinzufügen aus der Liste laufender Programme oder per Eintippen; entfernen per Kreuz. Gleiche Ziele nicht doppelt.
- **Aktionsarten je nach Reglerart:** Stufenlos: nur "Lautstärke". Taste: "Stumm" (mit Modus: Umschalten / Gedrückt halten = stumm / Gedrückt halten = Ton an), "Medien", "Gerät wechseln", "Tastenkombination".
- **Drag-and-Drop:** `MainViewModel.TransferMapping(from, to, copy)` mit Rückmeldung für die Statuszeile; das Bild ruft es auf.
- **Overlay** bei mehreren Zielen: Titel = `Name` oder erstes Ziel + "+ n", Pegel vom ersten Ziel.
- Keine englischen Nutzertexte außerhalb von `Texts`. Zuordnungsbeschreibung (`MappingDescriber`) kurz, auch für Mehrfachziele ("3 Programme").

## 4. Aufbau des Fensters (App)
```
┌ Titelleiste: [Logo] Ear Saxophone   ● nanoKONTROL2   [Profil ▾]  [⚙]            ─ □ ✕
├──────────────────────────────────────────────┬────────────────────────────────┐
│                                              │ Fader 4                  [📌]  │
│        Geräteabbild (groß, Mittelpunkt)      │ ──────────────────────────────  │
│        Elemente mit Mini-Symbol der Belegung │ 🔊 Lautstärke  (Symbol-Auswahl) │
│        Auswahlring, Live-Bewegung            │ Ziele:  [Symbol] Name   ✕ ...   │
│        Drag-and-Drop                         │         [＋ hinzufügen]         │
│                                              │ Bereich / umkehren (Symbole)    │
│                                              │ ┌────────────────────────────┐ │
│                                              │ │  ✓ Übernehmen    ✕  🗑     │ │  <- auffällig, sobald geändert
│                                              │ └────────────────────────────┘ │
└──────────────────────────────────────────────┴────────────────────────────────┘
 dünne Statuszeile / Fehlerleiste (nur wenn nötig)
```
- **Profil** ist ein kleines Dropdown in der Titelzeile (Wechseln, Neu, Umbenennen, Löschen in einem Menü mit Symbolen); höchstens 32 px hoch.
- **Zahnrad** öffnet ein Flyout: Anzeige bei Änderungen (Schalter), Position (Auswahl über sechs kleine Positionssymbole), Anzeige testen, Mit Windows starten, Im Infobereich bleiben, Minimiert starten. Sonst stehen keine Einstellungen im Hauptfenster.
- **Symbole statt Text:** Symbole kommen aus WPF-UI (`ui:SymbolIcon`, Fluent-Symbolschrift ist mitgeliefert); Namen werden beim Kompilieren geprüft. Text nur dort, wo er Namen oder Werte trägt. Kurze Tooltips erklären die Symbole.
- **Design:** durchgehend dunkel, ruhiger Verlauf im Hintergrund, Karten mit großen Rundungen und feinem Rand, Akzentfarbe `#3FB6A8` (wie das Programmsymbol), 8-Punkt-Raster, klare Schriftgrößen (Titel 18, Text 13, Hinweis 11), sanfte Animationen (Auswahlring, Hover, Übernehmen-Leiste einblenden, 120-200 ms). Mindestgröße des Fensters bleibt bedienbar.

## 5. Regeln für alle Beteiligten
- Sprache der Oberfläche: Deutsch, alle Texte zentral in `src/EarSaxophone.Core/Localization/Texts.cs`.
- Plattformneutrale Logik gehört in `EarSaxophone.Core` und wird mit xUnit unter Linux getestet; Windows-spezifisches nur in `EarSaxophone.Windows`/`EarSaxophone.App`.
- Bauen/Testen: `dotnet build -c Release` und `dotnet test tests/EarSaxophone.Core.Tests -c Release` (Linux; WPF kompiliert über `EnableWindowsTargeting`, startet aber nicht). Warnungen sind Fehler.
- Wichtige Logik wird zusätzlich mit gezielten Code-Änderungen (Mutationen) auf Aussagekraft geprüft und danach zurückgesetzt.
- **Nicht pushen, keine Tags, keine Workflows ändern, nicht `git commit`** (der Kurator committet nach Durchsicht). Keine Dateien außerhalb des zugewiesenen Teils ändern, außer wo für das Kompilieren zwingend nötig (dann in der Abschlussmeldung nennen).
- Was nicht gelaufen ist, wird in der Abschlussmeldung als **ungeprüft** bezeichnet. Nichts als "getestet" melden, was nicht ausgeführt wurde. UI-Verhalten kann nur die CI auf Windows prüfen.
- Abschlussmeldung: höchstens 25 Zeilen: erledigt, geänderte/neue Dateien, ausgeführte Tests mit Ergebnis, ungeprüft/offen, Annahmen.
