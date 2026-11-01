# Checkliste für lokale Tests (Windows 11, echter nanoKONTROL2)

Diese Punkte lassen sich in der Cloud nicht prüfen. Die Spalte "Status" bleibt "offen", bis du sie auf deinem PC bestätigt hast.
Hinweis: nanoKONTROL2 im Standardmodus verwenden (Korg KONTROL Editor nicht gleichzeitig offen, siehe Punkt M2-4).

## Schnelltest für die Abnahme (ca. 20-30 Minuten)
Wenn du nicht alle Fälle unten durchgehen willst, prüfe zuerst diese acht Punkte in dieser Reihenfolge. Sie decken die wichtigsten Risiken ab (Gerät, Beschriftung, Lautstärke, Mikrofon, Discord, Medientasten, Tray, Ruhezustand).
| Nr. | Was tun | Verweis | Ergebnis (ok / nicht ok + Notiz) |
|---|---|---|---|
| 1 | ZIP entpacken, `EarSaxophone.exe` starten, nanoKONTROL2 angeschlossen: grüner Punkt, Fenster sieht gut aus | M2-1, M5-1 | |
| 2 | MIDI-Learn: je einen Fader, einen Drehregler, S/M/R-Taste und Play bewegen: passen die Beschriftungen der Kacheln? | M5-2 | |
| 3 | Fader 1 auf Lautsprecher, Windows-Lautstärke beobachten; Mute-Taste auf Lautsprecher | M5-3, M3-1 | |
| 4 | Mikrofon: Fader und `M 1` auf Mikrofon, in Windows-Einstellungen > Sound > Eingabe beobachten | M3-2 | |
| 5 | **Discord:** Fader auf `Discord.exe`; Discord beenden und neu starten (App bleibt offen): reagiert derselbe Fader wieder, ohne neu zu lernen? | M3-5, M3-6 | |
| 6 | Medientasten mit Spotify (Desktop) und mit einem Browser-Tab; `Strg+Umschalt+Esc` als Tastenkombination | M4-1, M4-2, M4-4 | |
| 7 | Fenster mit X schließen (Tray bleibt), Tray-Menü, Programm beenden und neu starten: Zuordnungen noch da? | M5-4, M5-6 | |
| 8 | Rechner in den Ruhezustand und wieder aufwecken: reagieren die Regler wieder? | M2-7 | |

Neu seit rc2 (Geräteabbild, Anzeige, Gerätewechsel; das Update ist jetzt Test V7):
| Nr. | Was tun | Ergebnis (ok / nicht ok + Notiz) |
|---|---|---|
| N1 | Geräteabbild: Sieht es gut aus? Antippen eines Faders/Drehreglers/einer Taste wählt es aus (Rahmen), der Editor rechts zeigt es. Bewegst du einen Fader/Drehregler am Gerät, bewegen sich Kappe/Zeiger im Bild; gedrückte Tasten leuchten. | |
| N2 | Anzeige: Fader auf Lautsprecher legen und bewegen: Erscheint die Einblendung mit Name, Balken und Prozent und verschwindet nach ca. 2 s? Position unten im Fenster ändern (alle sechs) und "Anzeige testen". Fensterbild/Spiel im Vordergrund stört sie nicht (kein Fokusklau, Klicks gehen hindurch)? | |
| N3 | Gerätewechsel: Track-Taste (oder eine andere) auf "Audiogerät wechseln", Wiedergabe, Geräte ankreuzen, "Übernehmen". Taste drücken: Wechselt das Windows-Standard-Wiedergabegerät (Einstellungen > System > Sound) reihum und zeigt die Anzeige den Namen? Dasselbe mit "Aufnahme (Mikrofon)". | |

Neu in V2 (alles ungeprüft an echter Hardware bzw. unter Windows, daher bitte der Reihe nach):
| Nr. | Was tun | Ergebnis (ok / nicht ok + Notiz) |
|---|---|---|
| V1 | Gerätebild und Auswahl per Druck: Sieht das Bild wie dein nanoKONTROL2 aus (Symbole, Badges, LED-Flächen)? Fader bewegen, Taste (z. B. Rec) drücken: Wird das Element im Bild gewählt (Auswahl folgt dem Gerät), springt die Auswahl beim Weiterbedienen nicht? Nadel: angepinnt wählt nur der Klick. | |
| V2 | Mehrere Belegungen: Auf einen Fader mehrere Programme legen (Hinzufügen > Programm…, auch "Datei öffnen …"). Bewegen sich alle gemeinsam? Auf einer Taste: Stumm in den Modi Umschalten / gehalten stumm / gehalten Ton an. Bei Fadern wird kein Hotkey angeboten? | |
| V3 | Drag-and-Drop: Belegung auf ein freies passendes Element ziehen (verschiebt), auf ein belegtes (tauscht), mit Strg (kopiert). Fader auf Taste ziehen: wird abgelehnt mit Hinweis? | |
| V4 | Übernehmen: Etwas ändern, Übernehmen-Leiste erscheint auffällig; Übernehmen (Enter) und Verwerfen. Auswahl wechseln mit gültiger ungespeicherter Änderung: wird übernommen? | |
| V5 | Pop-up neues Programm: Ein noch nicht belegtes Programm Ton abspielen lassen. Erscheint nach kurzer Zeit das Pop-up mit Miniatur, ohne Fokusklau? Regler anklicken oder am Gerät bewegen: Programm wird dem Regler hinzugefügt. "Nicht jetzt" und "Nie fragen" (Zahnrad: Ignorierte zurücksetzen). Meldet Windows die aktive Ausgabe zuverlässig (kein Pop-up ohne Ton, keins zu spät)? | |
| V6 | LED bei Stumm: Gerät einrichten (README, Abschnitt LEDs: Korg KONTROL Editor, Receive Scene Data, LED Mode = External, Control Mode = CC, Write Scene Data, neu einstecken; App währenddessen beendet). Dann `M 1` o. Ä. auf Mikrofon-Stumm legen: Leuchtet die Taste bei Stumm und erlischt sie sonst? Stimmen die angenommenen CC-Nummern (S 32–39, M 48–55, R 64–71, Play 41, Stop 42, Rew 43, FF 44, Rec 45, Cycle 46)? Track/Marker ohne LED? Notiere Abweichungen. | |
| V7 | Update-Knopf (ab rc4; von rc3 auf rc4 einmal von Hand): Zahnrad > Nach Updates suchen; bei neuer Version Banner/"Jetzt aktualisieren": Wird aktualisiert und neu gestartet, bleiben `profiles.json`/`settings.json`? Antivirus-Meldungen? Automatische Prüfung höchstens 1x/24 h, im Zahnrad abschaltbar. | |
| V8 | Profil-Migration: Mit einer `profiles.json` aus rc2/rc3 starten (Kopie aufheben). Sind alle Belegungen noch da, und liegt `profiles.json.v1.bak` daneben? | |

**Bei jedem Fehler bitte notieren:** Version (Fenstertitel), die genaue Meldung (rote Leiste oder Statuszeile) und die letzte Zeile von `error.log`, falls vorhanden.

## M2: MIDI-Eingang und Wiederverbindung
| Nr. | Test | Erwartung | Status |
|---|---|---|---|
| M2-1 | App starten, nanoKONTROL2 per USB angeschlossen | Zustand "Verbunden"; Bewegen eines Faders erzeugt Nachrichten (Control-Change) | offen |
| M2-2 | App starten ohne Gerät, danach Gerät einstecken | Hinweis "nicht gefunden", nach höchstens ca. 1-2 s Verbindung | offen |
| M2-3 | USB-Kabel im Betrieb ziehen und wieder einstecken (kurz und mehrere Sekunden) | "Getrennt" erscheint; nach Wiedereinstecken verbindet die App selbst. **Bekanntes Risiko:** kurzes Ziehen und Einstecken zwischen zwei Abfragen kann unbemerkt bleiben; dann fließen keine Nachrichten mehr | offen |
| M2-4 | Anderes Programm hält das MIDI-Gerät offen (z. B. MIDI Mixer, DAW) | Verständliche Meldung "konnte nicht geöffnet werden", automatischer Neuversuch, wenn das andere Programm es freigibt | offen |
| M2-5 | Gerät an anderem USB-Port einstecken | Wird anhand des Namens gefunden (Index spielt keine Rolle) | offen |
| M2-6 | Alle Regler/Tasten des nanoKONTROL2 einzeln bewegen | Jeder sendet eine eigene Control-Nummer; Tasten senden 127 beim Drücken und 0 beim Loslassen | offen |
| M2-7 | Rechner in den Ruhezustand/Standby schicken und wieder aufwecken | Nach wenigen Sekunden reagieren die Regler wieder (die App verbindet nach dem Aufwachen selbst neu); sonst "Neu verbinden" probieren und notieren | offen |
| M2-8 | Schaltfläche "Neu verbinden" bei laufender Verbindung | Kurzes Neuaufbauen, danach funktionieren die Regler weiter, keine Fehlermeldung | offen |
| M2-9 | Anderes USB-MIDI-Gerät (falls vorhanden) während des Betriebs ein- und abstecken | nanoKONTROL2 bleibt nutzbar (kurze Unterbrechung ist in Ordnung) | offen |

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
| M5-2 | Nacheinander (ab V2 ohne MIDI-Learn-Knopf: Auswahl folgt dem Gerät) Fader 1, einen Drehregler, S/M/R und Play am Gerät bewegen/drücken | Jeweils die passende Kachel wird gewählt (Beschriftung stimmt mit dem Gerät überein). Weicht etwas ab: "Weiterer Regler" erscheint, bitte notieren welche | offen |
| M5-3 | Fader 1 auf Lautsprecher legen, "Übernehmen"; Fader bewegen | Windows-Lautstärke folgt; Kachel zeigt Pegel und "Lautstärke: Lautsprecher" | offen |
| M5-4 | Zuordnungen anlegen, App beenden (Tray: Beenden), neu starten | Zuordnungen sind noch da (`profiles.json` im Programmordner) | offen |
| M5-5 | (entfällt in V2: "Testen" gibt es nicht mehr) | - | entfällt |
| M5-6 | Fenster mit X schließen (Einstellung "Im Infobereich bleiben" an); Tray-Symbol: Doppelklick, Rechtsklick-Menü | Programm läuft weiter, Symbol sichtbar, Doppelklick öffnet, Menü "Öffnen"/"Beenden" funktioniert; mit Einstellung aus beendet das X das Programm | offen |
| M5-7 | "Mit Windows starten" einschalten, abmelden/neu starten (oder Task-Manager > Autostart prüfen) | Eintrag "EarSaxophone" vorhanden, Programm startet minimiert im Tray; Ausschalten entfernt den Eintrag | offen |
| M5-8 | Programm ein zweites Mal starten | Kein zweites Fenster; das vorhandene kommt in den Vordergrund (auch aus dem Tray) | offen |
| M5-9 | ZIP in einen nicht beschreibbaren Ordner entpacken (z. B. unter `C:\Program Files`), starten, etwas zuordnen | Profile werden unter `%APPDATA%\EarSaxophone` gespeichert; keine Fehlermeldung | offen |
| M5-10 | Profile: neues Profil anlegen, umbenennen, wechseln, löschen; je Profil andere Belegung | Wechsel wirkt sofort auf das Gerät; letztes Profil lässt sich nicht löschen | offen |
| M5-11 | Programmordner verschieben, bei eingeschaltetem Autostart einmal starten | Autostart-Eintrag zeigt danach auf den neuen Ort | offen |
| M5-12 | Anwendung zuordnen: Discord starten, "Aktualisieren" | Discord.exe erscheint in der Liste; Auswahl funktioniert, auch ohne Liste per Eintippen | offen |
| M5-13 | Gerät abziehen; Mikrofon-Aktion ohne Mikrofon auslösen | Roter Punkt und Hinweistext; rote Fehlerleiste mit verständlicher Meldung, "Schließen" blendet sie aus | offen |

## Nachträge aus dem Review (Robustheit, Windows 11)
| Nr. | Test | Erwartung | Status |
|---|---|---|---|
| R-1 | USB-Headset einstecken, während du einen Fader bewegst | Kein Absturz, kein Hängen; Fader reagiert nach höchstens ca. 1 s wieder | offen |
| R-2 | Standard-Wiedergabegerät mehrfach schnell wechseln (Tastenkombination Win+Ctrl+V oder Einstellungen) | Master-Fader folgt dem jeweils aktuellen Gerät, keine Fehlermeldung | offen |
| R-3 | Windows-Audiodienst neu starten (Administrator-Eingabeaufforderung: `net stop audiosrv && net start audiosrv`) | Programm läuft weiter; nach wenigen Sekunden funktionieren Lautstärke-Aktionen wieder (bis dahin eventuell Meldung) | offen |
| R-4 | Fader schnell und lange hin- und herziehen, dabei Discord/Browser Ton ausgeben lassen | Gleichmäßige Reaktion, keine Verzögerung, CPU-Last gering | offen |
| R-5 | Programm 1 Stunde laufen lassen, Task-Manager beobachten | Speicherverbrauch bleibt stabil (kein stetiges Wachstum) | offen |
