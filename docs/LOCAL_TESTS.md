# Checkliste für lokale Tests (Windows 11, echter nanoKONTROL2)

Diese Punkte lassen sich in der Cloud nicht prüfen. Die Spalte "Status" bleibt "offen", bis du sie auf deinem PC bestätigt hast.
Hinweis: nanoKONTROL2 im Standardmodus verwenden (Korg KONTROL Editor nicht gleichzeitig offen, siehe Punkt M2-4).

## M2: MIDI-Eingang und Wiederverbindung
| Nr. | Test | Erwartung | Status |
|---|---|---|---|
| M2-1 | App starten, nanoKONTROL2 per USB angeschlossen | Zustand "Verbunden"; Bewegen eines Faders erzeugt Nachrichten (Control-Change) | offen |
| M2-2 | App starten ohne Gerät, danach Gerät einstecken | Hinweis "nicht gefunden", nach höchstens ca. 1-2 s Verbindung | offen |
| M2-3 | USB-Kabel im Betrieb ziehen und wieder einstecken (kurz und mehrere Sekunden) | "Getrennt" erscheint; nach Wiedereinstecken verbindet die App selbst. **Bekanntes Risiko:** kurzes Ziehen und Einstecken zwischen zwei Abfragen kann unbemerkt bleiben; dann fließen keine Nachrichten mehr | offen |
| M2-4 | Anderes Programm hält das MIDI-Gerät offen (z. B. MIDI Mixer, DAW) | Verständliche Meldung "konnte nicht geöffnet werden", automatischer Neuversuch, wenn das andere Programm es freigibt | offen |
| M2-5 | Gerät an anderem USB-Port einstecken | Wird anhand des Namens gefunden (Index spielt keine Rolle) | offen |
| M2-6 | Alle Regler/Tasten des nanoKONTROL2 einzeln bewegen | Jeder sendet eine eigene Control-Nummer; Tasten senden 127 beim Drücken und 0 beim Loslassen | offen |

## M3: Audio (Windows 11, echte Geräte)
| Nr. | Test | Erwartung | Status |
|---|---|---|---|
| M3-1 | Fader auf Master/Lautsprecher abbilden, bewegen; Mute-Taste drücken | Windows-Lautstärke folgt dem Fader (Windows-Lautstärkeregler beobachten); Mute schaltet um | offen |
| M3-2 | Fader auf Mikrofon (Standard-Aufnahmegerät) abbilden; Mute-Taste | Pegel in Windows > System > Sound > Eingabe ändert sich; Lautsprecher bleiben unberührt | offen |
| M3-3 | Mikrofon eines Headsets, das keine Windows-Lautstärke erlaubt (falls vorhanden) | Verständliche Meldung mit Gerätename statt Absturz | offen |
| M3-4 | Kein Mikrofon angeschlossen / deaktiviert | Meldung "Kein Standard-Aufnahmegerät"; nach Anschließen funktioniert es ohne Neustart der App | offen |
| M3-5 | **Discord:** Fader auf `Discord.exe` abbilden, Sprachkanal beitreten, Fader bewegen | Discord-Lautstärke in Windows (Lautstärkemixer) folgt | offen |
| M3-6 | Discord beenden und neu starten (ohne die App neu zu starten, ohne neu zu lernen) | Nach spätestens ca. 1-2 s steuert derselbe Fader wieder Discord; zuletzt eingestellter Wert wird übernommen | offen |
| M3-7 | Discord-Update oder Neustart nach längerer Laufzeit (>30 min) | Zuordnung bleibt erhalten (Zuordnung über Exe-Name, nicht über Pfad oder Session-ID) | offen |
| M3-8 | Discord in Windows auf anderes Ausgabegerät legen; Standard-Wiedergabegerät wechseln | Fader steuert Discord weiter; Master-Fader folgt dem neuen Standardgerät | offen |
| M3-9 | Chrome/Edge mit mehreren Tabs/Fenstern, die Ton ausgeben | Alle Sitzungen der Anwendung werden gemeinsam geregelt (Grenze: Chrome-Tabs sind Teil einer Anwendung, nicht einzeln steuerbar) | offen |
| M3-10 | Anwendung als Administrator gestartet (z. B. Discord "Als Administrator ausführen") | Verhalten notieren: wird sie gefunden oder meldet die App "nicht aktiv"? (Bekanntes Risiko: Pfad nicht auslesbar) | offen |
| M3-11 | USB-Headset während des Betriebs abziehen | Kein Absturz; Meldung bei der nächsten Aktion | offen |
| M3-12 | Fader schnell hin- und herziehen (viele Nachrichten pro Sekunde) | Keine spürbare Verzögerung, CPU-Last gering | offen |

## M4: Medientasten und Tastenkombinationen (Windows 11)
Hintergrund und erwartete Grenzen: `docs/MEDIA_CONTROL.md`.
| Nr. | Test | Erwartung | Status |
|---|---|---|---|
| M4-1 | Spotify (Desktop) spielt; Taste auf Play/Pause, Nächster, Vorheriger legen und drücken | Spotify pausiert/startet bzw. wechselt den Titel | offen |
| M4-2 | Spotify im Browser und SoundCloud im Browser (Chrome/Edge), jeweils einzeln spielend | Notieren, welche Befehle funktionieren (Play/Pause, Nächster, Vorheriger) | offen |
| M4-3 | Spotify und ein Browser-Tab gleichzeitig geöffnet, einer pausiert | Notieren, welches Programm den Befehl bekommt | offen |
| M4-4 | Tastenkombination `Win+D` und `Ctrl+Shift+Esc` auf Tasten legen | Desktop wird angezeigt bzw. Task-Manager öffnet sich | offen |
| M4-5 | Task-Manager als Administrator im Vordergrund, dann Tastenkombination/Medientaste auslösen | Verhalten notieren (Meldung "Windows blockiert" oder stillschweigend ignoriert) | offen |
| M4-6 | Eine Taste kurz drücken, lang halten, schnell mehrfach drücken | Genau eine Aktion pro Druck, kein Wiederholen beim Halten | offen |
| M4-7 | Ungültige Kombination eintragen (z. B. `Ctrl+Banana`) | Verständliche Fehlermeldung in der Oberfläche (ab M5 prüfbar) | offen |

## M5: Oberfläche, Tray, Autostart (Windows 11)
Die CI hat das Fenster gerendert und per UI-Automation bedient, aber nie ein Mensch. Bitte besonders auf Aussehen und Bedienung achten.
| Nr. | Test | Erwartung | Status |
|---|---|---|---|
| M5-1 | App starten (ZIP entpacken, `EarSaxophone.exe`) | Dunkles Fenster, gut lesbare Schrift, keine abgeschnittenen Texte; auch bei Windows-Skalierung 125 % und 150 %; Fenster auf Mindestgröße ziehen | offen |
| M5-2 | Auf "Regler wählen (MIDI-Learn)" klicken, dann nacheinander Fader 1, einen Drehregler, S/M/R und Play am Gerät bewegen/drücken | Jeweils die passende Kachel wird gewählt (Beschriftung stimmt mit dem Gerät überein). Weicht etwas ab: "Weiterer Regler" erscheint, bitte notieren welche | offen |
| M5-3 | Fader 1 auf Lautsprecher legen, "Übernehmen"; Fader bewegen | Windows-Lautstärke folgt; Kachel zeigt Pegel und "Lautstärke: Lautsprecher" | offen |
| M5-4 | Zuordnungen anlegen, App beenden (Tray: Beenden), neu starten | Zuordnungen sind noch da (`profiles.json` im Programmordner) | offen |
| M5-5 | "Testen" im Editor (Lautstärke, Medienbefehl) | Aktion läuft einmal, ohne zu speichern; Lautstärke steht danach auf 50 % | offen |
| M5-6 | Fenster mit X schließen (Einstellung "Im Infobereich bleiben" an); Tray-Symbol: Doppelklick, Rechtsklick-Menü | Programm läuft weiter, Symbol sichtbar, Doppelklick öffnet, Menü "Öffnen"/"Beenden" funktioniert; mit Einstellung aus beendet das X das Programm | offen |
| M5-7 | "Mit Windows starten" einschalten, abmelden/neu starten (oder Task-Manager > Autostart prüfen) | Eintrag "EarSaxophone" vorhanden, Programm startet minimiert im Tray; Ausschalten entfernt den Eintrag | offen |
| M5-8 | Programm ein zweites Mal starten | Kein zweites Fenster; das vorhandene kommt in den Vordergrund (auch aus dem Tray) | offen |
| M5-9 | ZIP in einen nicht beschreibbaren Ordner entpacken (z. B. unter `C:\Program Files`), starten, etwas zuordnen | Profile werden unter `%APPDATA%\EarSaxophone` gespeichert; keine Fehlermeldung | offen |
| M5-10 | Profile: neues Profil anlegen, umbenennen, wechseln, löschen; je Profil andere Belegung | Wechsel wirkt sofort auf das Gerät; letztes Profil lässt sich nicht löschen | offen |
| M5-11 | Programmordner verschieben, bei eingeschaltetem Autostart einmal starten | Autostart-Eintrag zeigt danach auf den neuen Ort | offen |
| M5-12 | Anwendung zuordnen: Discord starten, "Aktualisieren" | Discord.exe erscheint in der Liste; Auswahl funktioniert, auch ohne Liste per Eintippen | offen |
| M5-13 | Gerät abziehen; Mikrofon-Aktion ohne Mikrofon auslösen | Roter Punkt und Hinweistext; rote Fehlerleiste mit verständlicher Meldung, "Schließen" blendet sie aus | offen |
