# Ear Saxophone

Eine Alternative zu MIDI Mixer für **Windows 11**: Der Korg **nanoKONTROL2** steuert die Lautstärke von Lautsprechern, Mikrofon und einzelnen Programmen (zum Beispiel Discord), schaltet stumm, sendet Medientasten und beliebige Tastenkombinationen. Alle Regler und Tasten sind frei belegbar (MIDI-Learn), die Belegung wird in Profilen gespeichert.

> **Stand (ehrlich):** Dies ist ein **Release Candidate**; der Quellstand ist die überarbeitete **Oberfläche V2** (in Abnahme). Die Anwendung wird in der CI auf Windows gebaut, getestet (517 Core-Tests) und gestartet. **Sie wurde aber noch nie mit einem echten nanoKONTROL2, echten Audiogeräten oder echten Programmen wie Discord bedient.** Die Prüfliste dafür steht in [docs/LOCAL_TESTS.md](docs/LOCAL_TESTS.md). Fortschritt und Testergebnisse: [STATUS.md](STATUS.md).

## Funktionen
- **Lautstärke** für das Standard-Wiedergabegerät (Lautsprecher/Kopfhörer), das Standard-Aufnahmegerät (**Mikrofon**) und einzelne **Anwendungen**; Bereich einstellbar (zum Beispiel 20–80 %), auch umkehrbar.
- **Stummschaltung umschalten** für dieselben Ziele (auch Mikrofon-Mute).
- **Audiogerät wechseln:** Eine Taste schaltet reihum durch die Wiedergabegeräte (oder die Mikrofone), zum Beispiel Lautsprecher, Kopfhörer, Monitor. Du kannst auswählen, welche Geräte dabei sein sollen.
- **Anzeige am Bildschirm** (wie bei MIDI Mixer): Beim Ändern von Lautstärke, Stummschaltung oder Gerät erscheint kurz eine kleine Einblendung mit Ziel, Balken und Prozent. Position (vier Ecken, oben/unten Mitte) und Ein/Aus stellst du im Zahnrad ein.
- **Das Gerät als Bild (V2):** Das Bild ist dem echten nanoKONTROL2 nachempfunden, mit Symbolen und kleinen Badges für die Belegung und farbigen Flächen für die Tasten-LEDs. Drehregler, Fader und Tasten zeigen live, was am Gerät passiert. **Auswählen** geht per Klick **oder durch Drücken/Bewegen am Gerät** (die Auswahl folgt dem Gerät; Nadel zum Anpinnen). Ersetzt den MIDI-Learn-Knopf.
- **Zwei Fenster:** Das Hauptfenster zeigt nur das Gerät. Sobald du ein Element anklickst oder am Gerät bewegst, öffnet sich daneben (ohne den Fokus zu stehlen) das Fenster "Was soll gesteuert werden?"; es lässt sich schließen und kommt beim nächsten Antippen wieder.
- **Acht Stile** (Zahnrad > Stil): Studio dunkel, Papier und Hardware, Weiß minimal, Retro Amber, Blueprint, Sunset, Rack Silber, Messing und Holz. Jeder Stil ändert Farben, Rundungen, Schrift und das Aussehen des Geräts. Die Wahl steht in `settings.json`.
- **Bedienfenster "Was soll gesteuert werden?":** zeigt, was auf dem gewählten Element liegt. Über **Hinzufügen** kommen Ziele dazu: Programm… (Liste laufender Programme oder **Datei öffnen …**), Lautsprecher, Mikrofon. Sobald etwas geändert ist, erscheint die **Übernehmen-Leiste**.
- **Mehrere Belegungen pro Regler/Taste** (zum Beispiel ein Fader für alle Spiele). Angeboten wird nur, was zur Reglerart passt: Fader/Drehregler stufenlos (nur Lautstärke, kein Hotkey); Tasten Drücken, Halten oder Umschalten, Stumm in den Modi Umschalten, stumm solange gehalten, Ton nur solange gehalten.
- **Drag-and-Drop** im Geräteabbild: Belegung auf ein passendes Element ziehen (verschieben, belegtes Ziel wird getauscht; **Strg** kopiert).
- **Pop-up bei neuem Programm mit Ton:** Spielt ein noch nicht belegtes Programm Ton ab, fragt ein kleines Fenster (ohne Fokusklau) mit Miniatur (8 Fader/Knöpfe), auf welchen Regler es soll; "Nicht jetzt" oder "Nie fragen". Im Zahnrad abschaltbar.
- **Tasten-LEDs:** Die Taste leuchtet, wenn ihr Ziel stumm ist (nach Einrichtung am Gerät, siehe unten).
- **Zahnrad:** alle Einstellungen (Anzeige, Position, Autostart, Infobereich, minimiert starten, Pop-up, Updates). Das Profil ist ein kleines Feld oben.
- **Medienbefehle** (Wiedergabe/Pause, nächster, vorheriger Titel, Stopp) und **Tastenkombinationen** (zum Beispiel `Strg+Alt+M`) als Aktionen auf Tasten, nicht an ein Programm gebunden.
- Robuste **Geräteverbindung**: Das Gerät wird automatisch gefunden und nach dem Abziehen oder Einstecken wieder verbunden.
- Dunkle Oberfläche, **Tray-Betrieb**, **Start mit Windows**, verständliche Fehlermeldungen auf Deutsch.

## Voraussetzungen
- Windows 11, 64 Bit. Keine .NET-Installation nötig (die Laufzeit ist im ZIP enthalten, entpackt etwa 130 MB).
- Ein Korg nanoKONTROL2 per USB (im Werkszustand, Betriebsart "Standard"). Andere MIDI-Controller werden in Version 1 nicht unterstützt.

## Installation
1. Das ZIP herunterladen: [Release v1.0.0-rc2](https://github.com/ragusa-ui/Ear-Saxophone/releases/tag/v1.0.0-rc2) (Datei `EarSaxophone-1.0.0-rc2-win-x64.zip`; die Prüfsumme steht in der `.sha256`-Datei daneben). Neuere Entwicklungsstände gibt es als Artefakt `EarSaxophone-win-x64` eines erfolgreichen Laufs unter **Actions** (GitHub-Anmeldung nötig).
2. ZIP **vollständig entpacken** (nicht aus dem ZIP heraus starten), am besten in einen Ordner, in den du schreiben darfst, zum Beispiel `C:\Tools\EarSaxophone`.
3. `EarSaxophone.exe` starten.

Die Programmdatei ist **nicht digital signiert**. Windows SmartScreen kann deshalb beim ersten Start warnen ("Der Computer wurde durch Windows geschützt"): "Weitere Informationen" und dann "Trotzdem ausführen". Das ZIP enthält keine Installation; es wird nichts in Windows eingetragen, außer du schaltest "Mit Windows starten" ein.

**Aktualisieren:** Die App prüft beim Start höchstens einmal in 24 Stunden auf eine neue Version (im Zahnrad abschaltbar). Bei einer neuen Version erscheint ein Banner bzw. der Knopf **Jetzt aktualisieren**. Die App lädt das ZIP von der GitHub-Release-Seite, prüft die Prüfsumme (`.sha256`), ersetzt die Programmdateien durch Umbenennen der alten (`.old`, ohne Skripte) und startet neu; `profiles.json`, `settings.json` und `error.log` bleiben unberührt. Das funktioniert nur bei einem **öffentlichen** Repository (aktuell öffentlich); sonst meldet die App "Update-Prüfung nicht möglich". Den Update-Knopf gibt es **ab Version rc4**; von rc3 auf rc4 einmal von Hand (ZIP entpacken, `profiles.json` und `settings.json` aus dem alten Ordner kopieren). Die frühere `Update.cmd`/`Update.ps1` gibt es nicht mehr (Antivirus blockierte sie). Alles Windows-seitige am Update ist **ungeprüft**. Von Hand geht es weiterhin: neue ZIP in denselben Ordner entpacken und Überschreiben bestätigen. Liegt das Programm in einem Ordner ohne Schreibrecht, wird das Update rückgängig gemacht und gemeldet.

**Deinstallieren:** "Mit Windows starten" in der Anwendung ausschalten, Programm beenden (Tray-Symbol > Beenden), Ordner löschen.

**Wo liegen meine Einstellungen?** Neben `EarSaxophone.exe` (portabel): `profiles.json` (Belegungen), `settings.json` (Schalter), bei unerwarteten Fehlern `error.log`. Ist der Ordner nicht beschreibbar (zum Beispiel unter `C:\Program Files`), werden sie in `%APPDATA%\EarSaxophone` abgelegt. Zum Umziehen den ganzen Ordner oder diese Dateien kopieren.

## Erste Schritte
1. Schließe andere Programme, die den nanoKONTROL2 per MIDI verwenden (zum Beispiel MIDI Mixer, eine Musiksoftware oder den Korg KONTROL Editor): Windows erlaubt dem Gerät nur einen Nutzer.
2. Starte `EarSaxophone.exe`. Oben links erscheint ein grüner Punkt mit "Verbunden mit nanoKONTROL2".
3. Klicke auf **Regler wählen (MIDI-Learn)** und bewege einen Fader oder drücke eine Taste am Gerät. Die passende Kachel wird ausgewählt. Du kannst auch direkt eine Kachel anklicken.
4. Im Bereich rechts wählst du die **Aktion** und das **Ziel**, dann **Übernehmen**. Mit **Testen** führst du die Aktion einmal aus, ohne zu speichern.
5. Weitere Regler genauso. Alles wird sofort gespeichert.

Beispiele: Fader 1 = Lautstärke von `Discord.exe`; Fader 2 = Lautsprecher; Drehregler 1 = Mikrofon; `M 1` = Mikrofon stumm; `Play` = Wiedergabe/Pause; `Cycle` = `Strg+Alt+M`.

### Anwendungen steuern (zum Beispiel Discord)
Eine Anwendung wird über den **Namen der Programmdatei** gewählt (`Discord.exe`), nicht über fest gemerkte Windows-Sitzungen. Dadurch bleibt die Zuordnung nach einem Neustart oder Update des Programms erhalten. Alle Fenster und Prozesse des Programms und alle Ausgabegeräte, auf denen es Ton ausgibt, werden gemeinsam geregelt. Startet das Programm später, bekommt es automatisch den zuletzt eingestellten Wert. Die Programm-Auswahl zeigt Programme, die gerade Audio nutzen; über **Datei öffnen …** wählst du eine Programmdatei, der Name lässt sich auch eintippen.

### Profilformat und Migration
Profile aus Version 1 (`profiles.json`) werden beim Laden verlustfrei ins Schema 2 (mehrere Aktionen je Element) migriert; vor dem ersten Überschreiben legt die App einmalig die Sicherung `profiles.json.v1.bak` an. Eine neuere Schemaversion wird abgelehnt. Migration: in Core-Tests geprüft, mit deinen echten Profilen ungeprüft (Test V8).

### LEDs am Gerät einrichten (ungeprüft an echter Hardware)
Im Auslieferungszustand leuchtet nichts von selbst; die App setzt die LEDs nur im LED-Modus "External". Einmalig am Gerät:
1. **App beenden** (Editor und App können den Port nicht gemeinsam nutzen). Korg KONTROL Editor starten, Gerät per USB anschließen.
2. **Communication > Receive Scene Data.**
3. Im Bereich **Common**: **LED Mode = External** und **Control Mode = CC**.
4. **Communication > Write Scene Data.**
5. Gerät aus- und wieder einstecken, Editor schließen, App starten. Berichte, ob die Einstellung bleibt, widersprechen sich.

Annahmen (unbestätigt): CC auf Kanal 1, Wert 127 = an, 0 = aus; S 32–39, M 48–55, R 64–71, Play 41, Stop 42, Rew 43, FF 44, Rec 45, Cycle 46. Track und Marker haben keine LED. Hintergrund: [docs/LED_RESEARCH.md](docs/LED_RESEARCH.md).

### Profile
Im kleinen Profilfeld oben wählst du das Profil, benennst es um, legst ein neues an oder löschst es. Jedes Profil hat seine eigene Belegung; der Wechsel wirkt sofort.

### Tray und Autostart
- **Im Infobereich bleiben:** Das Schließen-Kreuz blendet das Fenster nur aus; das Programm läuft weiter. Beenden über das Tray-Symbol (Rechtsklick > Beenden). Doppelklick öffnet das Fenster. Ein zweiter Programmstart öffnet das vorhandene Fenster.
- **Mit Windows starten:** Trägt das Programm für deinen Benutzer in den Autostart ein (kein Administrator nötig). Wird der Programmordner verschoben, repariert sich der Eintrag beim nächsten manuellen Start.
- **Minimiert starten:** Beim Start nur Tray-Symbol, kein Fenster.

## Bekannte Einschränkungen
- **Audiogerät wechseln:** Windows hat dafür keine offizielle Schnittstelle; das Programm nutzt dieselbe interne Schnittstelle wie andere Umschalt-Werkzeuge. Sie funktioniert auf Windows 10/11, kann aber durch ein Windows-Update einmal ausfallen; dann erscheint eine Meldung. Die Reihenfolge im Wechsel ist alphabetisch nach Gerätename. Mindestens zwei Geräte müssen angeschlossen (und ausgewählt) sein.
- **Die Einblendung** erscheint nicht über Spielen im exklusiven Vollbild (Fenstermodus oder "randloses Vollbild" geht) und nutzt den Hauptbildschirm.
- **Medientasten** gehen an das Programm, das Windows als aktuelle Mediensitzung ansieht; das Ziel lässt sich nicht wählen. Ob Spotify/SoundCloud im Browser reagieren, hängt von Webseite und Browser ab. Einzelheiten: [docs/MEDIA_CONTROL.md](docs/MEDIA_CONTROL.md).
- **Programme mit Administratorrechten** (auch Fenster im Vordergrund) blockieren simulierte Tasten; solche Programme sind eventuell auch nicht als Audioziel auffindbar.
- **Browser-Tabs** sind nicht einzeln steuerbar: Alle Tabs eines Browsers sind eine Anwendung.
- **Mikrofone** lassen sich nur regeln, wenn Windows für das Gerät eine Lautstärke oder Stummschaltung anbietet. Manche Headsets und Geräte mit eigener Treibersoftware erlauben das nicht; dann erscheint eine Meldung mit dem Gerätenamen.
- **Tastenkombinationen** unterstützen Buchstaben, Ziffern, `F1`–`F24`, Pfeiltasten, `Enter`, `Esc` und ähnliche Tasten, aber keine Sonderzeichen wie `,` oder `-`. Sie wirken auf das Programm im Vordergrund.
- **LEDs:** nur Tasten-LED bei Stumm, nur nach Einrichtung (siehe oben), an echter Hardware ungeprüft. Nur der nanoKONTROL2 (Standardbelegung, MIDI-Kanal 1) ist vorgesehen. Hat dein Gerät andere Control-Nummern, erkennt MIDI-Learn trotzdem jeden Regler und zeigt ihn unter "Weitere Regler".
- **Pop-up bei neuem Programm:** Ob Windows die aktive Ausgabe eines Programms zuverlässig meldet, ist ungeprüft; das Pop-up kann ausbleiben oder zu oft erscheinen.
- Fensterposition und -größe werden nicht gespeichert. Die Oberfläche ist nur auf Deutsch.

## Fehlersuche
| Problem | Ursache und Abhilfe |
|---|---|
| "nanoKONTROL2 nicht gefunden" | Gerät per USB anschließen, anderen USB-Port probieren. Im Windows-Geräte-Manager sollte es unter "Audio-, Video- und Gamecontroller" stehen. Das Programm sucht automatisch weiter. |
| "…konnte nicht geöffnet werden (nutzt ein anderes Programm das Gerät?)" | Anderes MIDI-Programm schließen (MIDI Mixer, DAW, Korg KONTROL Editor). Danach verbindet sich die App von selbst. |
| Gerät eingesteckt oder aus dem Ruhezustand zurück, aber nichts passiert | Oben auf **Neu verbinden** klicken (die App versucht das nach dem Ruhezustand auch selbst). Sehr kurzes Abziehen und Einstecken kann die App nicht erkennen. Hilft das nicht, Gerät abziehen und Programm neu starten. |
| Regler reagiert, aber die Beschriftung stimmt nicht | Dein Gerät sendet andere Control-Nummern. Regler am Gerät drücken/bewegen und belegen; er erscheint unter "Weitere Regler". |
| Discord (oder anderes Programm) reagiert nicht | Läuft es und gibt es Ton aus? Programmdatei-Name prüfen (`Discord.exe`). Bei Discord PTB/Canary heißt die Datei anders (`DiscordPTB.exe`). Läuft das Programm als Administrator, die App ebenfalls als Administrator starten oder das Programm normal starten. |
| Meldung "Kein Standard-Aufnahmegerät verfügbar" | In Windows unter Einstellungen > System > Sound > Eingabe ein Mikrofon aktivieren und als Standard wählen. |
| Meldung "… erlaubt keine Lautstärke-Steuerung über Windows" | Das Gerät bietet in Windows keine Lautstärke oder Stummschaltung an (Treiber/Headset). Gerätesoftware des Herstellers verwenden. |
| Gerätewechsel meldet "Windows hat das Umschalten abgelehnt" oder wechselt nicht | Die Windows-Schnittstelle zum Umschalten hat auf deinem System nicht funktioniert (mit Meldung und Version bitte melden). Als Ersatz die Windows-Einstellungen (Sound) oder das Lautsprecher-Symbol in der Taskleiste verwenden. |
| Medientaste landet beim falschen Programm | Windows wählt das Ziel (siehe Einschränkungen). Das gewünschte Programm einmal kurz abspielen, oder stattdessen eine Tastenkombination verwenden, die das Programm kennt. |
| Meldung "Windows hat die Tasteneingabe blockiert" | Im Vordergrund läuft ein Programm mit Administratorrechten. Es zuerst normal starten oder die App ebenfalls als Administrator starten. |
| "Die Profile konnten nicht geladen werden" | `profiles.json` war beschädigt; sie wurde als `profiles.json.corrupt` gesichert und es wird mit Standardwerten gestartet. Die Sicherung lässt sich bei Bedarf von Hand wiederherstellen. |
| Programm startet nicht / schließt sich sofort | `error.log` neben `EarSaxophone.exe` ansehen. SmartScreen oder ein Virenschutz kann die unsignierte Datei blockieren (Ausnahme eintragen). Das ZIP muss vollständig entpackt sein. |
| Nach dem Verschieben des Ordners startet es nicht mehr mit Windows | Einmal von Hand starten; der Autostart-Eintrag wird repariert. |

Bei Fehlern bitte die **Version** (steht im Fenstertitel), die genaue Meldung und die letzte Zeile von `error.log` angeben.

## Für Entwickler
- Voraussetzung: .NET SDK 10. Bauen und testen: `dotnet build EarSaxophone.sln -c Release`, `dotnet test tests/EarSaxophone.Core.Tests -c Release` (läuft auch unter Linux). Das Windows-Testprojekt (`tests/EarSaxophone.Windows.Tests`) und das Starten der Oberfläche laufen nur auf Windows (in der CI).
- Portable ZIP lokal erzeugen: `dotnet publish src/EarSaxophone.App -c Release -r win-x64 --self-contained true -o artifacts/publish`.
- CI: `.github/workflows/build.yml` (Build, Tests, Publish, Start der veröffentlichten App, UI-Prüfungen, Artefakte). Release: `.github/workflows/release.yml` (ein Tag `v1.2.3` oder `v1.2.3-rc1` erzeugt ein GitHub-Release; manueller Start mit Version ist ein Probelauf, mit gesetztem Haken "publish" erzeugt er das Release samt Tag auf dem gewählten Commit).
- Dokumentation: [Architektur](docs/ARCHITECTURE.md), [Umgebung und Bibliotheken](docs/ENVIRONMENT.md), [Mediensteuerung](docs/MEDIA_CONTROL.md), [Design V2](docs/DESIGN_V2.md), [LED-Recherche](docs/LED_RESEARCH.md), [lokale Tests](docs/LOCAL_TESTS.md), [Status](STATUS.md).
- Verwendete Bibliotheken (alle MIT, Details in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)): NAudio 3.3.0, WPF-UI 4.3.0, H.NotifyIcon.Wpf 2.4.1.

## Lizenz
MIT-Lizenz, siehe [LICENSE](LICENSE). Die verwendeten Bibliotheken (NAudio, WPF-UI, H.NotifyIcon, .NET) stehen ebenfalls unter der MIT-Lizenz; ihre Copyright-Hinweise stehen in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) und liegen dem ZIP bei.
