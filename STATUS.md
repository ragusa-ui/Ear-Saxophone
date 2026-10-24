# STATUS

**Letztes Update:** 2026-10-24 (Meilenstein 5 abgeschlossen, CI bestätigt)
**Letzter bestätigter Build:** GitHub Actions, [Lauf #15](https://github.com/ragusa-ui/Ear-Saxophone/actions/runs/40098245065) auf Commit `676f743` (Stand M5): **erfolgreich** auf `windows-latest`: Build, Tests (Core- und Windows-Testprojekt), self-contained Publish, Start der veröffentlichten App ohne .NET im PATH, 7 UI-Klickprüfungen, Normalstart mit Tray und Einzelinstanz, ZIP und UI-Screenshot als Artefakte; Linux-Job ebenfalls grün.

## Fortschritt
Berechnung: Summe der Gewichte erledigter Arbeitspakete (Gesamtgewicht 100). Ein Paket zählt, wenn es umgesetzt und **so weit geprüft ist, wie es in der Cloud möglich ist** (Unit-Tests mit Fakes, Kompilieren). Teilfortschritt wird nicht angerechnet. Alles, was echte Hardware braucht, steht zusätzlich in `docs/LOCAL_TESTS.md`; bestätigt hast du davon bisher **nichts**, und 5 Punkte (6.5) sind ausdrücklich für deine Abnahme reserviert.

| Meilenstein | Gewicht | Erledigt | Fortschritt |
|---|---|---|---|
| M1 Gerüst, Core-Modell, CI | 20 | 20 | 100 % |
| M2 MIDI-Eingang, Reconnect | 12 | 12 | 100 % |
| M3 Audio (Master, Mikrofon, Apps) | 25 | 25 | 100 % |
| M4 Medien und Hotkeys | 10 | 10 | 100 % |
| M5 Oberfläche, Tray, Autostart | 20 | 20 | 100 % |
| M6 Release, Review, Abnahme | 13 | 0 | 0 % |
| **Gesamt** | **100** | **87** | **87 %** |

**Einordnung:** 87 % heißt "in der Cloud umgesetzt und geprüft", nicht "auf deinem PC bestätigt". Auf echter Hardware bestätigt: 0 Punkte. Die Windows-Adapter (MIDI, Audio, SendInput) und die Oberfläche wurden bisher nur kompiliert und in der CI ohne Hardware ausgeführt; ein Mensch hat die App noch nie bedient.

## Aufgabenliste
Legende: ☑ erledigt · ◐ in Arbeit · ☐ noch nicht begonnen · ⛔ blockiert

### M1 (20) - abgeschlossen
- ☑ 1.1 Solution, .NET 10, zentrale Paketversionen (3)
- ☑ 1.2 MIDI-Nachrichtenmodell und Parser (3)
- ☑ 1.3 Mapping-/Profil-Modell inkl. JSON, Validierung (4)
- ☑ 1.4 Wertskalierung (2)
- ☑ 1.5 MIDI-Learn und Dispatcher (3)
- ☑ 1.6 ProfileStore (atomar, `.corrupt`-Backup) (2)
- ☑ 1.7 GitHub-Actions-Workflow, Lauf grün bestätigt (2)
- ☑ 1.8 Dokumentationsgerüst (1)

### M2 (12) - abgeschlossen
- ☑ 2.1 `IMidiInput`/`IMidiInputProvider` und NAudio-Adapter; **nur kompiliert** (3)
- ☑ 2.2 `MidiConnectionManager` (Erkennung, Polling-Reconnect, Meldungen); mit Fake-Gerät getestet (4)
- ☑ 2.3 `ControlRouter` (Learn + Dispatch) (3)
- ☑ 2.4 Lokale Testfälle M2 in `docs/LOCAL_TESTS.md` (2)

### M3 (25) - abgeschlossen (CI grün, Hardware-Tests offen)
- ☑ 3.1 Audio-Abstraktionen (`IAudioSystem`, Endpunkt, Session) und Fakes (3)
- ☑ 3.2 Ausgabegerät: Lautstärke und Mute im `AudioController` (Core, getestet) und NAudio-Adapter (nur kompiliert) (4)
- ☑ 3.3 Mikrofon als Capture-Gerät: Lautstärke, Mute, Meldungen bei fehlendem Gerät und bei Geräten ohne Windows-Regelung (Core getestet; Adapter nur kompiliert) (5)
- ☑ 3.4 Anwendungs-Identität (Discord-Hypothese) mit Tests: mehrere Sessions/Prozesse/Geräte, Neustart, Update, Gerätewechsel; Tests per Mutation auf Aussagekraft geprüft. **Nur im Modell bestätigt, echte Windows-Sessions offen** (8)
- ☑ 3.5 Ereignisbehandlung: neue Sessions und Gerätewechsel lösen Abgleich aus (Core getestet); Windows-Events und 1-s-Abgleich im Adapter (nur kompiliert) (5)

### M4 (10) - abgeschlossen (CI grün, Tests mit echten Programmen offen)
- ☑ 4.1 Medienbefehle (Play/Pause, Nächster, Vorheriger, Stop): `IKeyboardSender`, `ActionExecutor`, `SendInput`-Sender; Core mit Fake getestet, Tastenfolge und Struktur in der CI auf Windows getestet, echte Zustellung offen (3)
- ☑ 4.2 Hotkey-Parser (`Ctrl+Alt+M`, `Strg`, kanonische Schreibweise, verständliche Fehler) und Senden (5)
- ☑ 4.3 Grenzen der globalen Mediensteuerung dokumentiert (`docs/MEDIA_CONTROL.md`) und lokale Testfälle M4-1 bis M4-7 (2)

### M5 (20) - abgeschlossen (CI grün, Bedienung durch dich offen)
- ☑ 5.1 Dunkles Theme (WPF-UI) und Layout: Kopfzeile, Controller-Raster, Editor, Fußzeile; CI-Bericht: alle verwendeten Theme-Ressourcen vorhanden, 0 Überlappungen, 0 Elemente außerhalb des Fensters (4)
- ☑ 5.2 Controller-Übersicht (alle 51 Elemente des nanoKONTROL2 mit Live-Pegel) und MIDI-Learn; Logik per Unit-Tests, Klicks per UI-Automation in der CI (6)
- ☑ 5.3 Aktionszuweisung: Lautstärke/Stumm für Lautsprecher, Mikrofon, Anwendung (mit Liste laufender Programme), Medienbefehl, Tastenkombination mit Prüfung, Bereich/Umkehren, Testen, Entfernen, Profile (neu, umbenennen, löschen) (5)
- ☑ 5.4 Tray: Symbol, Menü Öffnen/Beenden, Schließen blendet nur aus, Start minimiert, Einzelinstanz; in der CI gestartet (kein Absturz, zweite Instanz beendet sich), Aussehen und Menü nicht gesehen (2)
- ☑ 5.5 Autostart: Run-Schlüssel mit Pfadreparatur; Registry-Tests auf Windows in der CI; Schalter in der Oberfläche getestet (2)
- ☑ 5.6 Verständliche Fehleranzeige: Fehlerleiste mit "Schließen", deutsche Meldungen zentral; in der CI angezeigt und geschlossen (1)

### M6 (13)
- ☐ 6.1 Release-Workflow und ZIP (2)
- ☐ 6.2 README final (2)
- ☐ 6.3 Unabhängiges Review kritischer Teile (Audio-Interop, Reconnect) mit einem stärkeren Modell (3)
- ☐ 6.4 Checkliste für lokale Tests final (1)
- ☐ 6.5 Lokale Abnahme durch den Nutzer auf Windows 11 mit echtem nanoKONTROL2 (5)

## Aktuelle Aufgabe
**Keine laufende Aufgabe.** M5 ist abgeschlossen. Als Nächstes beginnt M6 (Release, README, Review).

## Tests
| Bereich | Ergebnis |
|---|---|
| `EarSaxophone.Core.Tests`, Linux, Release: 148 Tests (Parser, Skalierung, Learn, Dispatcher, Router, Verbindungsmanager, Profile/Store, App-Matching, Endpunkte/Mikrofon, Anwendungs-Identität, Hotkeys, Executor, MainViewModel/Editor/Einstellungen) | **bestanden** (148/148) |
| Mutationsprüfung der Identitäts-Tests (Namensvergleich, Einmal-Anwendung, Render/Capture-Trennung) | alle 3 Änderungen wurden von den passenden Tests erkannt; Code wieder zurückgesetzt |
| GitHub Actions Lauf #8 (M4-Stand), Windows Server 2025: 125 Core-Tests und 16 Windows-Tests (INPUT-Struktur 40 Byte, Tastenfolge, Extended-Flag, Medientasten-Codes, Smoke-Tests der MIDI- und Audio-Adapter ohne Hardware); seit M5 kommen Registry-Tests für den Autostart dazu (Lauf #15 grün, Testschritt erfolgreich) | **bestanden** |
| GitHub Actions Lauf #15: Linux-Job, self-contained Publish, ZIP (65 MB) | **bestanden** |
| Lauf #15: veröffentlichte App startet auf Windows ohne .NET im PATH und rendert ihr Fenster (UI-Bericht: dunkles Theme, deutsche Texte, 0 Überlappungen) | **bestanden** (Screenshot als Artefakt `EarSaxophone-ui-screenshot`, nicht von mir angesehen, nur als Text geprüft) |
| Lauf #15: 7 UI-Prüfungen durch UI-Automation (Kachel wählen, MIDI-Learn starten/abbrechen, Profil neu/löschen, Fehlerleiste, Tray-Schalter) | **bestanden** |
| Lauf #15: Normalstart `--minimized` (Tray), zweite Instanz beendet sich, kein `error.log` | **bestanden** |
| Windows-Adapter (NAudio MIDI, Core Audio, SendInput) | in CI **ohne Hardware** ausgeführt (Erzeugen, Abfragen, Freigeben ohne Fehler; auf dem Runner gibt es kein MIDI-Gerät und vermutlich keine Audiogeräte); mit echten Geräten und Programmen nie ausgeführt |
| Alles mit nanoKONTROL2, Mikrofon, Discord, WPF-Start | noch nicht ausgeführt (`docs/LOCAL_TESTS.md`: M2-1 bis M2-6, M3-1 bis M3-12, M4-1 bis M4-7, M5-1 bis M5-12, alle offen) |

## Bekannte Probleme, offene Fragen, Blocker
- Keine Blocker.
- **Discord-Zuordnung ist weiterhin eine Hypothese in der Praxis.** Im Modell bestätigt; bei echten Sessions kann Windows/Discord anders reagieren (Tests M3-5 bis M3-10).
- Reconnect-Risiko: WinMM meldet das Entfernen eines USB-Geräts nicht; Erkennung per Polling (Test M2-3).
- Pfad eines Prozesses ist bei geschützten oder höher privilegierten Prozessen eventuell nicht lesbar; diese Anwendung gilt dann als nicht aktiv (Test M3-10).
- Browser-Tabs sind nicht einzeln steuerbar (eine Anwendung = alle ihre Sessions).
- Medientasten gehen an das Programm, das Windows als aktuelle Mediensitzung wählt; die App kann das Ziel nicht bestimmen. Ob Spotify/SoundCloud im Browser zuverlässig reagieren, ist offen (Tests M4-1 bis M4-3). Fenster mit Administratorrechten blockieren simulierte Tasten (Test M4-5). Details: `docs/MEDIA_CONTROL.md`.
- Hotkeys unterstützen in Version 1 keine Sonderzeichen-Tasten (`,` `.` `-` usw.).
- Threading der COM-Aufrufe (MIDI-Thread, Timer, Notification-Threads) ist nur durchdacht, nicht unter Last getestet (Test M3-12). Die Windows-Callbacks lösen die Arbeit bewusst in einem Task aus, weil sie auf einem gesperrten Audio-Thread laufen.
- CI-Warnung: Actions v4 laufen auf Node 20 (veraltet); Versionen später anheben.
- Offene Frage: Sprache der Oberfläche und der Meldungen (aktuell englische Texte im Core). Vorschlag: Deutsch als Standard in M5, Texte zentral sammeln. Bitte bestätigen, bevor M5 beginnt.
- Die Standardbelegung des nanoKONTROL2 (Control-Nummern der Beschriftung, z. B. Fader 1 = CC 0, Play = CC 41) stammt aus dem üblichen Werkszustand und ist nicht an deinem Gerät geprüft. Weicht es ab, erkennt MIDI-Learn jeden Regler trotzdem und zeigt ihn als "Weiterer Regler" (Test M5-2).
- Fensterposition und -größe werden nicht gespeichert.
- Im Editor ist als Ziel nur Standardgerät oder Anwendung wählbar; ein konkretes Gerät (Modell unterstützt es) kann die Oberfläche noch nicht einstellen.
- Die Oberfläche wurde in der CI mit 1100 px Breite gerendert; Verhalten bei hoher Windows-Skalierung (125-200 %) ist ungeprüft.
- Das Tray-Symbol und sein Menü wurden nie gesehen (CI ohne Taskleiste).
- Der Download der CI-Logs/Artefakte von dieser Cloud-Sitzung aus ist blockiert (Host `productionresultssa17.blob.core.windows.net` nicht erlaubt); ich prüfe die Oberfläche deshalb über den Textbericht im Log.

## Nächste Schritte
1. M6.1: Release-Workflow (ZIP als GitHub-Release bei Tag) und Hinweise zur ZIP-Nutzung.
2. M6.2: README final (Installation, Bedienung, Einschränkungen, Fehlersuche).
3. M6.3: Unabhängiges Review der kritischen Teile (Audio-Interop, Reconnect, Threading) mit einem stärkeren Modell, Befunde beheben.
4. M6.4: Checkliste für lokale Tests final; danach deine Abnahme (M6.5).
