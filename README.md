# Ear Saxophone

Eine Alternative zu MIDI Mixer für **Windows 11**: Der Korg **nanoKONTROL2** steuert die Lautstärke von Lautsprechern, Mikrofon und einzelnen Programmen (zum Beispiel Discord), schaltet stumm, sendet Medientasten und beliebige Tastenkombinationen. Alle Regler und Tasten sind frei belegbar (MIDI-Learn), die Belegung wird in Profilen gespeichert.

> **Stand (ehrlich):** Die Anwendung ist vollständig umgesetzt und wird in der CI auf Windows gebaut, getestet und gestartet. **Sie wurde aber noch nie mit einem echten nanoKONTROL2, echten Audiogeräten oder echten Programmen wie Discord bedient.** Die Prüfliste dafür steht in [docs/LOCAL_TESTS.md](docs/LOCAL_TESTS.md). Fortschritt und Testergebnisse: [STATUS.md](STATUS.md).

## Funktionen
- **Lautstärke** für das Standard-Wiedergabegerät (Lautsprecher/Kopfhörer), das Standard-Aufnahmegerät (**Mikrofon**) und einzelne **Anwendungen**; Bereich einstellbar (zum Beispiel 20–80 %), auch umkehrbar.
- **Stummschaltung umschalten** für dieselben Ziele (auch Mikrofon-Mute).
- **Medienbefehle** (Wiedergabe/Pause, nächster, vorheriger Titel, Stopp) und **Tastenkombinationen** (zum Beispiel `Strg+Alt+M`) als getrennte Aktionen, jeder Taste frei zuordenbar und nicht an ein Programm gebunden.
- **MIDI-Learn**, übersichtliche Darstellung aller 51 Elemente des nanoKONTROL2 mit Live-Pegel, mehrere **Profile**.
- Robuste **Geräteverbindung**: Das Gerät wird automatisch gefunden und nach dem Abziehen oder Einstecken wieder verbunden.
- Dunkle Oberfläche, **Tray-Betrieb**, **Start mit Windows**, verständliche Fehlermeldungen auf Deutsch.

## Voraussetzungen
- Windows 11, 64 Bit. Keine .NET-Installation nötig (die Laufzeit ist im ZIP enthalten, entpackt etwa 130 MB).
- Ein Korg nanoKONTROL2 per USB (im Werkszustand, Betriebsart "Standard"). Andere MIDI-Controller werden in Version 1 nicht unterstützt.

## Installation
1. Das ZIP herunterladen: unter **Releases** des GitHub-Repositorys, oder als Artefakt `EarSaxophone-win-x64` eines erfolgreichen Laufs unter **Actions** (dafür ist eine GitHub-Anmeldung nötig).
2. ZIP **vollständig entpacken** (nicht aus dem ZIP heraus starten), am besten in einen Ordner, in den du schreiben darfst, zum Beispiel `C:\Tools\EarSaxophone`.
3. `EarSaxophone.exe` starten.

Die Programmdatei ist **nicht digital signiert**. Windows SmartScreen kann deshalb beim ersten Start warnen ("Der Computer wurde durch Windows geschützt"): "Weitere Informationen" und dann "Trotzdem ausführen". Das ZIP enthält keine Installation; es wird nichts in Windows eingetragen, außer du schaltest "Mit Windows starten" ein.

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
Eine Anwendung wird über den **Namen der Programmdatei** gewählt (`Discord.exe`), nicht über fest gemerkte Windows-Sitzungen. Dadurch bleibt die Zuordnung nach einem Neustart oder Update des Programms erhalten. Alle Fenster und Prozesse des Programms und alle Ausgabegeräte, auf denen es Ton ausgibt, werden gemeinsam geregelt. Startet das Programm später, bekommt es automatisch den zuletzt eingestellten Wert. Die Liste "Aktualisieren" zeigt Programme, die gerade Audio nutzen; der Name lässt sich auch eintippen.

### Profile
Oben wählst du das Profil, benennst es um oder legst ein neues an. Jedes Profil hat seine eigene Belegung; der Wechsel wirkt sofort.

### Tray und Autostart
- **Im Infobereich bleiben:** Das Schließen-Kreuz blendet das Fenster nur aus; das Programm läuft weiter. Beenden über das Tray-Symbol (Rechtsklick > Beenden). Doppelklick öffnet das Fenster. Ein zweiter Programmstart öffnet das vorhandene Fenster.
- **Mit Windows starten:** Trägt das Programm für deinen Benutzer in den Autostart ein (kein Administrator nötig). Wird der Programmordner verschoben, repariert sich der Eintrag beim nächsten manuellen Start.
- **Minimiert starten:** Beim Start nur Tray-Symbol, kein Fenster.

## Bekannte Einschränkungen
- **Medientasten** gehen an das Programm, das Windows als aktuelle Mediensitzung ansieht; das Ziel lässt sich nicht wählen. Ob Spotify/SoundCloud im Browser reagieren, hängt von Webseite und Browser ab. Einzelheiten: [docs/MEDIA_CONTROL.md](docs/MEDIA_CONTROL.md).
- **Programme mit Administratorrechten** (auch Fenster im Vordergrund) blockieren simulierte Tasten; solche Programme sind eventuell auch nicht als Audioziel auffindbar.
- **Browser-Tabs** sind nicht einzeln steuerbar: Alle Tabs eines Browsers sind eine Anwendung.
- **Mikrofone** lassen sich nur regeln, wenn Windows für das Gerät eine Lautstärke oder Stummschaltung anbietet. Manche Headsets und Geräte mit eigener Treibersoftware erlauben das nicht; dann erscheint eine Meldung mit dem Gerätenamen.
- **Tastenkombinationen** unterstützen Buchstaben, Ziffern, `F1`–`F24`, Pfeiltasten, `Enter`, `Esc` und ähnliche Tasten, aber keine Sonderzeichen wie `,` oder `-`. Sie wirken auf das Programm im Vordergrund.
- **Kein LED-Feedback** am Gerät in Version 1. Nur der nanoKONTROL2 (Standardbelegung, MIDI-Kanal 1) ist vorgesehen. Hat dein Gerät andere Control-Nummern, erkennt MIDI-Learn trotzdem jeden Regler und zeigt ihn unter "Weitere Regler".
- Fensterposition und -größe werden nicht gespeichert. Die Oberfläche ist nur auf Deutsch.

## Fehlersuche
| Problem | Ursache und Abhilfe |
|---|---|
| "nanoKONTROL2 nicht gefunden" | Gerät per USB anschließen, anderen USB-Port probieren. Im Windows-Geräte-Manager sollte es unter "Audio-, Video- und Gamecontroller" stehen. Das Programm sucht automatisch weiter. |
| "…konnte nicht geöffnet werden (nutzt ein anderes Programm das Gerät?)" | Anderes MIDI-Programm schließen (MIDI Mixer, DAW, Korg KONTROL Editor). Danach verbindet sich die App von selbst. |
| Gerät eingesteckt oder aus dem Ruhezustand zurück, aber nichts passiert | Oben auf **Neu verbinden** klicken (die App versucht das nach dem Ruhezustand auch selbst). Sehr kurzes Abziehen und Einstecken kann die App nicht erkennen. Hilft das nicht, Gerät abziehen und Programm neu starten. |
| Regler reagiert, aber die Beschriftung stimmt nicht | Dein Gerät sendet andere Control-Nummern. Regler per MIDI-Learn auswählen und belegen; er erscheint unter "Weitere Regler". |
| Discord (oder anderes Programm) reagiert nicht | Läuft es und gibt es Ton aus? Programmdatei-Name prüfen (`Discord.exe`). Bei Discord PTB/Canary heißt die Datei anders (`DiscordPTB.exe`). Läuft das Programm als Administrator, die App ebenfalls als Administrator starten oder das Programm normal starten. |
| Meldung "Kein Standard-Aufnahmegerät verfügbar" | In Windows unter Einstellungen > System > Sound > Eingabe ein Mikrofon aktivieren und als Standard wählen. |
| Meldung "… erlaubt keine Lautstärke-Steuerung über Windows" | Das Gerät bietet in Windows keine Lautstärke oder Stummschaltung an (Treiber/Headset). Gerätesoftware des Herstellers verwenden. |
| Medientaste landet beim falschen Programm | Windows wählt das Ziel (siehe Einschränkungen). Das gewünschte Programm einmal kurz abspielen, oder stattdessen eine Tastenkombination verwenden, die das Programm kennt. |
| Meldung "Windows hat die Tasteneingabe blockiert" | Im Vordergrund läuft ein Programm mit Administratorrechten. Es zuerst normal starten oder die App ebenfalls als Administrator starten. |
| "Die Profile konnten nicht geladen werden" | `profiles.json` war beschädigt; sie wurde als `profiles.json.corrupt` gesichert und es wird mit Standardwerten gestartet. Die Sicherung lässt sich bei Bedarf von Hand wiederherstellen. |
| Programm startet nicht / schließt sich sofort | `error.log` neben `EarSaxophone.exe` ansehen. SmartScreen oder ein Virenschutz kann die unsignierte Datei blockieren (Ausnahme eintragen). Das ZIP muss vollständig entpackt sein. |
| Nach dem Verschieben des Ordners startet es nicht mehr mit Windows | Einmal von Hand starten; der Autostart-Eintrag wird repariert. |

Bei Fehlern bitte die **Version** (steht im Fenstertitel), die genaue Meldung und die letzte Zeile von `error.log` angeben.

## Für Entwickler
- Voraussetzung: .NET SDK 10. Bauen und testen: `dotnet build EarSaxophone.sln -c Release`, `dotnet test tests/EarSaxophone.Core.Tests -c Release` (läuft auch unter Linux). Das Windows-Testprojekt (`tests/EarSaxophone.Windows.Tests`) und das Starten der Oberfläche laufen nur auf Windows (in der CI).
- Portable ZIP lokal erzeugen: `dotnet publish src/EarSaxophone.App -c Release -r win-x64 --self-contained true -o artifacts/publish`.
- CI: `.github/workflows/build.yml` (Build, Tests, Publish, Start der veröffentlichten App, UI-Prüfungen, Artefakte). Release: `.github/workflows/release.yml` (Tag `v1.2.3` oder `v1.2.3-rc1` erzeugt ein GitHub-Release; manueller Start ist ein Probelauf ohne Release).
- Dokumentation: [Architektur](docs/ARCHITECTURE.md), [Umgebung und Bibliotheken](docs/ENVIRONMENT.md), [Mediensteuerung](docs/MEDIA_CONTROL.md), [lokale Tests](docs/LOCAL_TESTS.md), [Status](STATUS.md).
- Verwendete Bibliotheken (alle MIT): NAudio 3.3.0, WPF-UI 4.3.0, H.NotifyIcon.Wpf 2.4.1.

## Lizenz
Für dieses Projekt ist noch keine Lizenz festgelegt (Rechteinhaber entscheidet). Die verwendeten Bibliotheken stehen unter der MIT-Lizenz.
