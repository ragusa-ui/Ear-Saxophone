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
