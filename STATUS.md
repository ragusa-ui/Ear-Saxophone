# STATUS

**Letztes Update:** 2026-10-24 (Meilenstein 1 abgeschlossen)
**Letzter bestätigter Build:** GitHub Actions, [Lauf #1](https://github.com/ragusa-ui/Ear-Saxophone/actions/runs/40086116728) auf Commit `09035c4`: **erfolgreich** (Linux-Tests, Windows-Build, Windows-Tests, self-contained Publish, ZIP-Artefakt 65 MB). Lokal (Cloud, Linux): 0 Warnungen, 0 Fehler.

## Fortschritt
Berechnung: Summe der Gewichte vollständig erledigter Arbeitspakete (Gesamtgewicht 100). Ein Paket zählt erst, wenn es umgesetzt und wie beschrieben geprüft ist. Teilfortschritt wird nicht angerechnet.

| Meilenstein | Gewicht | Erledigt | Fortschritt |
|---|---|---|---|
| M1 Gerüst, Core-Modell, CI | 20 | 20 | 100 % |
| M2 MIDI-Eingang, Reconnect | 12 | 0 | 0 % |
| M3 Audio (Master, Mikrofon, Apps) | 25 | 0 | 0 % |
| M4 Medien und Hotkeys | 10 | 0 | 0 % |
| M5 Oberfläche, Tray, Autostart | 20 | 0 | 0 % |
| M6 Release, Review, Abnahme | 13 | 0 | 0 % |
| **Gesamt** | **100** | **20** | **20 %** |

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
- ☐ 2.1 NAudio-MIDI-Adapter hinter `IMidiInput` (3)
- ☐ 2.2 nanoKONTROL2-Erkennung und Wiederverbindungslogik (mit Fake testbar) (4)
- ☐ 2.3 Verdrahtung Eingang, Learn und Dispatcher (3)
- ☐ 2.4 Lokale Testfälle für Gerät/Reconnect in die Checkliste (2)

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
**Keine laufende Aufgabe.** M1 ist abgeschlossen. Als Nächstes beginnt M2.1 (`IMidiInput` und NAudio-Adapter).

## Tests
| Bereich | Ergebnis |
|---|---|
| `EarSaxophone.Core.Tests` (44 Tests: Parser, Skalierung, Learn, Dispatcher, Profile, Store), Linux, Release | **bestanden** (44/44) |
| Dieselben Tests auf Windows (GitHub Actions, Lauf #1) | **bestanden** |
| Linux-Tests in GitHub Actions (Lauf #1) | **bestanden** |
| WPF-Stub: Kompilieren unter Linux | **bestanden** (nur kompiliert, nicht gestartet) |
| Self-contained Publish und ZIP in CI (Lauf #1) | **erfolgreich** (Prüfung auf `EarSaxophone.exe`, `coreclr.dll`, `hostpolicy.dll`, `PresentationFramework.dll`); ZIP 65 MB. Das ZIP wurde nicht auf einem Windows-PC gestartet. |
| Alles mit Audio, Mikrofon, nanoKONTROL2, Start der App | noch nicht möglich/ausgeführt (nur lokal auf Windows prüfbar) |

## Bekannte Probleme, offene Fragen, Blocker
- Keine Blocker.
- Das Platzhalterfenster der App wurde nie gestartet, nur kompiliert und veröffentlicht.
- CI-Warnung: Die Actions `checkout`, `setup-dotnet`, `upload-artifact` (v4) laufen noch auf Node 20 (veraltet, werden auf Node 24 erzwungen). Kein Fehler; Versionen später anheben.
- Die Discord-Zuordnung ist weiterhin eine Hypothese (siehe docs/ARCHITECTURE.md); sie gilt erst nach den Tests in M3.4 als abgesichert.
- NAudio 3.3.0 enthält laut Paketdoku `AudioSessionManager`, `AudioSessionControl`, `AudioEndpointVolume`, `MMDeviceEnumerator` (mit Benachrichtigungen) und `MidiIn`. Signaturen sind noch nicht per Compiler geprüft.

## Nächste Schritte
1. M2.1: `IMidiInput` definieren und NAudio-Adapter (`MidiIn`) schreiben; Signaturen kompilieren.
2. M2.2: nanoKONTROL2-Erkennung und Reconnect-Logik mit Fake-Gerät testen.
3. M2.3: Eingang, MIDI-Learn und Dispatcher verbinden.
4. M3.1: Audio-Abstraktionen und Fakes.
