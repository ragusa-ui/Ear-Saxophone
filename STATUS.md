# STATUS

**Letztes Update:** 2026-10-24 (Meilenstein 2 abgeschlossen, CI-Lauf für diesen Stand ausstehend)
**Letzter bestätigter Build (noch für M1-Stand; Lauf für M2 folgt):** GitHub Actions, [Lauf #1](https://github.com/ragusa-ui/Ear-Saxophone/actions/runs/40086116728) auf Commit `09035c4`: **erfolgreich** (Linux-Tests, Windows-Build, Windows-Tests, self-contained Publish, ZIP-Artefakt 65 MB). Lokal (Cloud, Linux): 0 Warnungen, 0 Fehler.

## Fortschritt
Berechnung: Summe der Gewichte vollständig erledigter Arbeitspakete (Gesamtgewicht 100). Ein Paket zählt erst, wenn es umgesetzt und wie beschrieben geprüft ist. Teilfortschritt wird nicht angerechnet.

| Meilenstein | Gewicht | Erledigt | Fortschritt |
|---|---|---|---|
| M1 Gerüst, Core-Modell, CI | 20 | 20 | 100 % |
| M2 MIDI-Eingang, Reconnect | 12 | 12 | 100 % |
| M3 Audio (Master, Mikrofon, Apps) | 25 | 0 | 0 % |
| M4 Medien und Hotkeys | 10 | 0 | 0 % |
| M5 Oberfläche, Tray, Autostart | 20 | 0 | 0 % |
| M6 Release, Review, Abnahme | 13 | 0 | 0 % |
| **Gesamt** | **100** | **32** | **32 %** |

## Aufgabenliste
Legende: ☑ erledigt · ◐ in Arbeit · ☐ noch nicht begonnen · ⛔ blockiert

### M1 (20)
- ☑ 1.1 Solution, global.json, .NET 10, zentrale Paketversionen, Build-Konfiguration (3)
- ☑ 1.2 MIDI-Nachrichtenmodell und Parser (Note-On/Off, Control-Change, WinMM-Format, ungültige Eingaben) (3)
- ☑ 1.3 Mapping-/Profil-Modell inkl. JSON, Schema-Version, Validierung (4)
- ☑ 1.4 Wertskalierung (Bereich, Invertierung) (2)
- ☑ 1.5 MIDI-Learn-Zustandsmaschine und Dispatcher (Tasten nur bei steigender Flanke) (3)
- ☑ 1.6 ProfileStore: atomares Speichern, defekte Datei bleibt als `.corrupt` erhalten (2)
- ☑ 1.7 GitHub-Actions-Workflow: Build, Tests, self-contained Publish, ZIP-Artefakt; Lauf #1 grün bestätigt (2)
- ☑ 1.8 Dokumentationsgerüst: README, ARCHITECTURE, ENVIRONMENT, STATUS (1)

### M2 (12)
- ☑ 2.1 `IMidiInput`/`IMidiInputProvider` im Core und NAudio-Adapter (`NAudio.WinMM` 3.3.0) in `EarSaxophone.Windows`; **nur kompiliert, nicht mit Hardware getestet** (3)
- ☑ 2.2 `MidiConnectionManager`: Erkennung per Namensfragment, Polling, Wiederverbindung, Fehlermeldungen; mit Fake-Gerät getestet (4)
- ☑ 2.3 `ControlRouter`: Learn und Dispatch hinter einem Eingang, thread-sicher (3)
- ☑ 2.4 Lokale Testfälle für Gerät/Reconnect in `docs/LOCAL_TESTS.md` (2)

### M3 (25)
- ☐ 3.1 Audio-Abstraktionen und Fakes (3)
- ☐ 3.2 Ausgabegerät: Lautstärke und Mute (4)
- ☐ 3.3 Mikrofon als Aufnahmegerät (Capture): Lautstärke, Mute, Verhalten bei Geräten ohne Unterstützung (5)
- ☐ 3.4 Anwendungs-Identität (Discord-Hypothese) mit Tests: mehrere Sessions, Neustart, Gerätewechsel (8)
- ☐ 3.5 Ereignisbehandlung: neue Sessions und Standardgerätewechsel, Sollwerte neu anwenden (5)

### M4 (10)
- ☐ 4.1 Medientasten per SendInput (3)
- ☐ 4.2 Hotkey-Parser und Senden (5)
- ☐ 4.3 Dokumentation der Grenzen globaler Mediensteuerung (2)

### M5 (20)
- ☐ 5.1 Dunkles Theme und Layout (4)
- ☐ 5.2 Controller-Übersicht und MIDI-Learn in der UI (6)
- ☐ 5.3 Aktionszuweisung in der UI (5)
- ☐ 5.4 Tray (2)
- ☐ 5.5 Autostart (2)
- ☐ 5.6 Verständliche Fehleranzeige (1)

### M6 (13)
- ☐ 6.1 Release-Workflow und ZIP (2)
- ☐ 6.2 README final (Installation, Bedienung, Einschränkungen, Fehlersuche) (2)
- ☐ 6.3 Unabhängiges Review kritischer Teile (Audio-Interop, Reconnect) (3)
- ☐ 6.4 Checkliste für lokale Tests (1)
- ☐ 6.5 Lokale Abnahme durch den Nutzer auf Windows 11 mit echtem nanoKONTROL2 (5)

## Aktuelle Aufgabe
**Keine laufende Aufgabe.** M2 ist abgeschlossen; als Nächstes beginnt M3.1 (Audio-Abstraktionen und Fakes).

## Tests
| Bereich | Ergebnis |
|---|---|
| `EarSaxophone.Core.Tests` (58 Tests: Parser, Skalierung, Learn, Dispatcher, Router, Verbindungsmanager mit Fake-Gerät, Profile, Store), Linux, Release | **bestanden** (58/58) |
| Dieselben Tests auf Windows (GitHub Actions, Lauf #1, damals 44 Tests) | **bestanden** |
| Linux-Tests in GitHub Actions (Lauf #1) | **bestanden** |
| WPF-Stub und `EarSaxophone.Windows` (NAudio-MIDI-Adapter): Kompilieren unter Linux | **bestanden** (nur kompiliert, nicht gestartet) |
| NAudio-MIDI-Adapter mit echter Hardware (`docs/LOCAL_TESTS.md`, M2-1 bis M2-6) | noch nicht ausgeführt (nur lokal prüfbar) |
| Self-contained Publish und ZIP in CI (Lauf #1) | **erfolgreich** (Prüfung auf `EarSaxophone.exe`, `coreclr.dll`, `hostpolicy.dll`, `PresentationFramework.dll`); ZIP 65 MB. Das ZIP wurde nicht auf einem Windows-PC gestartet. |
| Alles mit Audio, Mikrofon, nanoKONTROL2, Start der App | noch nicht möglich/ausgeführt (nur lokal auf Windows prüfbar) |

## Bekannte Probleme, offene Fragen, Blocker
- Keine Blocker.
- Reconnect-Risiko: WinMM meldet das Entfernen eines USB-Geräts nicht; die App erkennt es über Polling der Geräteliste. Ein Ziehen und Einstecken zwischen zwei Abfragen bleibt eventuell unbemerkt (Test M2-3).
- Das Platzhalterfenster der App wurde nie gestartet, nur kompiliert und veröffentlicht.
- CI-Warnung: Die Actions `checkout`, `setup-dotnet`, `upload-artifact` (v4) laufen noch auf Node 20 (veraltet, werden auf Node 24 erzwungen). Kein Fehler; Versionen später anheben.
- Die Discord-Zuordnung ist weiterhin eine Hypothese (siehe docs/ARCHITECTURE.md); sie gilt erst nach den Tests in M3.4 als abgesichert.
- NAudio 3.3.0 enthält laut Paketdoku `AudioSessionManager`, `AudioSessionControl`, `AudioEndpointVolume`, `MMDeviceEnumerator` (mit Benachrichtigungen) und `MidiIn`. Signaturen sind noch nicht per Compiler geprüft.

## Nächste Schritte
1. CI-Lauf für den M2-Stand prüfen und STATUS.md aktualisieren.
2. M3.1: Audio-Abstraktionen (Geräte, Sessions, Endpunkt-Lautstärke) und Fakes.
3. M3.2/M3.3: Ausgabegerät und Mikrofon (Capture) mit NAudio.Wasapi; Signaturen per Compiler prüfen.
4. M3.4: Anwendungs-Identität mit Tests (mehrere Sessions, Neustart, Gerätewechsel).
