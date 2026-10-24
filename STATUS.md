# STATUS

**Letztes Update:** 2026-10-24 (Meilenstein 4 abgeschlossen, CI bestätigt)
**Letzter bestätigter Build:** GitHub Actions, [Lauf #8](https://github.com/ragusa-ui/Ear-Saxophone/actions/runs/40087431886) auf Commit `8cf4a72` (Stand M4): **erfolgreich** auf `windows-latest` (Windows Server 2025): Build 0 Warnungen/0 Fehler, 16/16 Windows-Tests, 125/125 Core-Tests, self-contained Publish (246 Dateien, 131 MB entpackt), ZIP 65 MB; Linux-Job mit Core-Tests ebenfalls grün.

## Fortschritt
Berechnung: Summe der Gewichte erledigter Arbeitspakete (Gesamtgewicht 100). Ein Paket zählt, wenn es umgesetzt und **so weit geprüft ist, wie es in der Cloud möglich ist** (Unit-Tests mit Fakes, Kompilieren). Teilfortschritt wird nicht angerechnet. Alles, was echte Hardware braucht, steht zusätzlich in `docs/LOCAL_TESTS.md`; bestätigt hast du davon bisher **nichts**, und 5 Punkte (6.5) sind ausdrücklich für deine Abnahme reserviert.

| Meilenstein | Gewicht | Erledigt | Fortschritt |
|---|---|---|---|
| M1 Gerüst, Core-Modell, CI | 20 | 20 | 100 % |
| M2 MIDI-Eingang, Reconnect | 12 | 12 | 100 % |
| M3 Audio (Master, Mikrofon, Apps) | 25 | 25 | 100 % |
| M4 Medien und Hotkeys | 10 | 10 | 100 % |
| M5 Oberfläche, Tray, Autostart | 20 | 0 | 0 % |
| M6 Release, Review, Abnahme | 13 | 0 | 0 % |
| **Gesamt** | **100** | **67** | **67 %** |

**Einordnung:** 67 % heißt "in der Cloud umgesetzt und geprüft", nicht "auf deinem PC bestätigt". Auf echter Hardware bestätigt: 0 Punkte. Die Windows-Adapter (MIDI, Audio, SendInput) wurden bisher nur kompiliert und in der CI per Smoke-Test ohne Hardware ausgeführt.

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

### M5 (20)
- ☐ 5.1 Dunkles Theme und Layout (4)
- ☐ 5.2 Controller-Übersicht und MIDI-Learn in der UI (6)
- ☐ 5.3 Aktionszuweisung in der UI (5)
- ☐ 5.4 Tray (2)
- ☐ 5.5 Autostart (2)
- ☐ 5.6 Verständliche Fehleranzeige (1)

### M6 (13)
- ☐ 6.1 Release-Workflow und ZIP (2)
- ☐ 6.2 README final (2)
- ☐ 6.3 Unabhängiges Review kritischer Teile (Audio-Interop, Reconnect) mit einem stärkeren Modell (3)
- ☐ 6.4 Checkliste für lokale Tests final (1)
- ☐ 6.5 Lokale Abnahme durch den Nutzer auf Windows 11 mit echtem nanoKONTROL2 (5)

## Aktuelle Aufgabe
**Keine laufende Aufgabe.** M4 ist abgeschlossen. Als Nächstes beginnt M5.1; vorher wird die Frage zur Sprache der Oberfläche benötigt.

## Tests
| Bereich | Ergebnis |
|---|---|
| `EarSaxophone.Core.Tests`, Linux, Release: 125 Tests (Parser, Skalierung, Learn, Dispatcher, Router, Verbindungsmanager, Profile/Store, App-Matching, Endpunkte/Mikrofon, Anwendungs-Identität, Hotkeys, Executor) | **bestanden** (125/125) |
| Mutationsprüfung der Identitäts-Tests (Namensvergleich, Einmal-Anwendung, Render/Capture-Trennung) | alle 3 Änderungen wurden von den passenden Tests erkannt; Code wieder zurückgesetzt |
| GitHub Actions Lauf #8 (M4-Stand), Windows Server 2025: 125 Core-Tests und 16 Windows-Tests (INPUT-Struktur 40 Byte, Tastenfolge, Extended-Flag, Medientasten-Codes, Smoke-Tests der MIDI- und Audio-Adapter ohne Hardware) | **bestanden** |
| GitHub Actions Lauf #8: Linux-Job, self-contained Publish, ZIP (65 MB) | **bestanden** |
| Windows-Adapter (NAudio MIDI, Core Audio, SendInput) | in CI **ohne Hardware** ausgeführt (Erzeugen, Abfragen, Freigeben ohne Fehler; auf dem Runner gibt es kein MIDI-Gerät und vermutlich keine Audiogeräte); mit echten Geräten und Programmen nie ausgeführt |
| Alles mit nanoKONTROL2, Mikrofon, Discord, WPF-Start | noch nicht ausgeführt (`docs/LOCAL_TESTS.md`: M2-1 bis M2-6, M3-1 bis M3-12, M4-1 bis M4-7, alle offen) |

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
- Das Platzhalterfenster der App wurde nie gestartet.

## Nächste Schritte
1. Antwort zur UI-Sprache einholen (Vorschlag Deutsch); danach M5.1 (dunkles Theme, WPF-UI einbinden) und das Fenster-Layout.
2. M5.2: Controller-Übersicht und MIDI-Learn in der UI, verdrahtet mit `MidiConnectionManager`, `ControlRouter`, `ActionExecutor`.
3. M5.3: Aktionszuweisung (Lautstärke/Mute für Lautsprecher, Mikrofon, Anwendung; Medienbefehl; Tastenkombination).
4. M5.4 bis M5.6: Tray, Autostart, Fehleranzeige.
