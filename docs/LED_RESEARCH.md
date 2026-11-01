# nanoKONTROL2 – LEDs ansteuern (Recherche, ungeprüft)

Stand: Recherche per Web, **nichts davon wurde an echter Hardware getestet**. Das offizielle Korg-Handbuch konnte nicht gelesen werden; vieles stammt aus Foren-Zusammenfassungen.

## Was wahrscheinlich funktioniert
- Die Tasten-LEDs werden per normaler MIDI-Nachricht gesetzt, wenn der LED-Modus des Geräts auf **External** steht (bestätigt durch die SuperCollider-Bibliothek davidgranstrom/NanoKontrol2).
- Vermutlich: CC auf demselben Kanal und derselben CC-Nummer wie die Taste, Wert 127 = an, 0 = aus (Einzelmeldung aus einem Forum).
- SysEx ist für das Schalten der LEDs nicht nötig, nur der Korg KONTROL Editor braucht es zum Speichern der Einstellung.

## Einstellung am Gerät (einmalig, nötig)
1. Korg KONTROL Editor installieren, Gerät per USB anschließen.
2. Communication > Receive Scene Data.
3. Common-Bereich: LED Mode auf **External**, Control Mode auf **CC**.
4. Communication > Write Scene Data.
5. Gerät aus- und einstecken und prüfen (Berichte, ob die Einstellung bleibt, widersprechen sich).

## Offene Punkte für echte Hardware
- Tatsächliche CC-Nummern und Kanal der Szene (Annahme, unbestätigt: S 32–39, M 48–55, R 64–71, Play 41, Stop 42, Rec 45, Rew 43, FF 44, Cycle 46).
- Track- und Marker-Tasten haben vermutlich keine LED; LEDs hätten S, M, R, Play, Stop, Rec, Rew, FF, Cycle.
- Im External-Modus leuchtet nichts von selbst: die App muss den Zustand beim Start setzen und bei jeder Änderung nachführen.
- Windows-Portname des Ausgangs unklar: nach Teilstring "nanokontrol" suchen (ohne Groß-/Kleinschreibung); Ein- und Ausgangsliste getrennt.
- Ohne Korg-Treiber ist der Port exklusiv: Editor und App nicht gleichzeitig. Mit Korg-Treiber gibt es widersprüchliche Berichte.
- Eigene Ausgabe-CCs dürfen nicht als Eingabe gelesen werden.

## Quellen
github.com/davidgranstrom/NanoKontrol2 (gelesen); Suchzusammenfassungen zu community.cantabilesoftware.com, forum.vb-audio.com, forums.steinberg.net, forum.qlcplus.org.
