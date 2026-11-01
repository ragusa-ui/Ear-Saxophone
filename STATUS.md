# STATUS

**Letztes Update:** 2026-11-01 (Release Candidate v1.0.0-rc1 veröffentlicht; offen nur deine Abnahme)
**Letzter bestätigter Build:** GitHub Actions, [Lauf #26](https://github.com/ragusa-ui/Ear-Saxophone/actions/runs/41310821082) auf Commit `618dde9` (Stand nach den Erweiterungen unten): **erfolgreich** auf `windows-latest`: Build, Core- und Windows-Tests, self-contained Publish, Start ohne .NET im PATH, 12 UI-Prüfungen per UI-Automation, Normalstart mit Tray und Einzelinstanz, Test des Update-Skripts in Windows PowerShell 5.1, ZIP und UI-Screenshot als Artefakte; Linux-Job grün. Diese Erweiterungen sind im Release [v1.0.0-rc2](https://github.com/ragusa-ui/Ear-Saxophone/releases/tag/v1.0.0-rc2) enthalten (Commit `b431b47`, ZIP 65,6 MB mit `.sha256`, von `release.yml` Lauf #4 erstellt; Vorabversion).

## Fortschritt
Berechnung: Summe der Gewichte erledigter Arbeitspakete (Gesamtgewicht 100). Ein Paket zählt, wenn es umgesetzt und **so weit geprüft ist, wie es in der Cloud möglich ist** (Unit-Tests mit Fakes, Kompilieren). Teilfortschritt wird nicht angerechnet. Alles, was echte Hardware braucht, steht zusätzlich in `docs/LOCAL_TESTS.md`; bestätigt hast du davon bisher **nichts**, und 5 Punkte (6.5) sind ausdrücklich für deine Abnahme reserviert.

| Meilenstein | Gewicht | Erledigt | Fortschritt |
|---|---|---|---|
| M1 Gerüst, Core-Modell, CI | 20 | 20 | 100 % |
| M2 MIDI-Eingang, Reconnect | 12 | 12 | 100 % |
| M3 Audio (Master, Mikrofon, Apps) | 25 | 25 | 100 % |
| M4 Medien und Hotkeys | 10 | 10 | 100 % |
| M5 Oberfläche, Tray, Autostart | 20 | 20 | 100 % |
| M6 Release, Review, Abnahme | 13 | 8 | 62 % |
| **Gesamt** | **100** | **95** | **95 %** |

**Einordnung:** 95 % heißt "in der Cloud umgesetzt und geprüft", nicht "auf deinem PC bestätigt". Auf echter Hardware bestätigt: 0 Punkte. Die Windows-Adapter (MIDI, Audio, SendInput) und die Oberfläche wurden bisher nur kompiliert und in der CI ohne Hardware ausgeführt; ein Mensch hat die App noch nie bedient. Die fehlenden 5 Punkte sind genau deine Abnahme (6.5), sie lassen sich nicht ersetzen.

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

### M6 (13) - bis auf die Abnahme erledigt
- ☑ 6.1 Release-Workflow `release.yml` (Tag `v1.2.3`/`v1.2.3-rc1` baut, testet, prüft die Version in der EXE, erzeugt ZIP und SHA-256, erstellt ein GitHub-Release; Vorabversion bei `-` im Tag). Probelauf ohne Release grün; **der Schritt "Release erstellen" ist ungetestet**, weil dafür ein Tag nötig ist (2)
- ☑ 6.2 README final: Installation, Bedienung, Anwendungs-Zuordnung, Einschränkungen, Fehlersuche-Tabelle, Entwicklerhinweise, ehrlicher Stand (2)
- ☑ 6.3 Unabhängiges Review (frische Opus-Sitzung, 27 Werkzeugaufrufe, Decompilation von NAudio zur Prüfung): 11 Befunde (2 hoch, 4 mittel, 5 niedrig), alle bearbeitet und mit 13 neuen Tests bzw. Code-Änderungen abgesichert (siehe docs/ARCHITECTURE.md, Abschnitt Robustheit). Die Wirkung der Audio-Adapter-Reparaturen unter echter Last ist ungeprüft (3)
- ☑ 6.4 Checkliste final: `docs/LOCAL_TESTS.md` mit Schnelltest (8 Punkte, ca. 20-30 Minuten) und allen Einzelfällen (1)
- ☐ 6.5 **Lokale Abnahme durch dich** auf Windows 11 mit echtem nanoKONTROL2 (5)

## Erweiterungen nach rc1 (auf Wunsch aus dem ersten Test, veröffentlicht in rc2)
Nicht Teil der 100 gewichteten Punkte; alle in der CI geprüft, keine an echter Hardware.
- ☑ **Geräteabbild statt Kachel-Tabelle:** Der nanoKONTROL2 ist schematisch gezeichnet (Transportblock, acht Kanäle); Antippen wählt den Regler; Drehregler-Zeiger, Fader-Kappe und Tastenleuchten zeigen live, was am Gerät passiert; Punkt für belegte, Rahmen für gewählte Elemente, Liste der Belegungen darunter. Geometrie per Unit-Test geprüft (51 Elemente, keine Überlappung). Das Aussehen wurde nur über den Textbericht geprüft, nicht angesehen.
- ☑ **Anzeige bei Änderungen (OSD):** kleines Overlay (immer oben, klickdurchlässig, ohne Fokus) für Lautstärke, Stumm, Gerätewechsel und Medientasten; sechs Positionen, Ein/Aus und Test in den Einstellungen. CI: erscheint, zeigt "Lautsprecher"/"65 %", Fensterstil klickdurchlässig/ohne Fokus, in allen sechs Positionen im sichtbaren Bereich.
- ☑ **Audiogerät wechseln** (Wiedergabe **und** Aufnahme/Mikrofon): neue Aktion, reihum nach Namen, optional auf ausgewählte Geräte begrenzt; Auswahl im Editor. Logik per 11 Unit-Tests. Das Setzen des Windows-Standardgeräts nutzt die **undokumentierte** Schnittstelle `IPolicyConfig` (wie andere Umschalt-Werkzeuge); sie ist kompiliert, aber nie an einem echten Gerät ausgeführt (Test N3).
- ☑ **Update-Skript** `Update.cmd`/`Update.ps1` im ZIP (privates Repository: kein automatischer Update-Check möglich). CI-Test in Windows PowerShell 5.1: falsche Prüfsumme wird abgelehnt, Update wird angewendet, `profiles.json`/`settings.json` bleiben, fremde ZIP wird abgelehnt. Mit echtem Download und laufendem Programm nicht getestet (Test N4).
- Tests: 186 Core-Tests (Linux) bestanden; mehrere Läufe in Folge ohne Ausreißer.

## Aktuelle Aufgabe
**Keine laufende Aufgabe.** rc2 ist veröffentlicht. Offen: deine Abnahme (6.5) mit dem Schnelltest in `docs/LOCAL_TESTS.md`, inklusive der neuen Punkte N1 bis N4.

## Tests
| Bereich | Ergebnis |
|---|---|
| `EarSaxophone.Core.Tests`, Linux, Release: 161 Tests (zusätzlich zu M5: Nachrichten-Warteschlange, Neuverbinden, Port-Index, Treiber-Warnungen, parallele Verarbeitung im MainViewModel), 4 Läufe in Folge ohne Ausreißer | **bestanden** (161/161) |
| Mutationsprüfung der Nachrichten-Warteschlange (Tastendruck darf nie zusammengefasst werden) | die absichtliche Änderung wurde von 2 Tests erkannt; Code zurückgesetzt |
| Mutationsprüfung der Identitäts-Tests (Namensvergleich, Einmal-Anwendung, Render/Capture-Trennung) | alle 3 Änderungen wurden von den passenden Tests erkannt; Code wieder zurückgesetzt |
| GitHub Actions Lauf #8 (M4-Stand), Windows Server 2025: 125 Core-Tests und 16 Windows-Tests (INPUT-Struktur 40 Byte, Tastenfolge, Extended-Flag, Medientasten-Codes, Smoke-Tests der MIDI- und Audio-Adapter ohne Hardware); seit M5 kommen Registry-Tests für den Autostart dazu (Lauf #15 grün, Testschritt erfolgreich) | **bestanden** |
| GitHub Actions Lauf #15: Linux-Job, self-contained Publish, ZIP (65 MB) | **bestanden** |
| Lauf #15: veröffentlichte App startet auf Windows ohne .NET im PATH und rendert ihr Fenster (UI-Bericht: dunkles Theme, deutsche Texte, 0 Überlappungen) | **bestanden** (Screenshot als Artefakt `EarSaxophone-ui-screenshot`, nicht von mir angesehen, nur als Text geprüft) |
| Lauf #15: 7 UI-Prüfungen durch UI-Automation (Kachel wählen, MIDI-Learn starten/abbrechen, Profil neu/löschen, Fehlerleiste, Tray-Schalter) | **bestanden** |
| Lauf #15: Normalstart `--minimized` (Tray), zweite Instanz beendet sich, kein `error.log` | **bestanden** |
| Windows-Adapter (NAudio MIDI, Core Audio, SendInput) | in CI **ohne Hardware** ausgeführt (Erzeugen, Abfragen, Freigeben ohne Fehler; auf dem Runner gibt es kein MIDI-Gerät und vermutlich keine Audiogeräte); mit echten Geräten und Programmen nie ausgeführt |
| Alles mit nanoKONTROL2, Mikrofon, Discord, WPF-Start | noch nicht ausgeführt (`docs/LOCAL_TESTS.md`: Schnelltest 1-8, M2-1 bis M2-9, M3-1 bis M3-12, M4-1 bis M4-7, M5-1 bis M5-13, R-1 bis R-5, alle offen) |

## Bekannte Probleme, offene Fragen, Blocker
- Keine Blocker.
- **Discord-Zuordnung ist weiterhin eine Hypothese in der Praxis.** Im Modell bestätigt; bei echten Sessions kann Windows/Discord anders reagieren (Tests M3-5 bis M3-10).
- Reconnect-Risiko: WinMM meldet das Entfernen eines USB-Geräts nicht; Erkennung per Polling von Name und Port-Index, nach dem Ruhezustand und über die Schaltfläche "Neu verbinden". Ein Ziehen und Einstecken innerhalb einer Sekunde ohne Indexänderung bleibt unbemerkt (Tests M2-3, M2-7). Ob Windows ein geöffnetes, abgezogenes Gerät aus der Liste entfernt, ist unbestätigt (der Reviewer konnte es auch nicht verifizieren).
- Pfad eines Prozesses ist bei geschützten oder höher privilegierten Prozessen eventuell nicht lesbar; diese Anwendung gilt dann als nicht aktiv (Test M3-10).
- Browser-Tabs sind nicht einzeln steuerbar (eine Anwendung = alle ihre Sessions).
- Medientasten gehen an das Programm, das Windows als aktuelle Mediensitzung wählt; die App kann das Ziel nicht bestimmen. Ob Spotify/SoundCloud im Browser zuverlässig reagieren, ist offen (Tests M4-1 bis M4-3). Fenster mit Administratorrechten blockieren simulierte Tasten (Test M4-5). Details: `docs/MEDIA_CONTROL.md`.
- Hotkeys unterstützen in Version 1 keine Sonderzeichen-Tasten (`,` `.` `-` usw.).
- Threading und COM-Lebensdauer im Audio-Adapter wurden reviewt und nachgebessert (Sperren, atomarer Neuaufbau, Zusammenfassen von Geräteereignissen), aber nie unter echter Last oder bei Geräte-Wechseln ausgeführt (Tests R-1 bis R-5, M3-12).
- CI-Warnung: Actions v4 laufen auf Node 20 (veraltet); Versionen später anheben.
- Lizenz: **MIT** (`LICENSE`, Rechteinhaber im Text: `ragusa-ui`, bei Bedarf ändern); Copyright-Hinweise der Bibliotheken in `THIRD-PARTY-NOTICES.md`, beides liegt dem ZIP bei.
- Release: **[v1.0.0-rc1](https://github.com/ragusa-ui/Ear-Saxophone/releases/tag/v1.0.0-rc1)** (Vorabversion) auf Commit `c77d7af`, erstellt von `release.yml` (Lauf #3, `publish`): ZIP 65,6 MB und `.sha256` angehängt, Tag auf dem richtigen Commit geprüft. Der Tag wurde von GitHub gesetzt, weil ein Tag-Push aus der Cloud-Sitzung scheitert (der Git-Proxy trennt die Verbindung; Branch-Pushes funktionieren). Für das endgültige `v1.0.0` nach der Abnahme: Workflow `release` manuell starten mit Version `1.0.0` und Haken `publish`.
- Das ZIP des Release enthält den Stand von `c77d7af`; spätere Commits (nur Dokumentation) sind nicht darin.
- Die Programmdatei ist nicht signiert (SmartScreen-Warnung möglich, in der README erklärt).
- Die Standardbelegung des nanoKONTROL2 (Control-Nummern der Beschriftung, z. B. Fader 1 = CC 0, Play = CC 41) stammt aus dem üblichen Werkszustand und ist nicht an deinem Gerät geprüft. Weicht es ab, erkennt MIDI-Learn jeden Regler trotzdem und zeigt ihn als "Weiterer Regler" (Test M5-2).
- Fensterposition und -größe werden nicht gespeichert.
- Im Editor ist als Ziel nur Standardgerät oder Anwendung wählbar; ein konkretes Gerät (Modell unterstützt es) kann die Oberfläche noch nicht einstellen.
- Die Oberfläche wurde in der CI mit 1100 px Breite gerendert; Verhalten bei hoher Windows-Skalierung (125-200 %) ist ungeprüft.
- Das Tray-Symbol und sein Menü wurden nie gesehen (CI ohne Taskleiste).
- Der Download der CI-Logs/Artefakte von dieser Cloud-Sitzung aus ist blockiert (Host `productionresultssa17.blob.core.windows.net` nicht erlaubt); ich prüfe die Oberfläche deshalb über den Textbericht im Log.

## Nächste Schritte
1. **Du:** ZIP von [v1.0.0-rc2](https://github.com/ragusa-ui/Ear-Saxophone/releases/tag/v1.0.0-rc2) laden (von rc1 aus: in denselben Ordner entpacken, Überschreiben bestätigen; ab rc2 geht es mit `Update.cmd`), Schnelltest und die neuen Punkte N1-N4 aus `docs/LOCAL_TESTS.md` durchgehen; bei Fehlern Version, Meldung und `error.log` melden.
2. Danach: Befunde beheben, endgültiges `v1.0.0` über `release.yml` (Option `publish`) erzeugen, STATUS auf 100 %.
