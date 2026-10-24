# Architektur (Stand Meilenstein 4)

```
src/EarSaxophone.Core   net10.0          plattformneutral, vollständig unit-getestet
src/EarSaxophone.Windows net10.0-windows Windows-Adapter (NAudio: MIDI-Eingang, Core Audio; SendInput)
src/EarSaxophone.App    net10.0-windows  WPF-Oberfläche (aktuell nur Platzhalterfenster)
tests/EarSaxophone.Core.Tests            xUnit, plattformneutral (läuft auf Linux und Windows)
tests/EarSaxophone.Windows.Tests         xUnit, nur Windows (läuft in der CI auf windows-latest)
```

## Core
- `Midi/`: `MidiMessage`, `ControlId` (Identität eines physischen Reglers), `MidiMessageParser` (Rohbytes und WinMM-Kurznachricht; nur Note-Off/On und Control-Change).
- `Midi/IMidiInput.cs`: Schnittstellen für Eingabeports; `MidiConnectionManager` verbindet per Namensfragment ("nanoKONTROL2") und verbindet nach Trennung selbst neu (Polling der Geräteliste, da WinMM kein Entfernen meldet).
- `Mapping/`
  - `ControlRouter`: ein Eingang für alle Nachrichten; schaltet zwischen MIDI-Learn und Aktionen um.
  - `ControlMapping`: Regler -> Aktion; ein Regler darf pro Profil nur einmal belegt sein (`Profile.Validate`).
  - Aktionen: `SetVolumeAction`, `ToggleMuteAction`, `MediaAction`, `HotkeyAction`.
  - `AudioTarget`: trennt `DeviceFlow.Render` (Lautsprecher) und `DeviceFlow.Capture` (Mikrofon) ausdrücklich, dazu `DeviceRole` und optional eine konkrete Geräte-ID. Anwendungen werden über `AppSelector` (Exe-Name/-Pfad) beschrieben, nie über Session-IDs.
  - `ValueScaling`: 0-127 -> Pegel, mit Bereich und Invertierung.
  - `MappingDispatcher`: Regler-Aktionen laufend, Tasten nur bei steigender Flanke (Wert >= 64).
  - `MidiLearnSession`: erste sinnvolle Nachricht nach `Start()` wird als Regler übernommen.
- `Persistence/`: `ProfileSerializer` (System.Text.Json, Schema-Version, sprechende Fehler), `ProfileStore` (atomares Schreiben über Temp-Datei, defekte Datei bleibt als `.corrupt` erhalten).

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
