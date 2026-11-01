# Architektur (Stand V2, in Abnahme)

```
src/EarSaxophone.Core   net10.0          plattformneutral, vollständig unit-getestet
src/EarSaxophone.Windows net10.0-windows Windows-Adapter (NAudio: MIDI-Eingang, Core Audio; SendInput)
src/EarSaxophone.App    net10.0-windows  WPF-Oberfläche (WPF-UI, Tray, Einzelinstanz, Start-Logik)
tests/EarSaxophone.Core.Tests            xUnit, plattformneutral (läuft auf Linux und Windows)
tests/EarSaxophone.Windows.Tests         xUnit, nur Windows (läuft in der CI auf windows-latest)
```

## Core
- `Midi/`: `MidiMessage`, `ControlId` (Identität eines physischen Reglers), `MidiMessageParser` (Rohbytes und WinMM-Kurznachricht; nur Note-Off/On und Control-Change).
- `Midi/IMidiInput.cs`: Schnittstellen für Eingabeports; `MidiConnectionManager` verbindet per Namensfragment ("nanoKONTROL2") und verbindet nach Trennung selbst neu (Polling der Geräteliste, da WinMM kein Entfernen meldet).
- `Mapping/`
  - `ControlRouter`: ein Eingang für alle Nachrichten; schaltet zwischen MIDI-Learn und Aktionen um.
  - `ControlMapping` (V2): Regler -> **Liste von Aktionen**, optional `Name`; ein Regler nur einmal je Profil (`Profile.Validate`). `ControlShape` (Continuous/Button/Unknown) und `ActionRules.IsAllowed` lassen je Reglerart nur passende Aktionen zu. `Profile.TransferMapping` (Verschieben/Tauschen/Kopieren).
  - Aktionen: `SetVolumeAction`, `MuteAction` (Modi Umschalten / `MuteWhileHeld` / `UnmuteWhileHeld`; ersetzt `ToggleMuteAction`), `MediaAction`, `HotkeyAction`, `SwitchDeviceAction`. Tasten erzeugen `Pressed` und `Released`.
  - Schema 2 in `profiles.json`; Schema 1 wird beim Laden migriert, einmalig Sicherung `profiles.json.v1.bak`.
  - `AudioTarget`: trennt `DeviceFlow.Render` (Lautsprecher) und `DeviceFlow.Capture` (Mikrofon) ausdrücklich, dazu `DeviceRole` und optional eine konkrete Geräte-ID. Anwendungen werden über `AppSelector` (Exe-Name/-Pfad) beschrieben, nie über Session-IDs.
  - `ValueScaling`: 0-127 -> Pegel, mit Bereich und Invertierung.
  - `MappingDispatcher`: Regler-Aktionen laufend, Tasten nur bei steigender Flanke (Wert >= 64).
  - `MidiLearnSession`: erste sinnvolle Nachricht nach `Start()` wird als Regler übernommen.
- `Persistence/`: `ProfileSerializer` (System.Text.Json, Schema-Version, sprechende Fehler), `ProfileStore` (atomares Schreiben über Temp-Datei, defekte Datei bleibt als `.corrupt` erhalten).

## Oberfläche (Core: `ViewModels/`, `Devices/`, `Localization/`, `Settings/`; App: `EarSaxophone.App`)
- Die Bedienlogik liegt **ohne WPF-Typen** im Core und ist unter Linux testbar: `MainViewModel` (MIDI-Learn, Auswahl, Übernehmen/Testen/Entfernen, Profile, Fehler- und Statusanzeige, Einstellungen), `MappingEditorViewModel` (Eingaben prüfen, Aktion bauen), `ControllerElementViewModel`/`ElementRowViewModel` (Kacheln mit Live-Pegel). Wechsel auf den UI-Thread über `IUiThread`.
- `Devices/Nanokontrol2Layout`: Standardbelegung (51 Elemente) für die Übersicht; unbekannte Regler erscheinen per MIDI-Learn als "Weitere Regler".
- `Localization/Texts`: **alle** Texte für den Nutzer (Deutsch) an einer Stelle; `MappingDescriber` formuliert die Kurzbeschreibung je Zuordnung.
- `Settings/`: `AppSettings` (Autostart, im Tray bleiben, minimiert starten), `DataLocation` (portabel neben der Programmdatei, sonst `%APPDATA%\EarSaxophone`), `IAutostart` (Windows: `RegistryAutostart`, Run-Schlüssel mit Pfadreparatur).
- App: dünnes XAML (`MainWindow.xaml`) mit Bindings; `App.xaml.cs` setzt alles zusammen (Audio mit Ersatz `UnavailableAudioSystem`, MIDI, Executor, Timer 1 s für die Geräteprüfung), Einzelinstanz per Mutex, Tray (H.NotifyIcon), `error.log` bei unerwarteten Fehlern.
- CI-Hilfen (nur mit `--demo-screenshot`, nie im Normalbetrieb): `DemoData`, `UiReport` (Textbericht, Überlappungs- und Ressourcenprüfung, ASCII-Vorschau), `UiSelfCheck` (Bedienung per UI-Automation).

## V2: Oberfläche, Pop-up, LEDs (siehe `docs/DESIGN_V2.md`, `docs/LED_RESEARCH.md`)
- ViewModels im Core (Linux-getestet, 517 Core-Tests): Auswahl folgt dem Gerät, Mehrziel-Editor mit `IsDirty`/Übernehmen, Transfer für Drag-and-Drop, Badges, Pop-up-Logik (`ProgramPromptService`, `IAudioSession` mit Aktiv-Zustand). WPF-Fenster (Hauptfenster, Pop-up) in der App: nur kompiliert und in der CI gestartet.
- LEDs: Rückmeldung für Stumm-Tasten über einen MIDI-Ausgang; Annahmen zu CC-Nummern/Kanal unbestätigt, nie an Hardware ausgeführt.

## Gerätewechsel, Anzeige, Update
- `SwitchDeviceAction` (Flow, optionale Geräte-IDs): `AudioController.SwitchDevice` bildet eine Rotation aus den aktiven Geräten (nach Namen sortiert, ggf. auf die Auswahl begrenzt), nimmt das Gerät nach dem aktuellen Standard und setzt es über `IAudioSystem.SetDefaultEndpoint`. Windows-Seite: `PolicyConfig` (interne `IPolicyConfig`-COM-Schnittstelle, alle drei Rollen); jeder Fehler wird als `AudioUnsupportedException` gemeldet. Nur kompiliert und ohne Gerät ausgeführt, nicht an echten Geräten geprüft.
- Anzeige: `ActionResult` trägt Pegel, Stumm-Zustand und Gerätename; `OsdMessageFactory` macht daraus eine `OsdMessage` (nicht bei Fehlern und Tastenkombinationen). `MainViewModel.OsdRequested` löst im App-Projekt `OsdWindow` aus (immer oben, `WS_EX_TRANSPARENT|NOACTIVATE|TOOLWINDOW`, sechs Positionen im Arbeitsbereich des Hauptbildschirms). Einstellungen `ShowOsd`/`OsdPosition` in `settings.json`.
- Geräteabbild: `Nanokontrol2Layout` liefert für jedes der 51 Elemente Art (Drehregler/Fader/Taste), Position und Größe im schematischen Bild (1000 x 460 Einheiten, per Unit-Test überlappungsfrei und innerhalb des Geräts); `ElementTemplateSelector` wählt die Zeichnung, Zeiger/Kappe/Leuchten folgen `LastValue`.
- Update (ohne Skript, `Core/Updates/`): `GitHubReleaseSource` liest `/repos/ragusa-ui/Ear-Saxophone/releases` (nicht `/latest`, ohne Token; 404 = privat/nicht erreichbar -> freundlicher Text im Zahnrad, nie die Fehlerleiste), `UpdateService` wählt die höchste `SemVersion` (Prereleases ja, Entwürfe nein), lädt ZIP und `.sha256` in einen Temp-Ordner, prüft SHA-256 (fehlt die Datei: Hinweis nach dem Neustart), entpackt und sucht `EarSaxophone.exe` rekursiv. `UpdateInstaller` benennt jede vorhandene Datei in `<name>.old` um und schreibt die neue (Windows erlaubt Umbenennen, nicht Überschreiben laufender exe/dll); bei einem Fehler wird alles zurückgebaut. `profiles.json`, `settings.json`, `error.log`, `*.bak` werden nie geschrieben. Noch belegte `.old`-Dateien stehen in `update-leftovers.txt` und werden beim nächsten Start gelöscht (nur gelistete). Neustart: neue exe mit `--after-update` starten, alte beendet sich und gibt den Einzelinstanz-Mutex frei, die neue wartet bis 30 s darauf. `UpdateViewModel`: Zahnrad-Bereich, Banner, automatische Prüfung höchstens alle 24 h (`settings.json`: `checkForUpdates`, `lastUpdateCheck`). CI: Core-Tests mit Fakes; `UiSelfCheck` mit Fake-Quelle (`DemoUpdate`, kein Netz); `Windows.Tests/UpdateRunningExeTests` ersetzt eine laufende exe. Echtes Verhalten mit GitHub-Download und Neustart unter Windows ist ungeprüft.

## Aktionen und Eingabe
- `Actions/ActionExecutor`: führt jede Aktion aus (Audio, Medienbefehl, Tastenkombination) und liefert ein `ActionResult` mit verständlicher Meldung; Ausnahmen werden nie nach außen gereicht.
- `Input/Hotkey` (+ `KeyTable`): Parser und kanonische Schreibweise für Kombinationen wie `Ctrl+Alt+M` (`Strg` wird akzeptiert), Tasten als Windows-Virtual-Key-Codes.
- `Input/IKeyboardSender`: Schnittstelle; `SendInputKeyboardSender` (Windows) sendet Modifikatoren, Taste, Loslassen in einem `SendInput`-Aufruf. Grenzen der Mediensteuerung: `docs/MEDIA_CONTROL.md`.
- Datenfluss: MIDI-Nachricht -> `ControlRouter` -> `ActionExecutor` -> `AudioController` / `IKeyboardSender`.

## Audio (Core: `Audio/`, Windows: `EarSaxophone.Windows/Audio/`)
- `IAudioSystem`, `IAudioEndpoint`, `IAudioSession`: Abstraktion; `NAudioAudioSystem` implementiert sie mit NAudio.Wasapi (nur kompiliert, nicht an echter Hardware geprüft).
- `AudioController`: führt `SetVolumeAction`/`ToggleMuteAction` aus.
  - **Geräte** (Lautsprecher, Mikrofon): Standardgerät je `DeviceFlow` und `DeviceRole` oder eine Geräte-ID. Das Mikrofon ist ein Capture-Gerät und kann nie mit den Lautsprechern verwechselt werden. Fehlende Geräte und Geräte ohne Lautstärkeregelung führen zu verständlichen Meldungen (`AudioActionResult`).
  - **Anwendungen:** siehe unten.
- Der Windows-Adapter cacht Geräte und Sessions, baut sie bei Geräteänderungen neu auf und gleicht Sessions einmal pro Sekunde ab. `Changed` wird nur bei tatsächlichen Änderungen ausgelöst.

### Anwendungs-Identität (Discord)
Entwurf und Stand der Absicherung:
1. Identität einer Anwendung = normalisierter **Exe-Name** (ohne Groß-/Kleinschreibung, `.exe` optional, exakter Vergleich: `Discord` trifft nicht `DiscordPTB`). Ein Pfad im `AppSelector` ist optional und bricht bei Programm-Updates mit Versionsordnern (Discord: `app-1.0.xxxx`).
2. Session-Schlüssel (Gerät + Session-Instanz) sind **keine** Identität. Sie dienen nur dazu, sich zu merken, welche Session den Sollwert schon bekommen hat.
3. Bei jeder Aktion werden die Sessions frisch aufgelöst (kein dauerhafter Verweis, der veralten kann). Alle passenden Sessions (mehrere Prozesse, mehrere Geräte) werden gemeinsam geändert.
4. Der zuletzt gesetzte Wert wird jeder **neuen** Session genau einmal mitgegeben (Neustart, Gerätewechsel). Danach kann der Nutzer in Windows abweichen, ohne dass der Fader es überschreibt.
5. Sessions, die zwischen Auflisten und Schreiben verschwinden, werden übersprungen; nur wenn keine einzige geändert werden konnte, gilt die Aktion als fehlgeschlagen.

**Stand:** Im Modell durch 31 Tests mit simulierten Sessions bestätigt (mehrere Sessions/Prozesse/Geräte, Neustart mit neuer PID, Update mit neuem Ordner, Gerätewechsel, sterbende Sessions, ähnliche Namen, Systemklänge, Capture vs. Render). Die Tests wurden mit gezielten Code-Änderungen (Mutationen) auf Aussagekraft geprüft. **Ob echte Windows-Sessions von Discord so reagieren, ist noch nicht bestätigt** (Tests M3-5 bis M3-10 in `docs/LOCAL_TESTS.md`).

Bekannte Grenzen: Der Exe-Pfad wird per `QueryFullProcessImageName` gelesen; bei geschützten Prozessen kann das scheitern, die Anwendung gilt dann als nicht gefunden. Browser-Tabs sind Teil einer Anwendung (Chrome-Sessions teilen sich `chrome.exe`) und nicht einzeln steuerbar.

## Robustheit (Ergebnis des unabhängigen Reviews, umgesetzt)
- **Threads:** Der WinMM-Treiber-Callback tut nur `MidiMessagePump.Post`. Ein eigener Arbeits-Thread führt Router, Audio und `SendInput` aus; Ausnahmen werden abgefangen und angezeigt. Wartende Nachrichten desselben Reglers werden nur zusammengefasst, wenn sie auf derselben Seite der Druckschwelle liegen: Fader laufen nie in einen Rückstau, ein schnelles Drücken und Loslassen einer Taste geht nie verloren.
- **Geräteverbindung:** `MidiConnectionManager` vergleicht Name **und** Port-Index; `Reconnect()` (Schaltfläche, Rückkehr aus dem Ruhezustand nach 3 s) öffnet das Gerät erneut.
- **Audio-Adapter:** Der Neuaufbau der Geräteliste ist atomar (bei Fehler bleibt der alte Zustand, `_needsRebuild` löst Wiederholungen im 1-s-Takt aus, auch nach einem Fehler beim Start). Windows-Meldungen zu Geräteänderungen werden 250 ms lang zu einem Neuaufbau zusammengefasst. Alle Aufrufe auf COM-Wrappern laufen unter derselben Sperre, unter der sie freigegeben werden; nach der Freigabe liefern sie `AudioGoneException` statt abzustürzen. Überzählige COM-Objekte werden freigegeben, bei einem Fehler an einem Gerät bleiben dessen bisherige Sitzungen erhalten (kein erneutes Anwenden des gemerkten Werts).
- **Fehlerprotokoll:** Unbehandelte Ausnahmen (UI-Thread, andere Threads, nicht beobachtete Tasks) landen in `error.log`.
- **Sperren-Reihenfolge:** `AudioController` vor `NAudioAudioSystem`; Ereignisse werden außerhalb von Sperren ausgelöst; COM-Callbacks starten nur Tasks.
