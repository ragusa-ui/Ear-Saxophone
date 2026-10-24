# Mediensteuerung und Tastenkombinationen: Funktionsweise und Grenzen

## Zwei getrennte Aktionstypen
| Aktion | Was passiert | Wohin |
|---|---|---|
| **Medienbefehl** (Play/Pause, Nächster, Vorheriger, Stop) | Die App drückt die systemweite Medientaste (`SendInput` mit den Windows-Tasten `MediaPlayPause`, `MediaNext`, `MediaPrevious`, `MediaStop`). | An das Programm, das **Windows** als aktuelle Mediensitzung ansieht. Die App wählt das Ziel nicht. |
| **Tastenkombination** (z. B. `Ctrl+Alt+M`) | Die App drückt die Tasten wie eine Tastatur. | An das Fenster, das gerade den Fokus hat (oder an Programme mit eigenen globalen Hotkeys). |

Beides ist unabhängig voneinander einer beliebigen MIDI-Taste zuordenbar und an kein bestimmtes Programm gebunden. Eine Taste löst genau einmal beim Drücken aus, nicht beim Loslassen und nicht wiederholt.

Schreibweise der Kombinationen: Modifikatoren `Ctrl` (auch `Strg`), `Alt`, `Shift`, `Win`, dazu genau eine Taste: Buchstaben, Ziffern, `F1`-`F24`, `Numpad0`-`Numpad9`, `Enter`, `Escape`, `Tab`, `Space`, `Backspace`, `Delete`, `Insert`, `Home`, `End`, `PageUp`, `PageDown`, Pfeiltasten, `PrintScreen`, `MediaPlayPause`, `MediaNext`, `MediaPrevious`, `MediaStop`, `VolumeMute`, `VolumeDown`, `VolumeUp`. Sonderzeichen wie `,` `.` `-` werden in Version 1 nicht unterstützt.

## Grenzen
Die folgenden Punkte beschreiben das erwartete Windows-Verhalten aus allgemeinem Wissen. **Sie sind in diesem Projekt noch nicht an echten Programmen bestätigt**; die Tests M4-1 bis M4-6 in `LOCAL_TESTS.md` klären das.

1. **Ziel nicht wählbar.** Windows entscheidet, welches Programm die Medientaste bekommt. Laufen Spotify und ein Browser-Tab gleichzeitig, kann der Befehl beim "falschen" Programm landen.
2. **Programme müssen mitspielen.** Spotify (Desktop) und die großen Browser reagieren auf Medientasten, wenn die Seite die Windows-Mediensteuerung anmeldet. Ob SoundCloud und Spotify im Browser das zuverlässig tun und ob "Nächster/Vorheriger" dort funktioniert, hängt von der Webseite und dem Browser ab und ist nicht garantiert.
3. **Pausierte oder lange inaktive Sitzungen.** Hat ein Programm seine Mediensitzung abgegeben (z. B. geschlossener Tab), erreicht die Taste es nicht mehr.
4. **Administratorrechte.** Windows blockiert simulierte Tasten für Fenster, die mit höheren Rechten laufen als diese App (z. B. ein als Administrator gestarteter Task-Manager). Die App meldet das, wenn Windows die Eingabe ablehnt; manchmal wird sie auch ohne Fehlermeldung verworfen.
5. **Spiele und exklusive Eingabe.** Programme, die Tastatureingaben direkt abgreifen, ignorieren simulierte Tasten eventuell.
6. **Tastenkombinationen sind fokusabhängig.** `Ctrl+Alt+M` wirkt nur, wenn das gerade aktive Programm sie kennt (oder eines ein globales Hotkey dafür registriert hat).

## Mögliche Erweiterung (nicht in Version 1)
Gezielte Steuerung eines bestimmten Programms über die Windows-Mediensitzungen (`GlobalSystemMediaTransportControlsSessionManager`). Damit ließe sich z. B. "nur Spotify" wählen, auch wenn ein anderes Programm zuletzt Ton abgespielt hat. Das braucht zusätzliche Windows-Laufzeitkomponenten und wurde bewusst zurückgestellt.
