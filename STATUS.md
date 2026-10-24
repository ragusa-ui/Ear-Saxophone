# STATUS

**Letztes Update:** 2026-10-24 (Meilenstein 1, Push noch nicht abgeschlossen)
**Letzter bestätigter Build:** Lokal (Cloud, Linux, `dotnet build -c Release`, .NET SDK 10.0.113): 0 Warnungen, 0 Fehler. GitHub Actions: **noch nicht bestätigt** (Workflow noch nicht gelaufen).

## Fortschritt
Berechnung: Summe der Gewichte vollständig erledigter Arbeitspakete (Gesamtgewicht 100). Ein Paket zählt erst, wenn es umgesetzt und wie beschrieben geprüft ist. Teilfortschritt wird nicht angerechnet.

| Meilenstein | Gewicht | Erledigt | Fortschritt |
|---|---|---|---|
| M1 Gerüst, Core-Modell, CI | 20 | 18 | 90 % |
| M2 MIDI-Eingang, Reconnect | 12 | 0 | 0 % |
| M3 Audio (Master, Mikrofon, Apps) | 25 | 0 | 0 % |
| M4 Medien und Hotkeys | 10 | 0 | 0 % |
| M5 Oberfläche, Tray, Autostart | 20 | 0 | 0 % |
| M6 Release, Review, Abnahme | 13 | 0 | 0 % |
| **Gesamt** | **100** | **18** | **18 %** |

## Aufgabenliste
Legende: ☑ erledigt · ◐ in Arbeit · ☐ noch nicht begonnen · ⛔ blockiert

### M1 (20)
- ☑ 1.1 Solution, global.json, .NET 10, zentrale Paketversionen, Build-Konfiguration (3)
- ☑ 1.2 MIDI-Nachrichtenmodell und Parser (Note-On/Off, Control-Change, WinMM-Format, ungültige Eingaben) (3)
- ☑ 1.3 Mapping-/Profil-Modell inkl. JSON, Schema-Version, Validierung (4)
- ☑ 1.4 Wertskalierung (Bereich, Invertierung) (2)
- ☑ 1.5 MIDI-Learn-Zustandsmaschine und Dispatcher (Tasten nur bei steigender Flanke) (3)
- ☑ 1.6 ProfileStore: atomares Speichern, defekte Datei bleibt als `.corrupt` erhalten (2)
- ◐ 1.7 GitHub-Actions-Workflow: Build, Tests, self-contained Publish, ZIP-Artefakt; **erledigt erst, wenn ein Lauf grün bestätigt ist** (2)
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
**1.7:** Workflow pushen, Lauf in GitHub Actions abwarten und Ergebnis prüfen. Ziel: Ein bestätigter grüner Lauf für Linux-Tests, Windows-Build, Windows-Tests und das self-contained ZIP.

## Tests
| Bereich | Ergebnis |
|---|---|
| `EarSaxophone.Core.Tests` (44 Tests: Parser, Skalierung, Learn, Dispatcher, Profile, Store), Linux, Release | **bestanden** (44/44) |
| Dieselben Tests auf Windows (CI) | noch nicht ausgeführt |
| WPF-Stub: Kompilieren unter Linux | **bestanden** (nur kompiliert, nicht gestartet) |
| Self-contained Publish und ZIP | noch nicht ausgeführt |
| Alles mit Audio, Mikrofon, nanoKONTROL2, Start der App | noch nicht möglich/ausgeführt (nur lokal auf Windows prüfbar) |

## Bekannte Probleme, offene Fragen, Blocker
- Keine Blocker.
- Das Platzhalterfenster der App wurde nie gestartet, nur kompiliert.
- Die Discord-Zuordnung ist weiterhin eine Hypothese (siehe docs/ARCHITECTURE.md); sie gilt erst nach den Tests in M3.4 als abgesichert.
- Offen für M3: ob NAudio 3.3.0 `AudioSessionManager` bereitstellt, sonst eigene COM-Interop (noch nicht geprüft).

## Nächste Schritte
1. Workflow pushen und Ergebnis von GitHub Actions prüfen (1.7).
2. M2: `IMidiInput` und NAudio-Adapter, dabei NAudio-Mididienste für 3.3.0 prüfen.
3. M2: Gerätesuche und Reconnect-Logik mit Fake-Gerät testen.
4. M3: Prüfen, was NAudio 3.3.0 für Audio-Sessions anbietet, danach Abstraktionen und Fakes.
