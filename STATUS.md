# STATUS

**Letztes Update:** 2026-10-24 (Meilenstein 3 umgesetzt, CI-Lauf für diesen Stand ausstehend)
**Letzter bestätigter Build:** GitHub Actions, [Lauf #3](https://github.com/ragusa-ui/Ear-Saxophone/actions/runs/40086408389) auf Commit `f4a5dd4` (Stand M2): **erfolgreich** (Linux-Tests, Windows-Build, Windows-Tests, self-contained Publish, ZIP-Artefakt). Für den M3-Stand (NAudio.Wasapi, Audio-Code) liegt noch kein CI-Lauf vor; lokal in der Cloud (Linux): 0 Warnungen, 0 Fehler.

## Fortschritt
Berechnung: Summe der Gewichte erledigter Arbeitspakete (Gesamtgewicht 100). Ein Paket zählt, wenn es umgesetzt und **so weit geprüft ist, wie es in der Cloud möglich ist** (Unit-Tests mit Fakes, Kompilieren). Teilfortschritt wird nicht angerechnet. Alles, was echte Hardware braucht, steht zusätzlich in `docs/LOCAL_TESTS.md`; bestätigt hast du davon bisher **nichts**, und 5 Punkte (6.5) sind ausdrücklich für deine Abnahme reserviert.

| Meilenstein | Gewicht | Erledigt | Fortschritt |
|---|---|---|---|
| M1 Gerüst, Core-Modell, CI | 20 | 20 | 100 % |
| M2 MIDI-Eingang, Reconnect | 12 | 12 | 100 % |
| M3 Audio (Master, Mikrofon, Apps) | 25 | 25 | 100 % |
| M4 Medien und Hotkeys | 10 | 0 | 0 % |
| M5 Oberfläche, Tray, Autostart | 20 | 0 | 0 % |
| M6 Release, Review, Abnahme | 13 | 0 | 0 % |
| **Gesamt** | **100** | **57** | **57 %** |

**Einordnung:** 57 % heißt "in der Cloud umgesetzt und geprüft", nicht "auf deinem PC bestätigt". Auf echter Hardware bestätigt: 0 Punkte. Die Windows-Adapter (MIDI, Audio) sind bisher nur kompiliert.

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

### M3 (25) - umgesetzt, CI-Lauf ausstehend
- ☑ 3.1 Audio-Abstraktionen (`IAudioSystem`, Endpunkt, Session) und Fakes (3)
- ☑ 3.2 Ausgabegerät: Lautstärke und Mute im `AudioController` (Core, getestet) und NAudio-Adapter (nur kompiliert) (4)
- ☑ 3.3 Mikrofon als Capture-Gerät: Lautstärke, Mute, Meldungen bei fehlendem Gerät und bei Geräten ohne Windows-Regelung (Core getestet; Adapter nur kompiliert) (5)
- ☑ 3.4 Anwendungs-Identität (Discord-Hypothese) mit Tests: mehrere Sessions/Prozesse/Geräte, Neustart, Update, Gerätewechsel; Tests per Mutation auf Aussagekraft geprüft. **Nur im Modell bestätigt, echte Windows-Sessions offen** (8)
- ☑ 3.5 Ereignisbehandlung: neue Sessions und Gerätewechsel lösen Abgleich aus (Core getestet); Windows-Events und 1-s-Abgleich im Adapter (nur kompiliert) (5)

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
- ☐ 6.2 README final (2)
- ☐ 6.3 Unabhängiges Review kritischer Teile (Audio-Interop, Reconnect) mit einem stärkeren Modell (3)
- ☐ 6.4 Checkliste für lokale Tests final (1)
- ☐ 6.5 Lokale Abnahme durch den Nutzer auf Windows 11 mit echtem nanoKONTROL2 (5)

## Aktuelle Aufgabe
**Keine laufende Aufgabe.** M3 ist fertig umgesetzt; offen ist nur die CI-Bestätigung. Als Nächstes beginnt M4.1 (Medientasten per SendInput).

## Tests
| Bereich | Ergebnis |
|---|---|
| `EarSaxophone.Core.Tests`, Linux, Release: 89 Tests (Parser, Skalierung, Learn, Dispatcher, Router, Verbindungsmanager, Profile/Store, App-Matching, Endpunkte/Mikrofon, Anwendungs-Identität) | **bestanden** (89/89) |
| Mutationsprüfung der Identitäts-Tests (Namensvergleich, Einmal-Anwendung, Render/Capture-Trennung) | alle 3 Änderungen wurden von den passenden Tests erkannt; Code wieder zurückgesetzt |
| GitHub Actions Lauf #3 (M2-Stand, 58 Tests): Linux, Windows-Build/-Tests, self-contained Publish, ZIP | **bestanden** |
| GitHub Actions für M3-Stand (89 Tests, NAudio.Wasapi) | **noch nicht ausgeführt** |
| Windows-Adapter (NAudio MIDI, Core Audio) | **nur kompiliert**, nie mit Hardware oder echten Sessions ausgeführt |
| Alles mit nanoKONTROL2, Mikrofon, Discord, WPF-Start | noch nicht ausgeführt (`docs/LOCAL_TESTS.md`: M2-1 bis M2-6, M3-1 bis M3-12, alle offen) |

## Bekannte Probleme, offene Fragen, Blocker
- Keine Blocker.
- **Discord-Zuordnung ist weiterhin eine Hypothese in der Praxis.** Im Modell bestätigt; bei echten Sessions kann Windows/Discord anders reagieren (Tests M3-5 bis M3-10).
- Reconnect-Risiko: WinMM meldet das Entfernen eines USB-Geräts nicht; Erkennung per Polling (Test M2-3).
- Pfad eines Prozesses ist bei geschützten oder höher privilegierten Prozessen eventuell nicht lesbar; diese Anwendung gilt dann als nicht aktiv (Test M3-10).
- Browser-Tabs sind nicht einzeln steuerbar (eine Anwendung = alle ihre Sessions).
- Threading der COM-Aufrufe (MIDI-Thread, Timer, Notification-Threads) ist nur durchdacht, nicht unter Last getestet (Test M3-12). Die Windows-Callbacks lösen die Arbeit bewusst in einem Task aus, weil sie auf einem gesperrten Audio-Thread laufen.
- CI-Warnung: Actions v4 laufen auf Node 20 (veraltet); Versionen später anheben.
- Offene Frage: Sprache der Oberfläche und der Meldungen (aktuell englische Texte im Core). Vorschlag: Deutsch als Standard in M5, Texte zentral sammeln. Bitte bestätigen, bevor M5 beginnt.
- Das Platzhalterfenster der App wurde nie gestartet.

## Nächste Schritte
1. CI-Lauf für den M3-Stand prüfen.
2. M4.1: Medientasten (Play/Pause, Nächster, Vorheriger, Stop) per SendInput hinter einer Schnittstelle, mit Fake getestet.
3. M4.2: Hotkey-Parser ("Ctrl+Alt+M") im Core testen, Senden per SendInput im Windows-Projekt.
4. M4.3: Grenzen der globalen Mediensteuerung dokumentieren.
