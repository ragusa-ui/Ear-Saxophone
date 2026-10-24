# Architektur (Stand Meilenstein 1)

```
src/EarSaxophone.Core   net10.0          plattformneutral, vollständig unit-getestet
src/EarSaxophone.App    net10.0-windows  WPF-Oberfläche (aktuell nur Platzhalterfenster)
tests/EarSaxophone.Core.Tests            xUnit
```
Geplant (spätere Meilensteine): `EarSaxophone.Windows` (NAudio-MIDI, Core Audio, SendInput).

## Core
- `Midi/`: `MidiMessage`, `ControlId` (Identität eines physischen Reglers), `MidiMessageParser` (Rohbytes und WinMM-Kurznachricht; nur Note-Off/On und Control-Change).
- `Mapping/`
  - `ControlMapping`: Regler -> Aktion; ein Regler darf pro Profil nur einmal belegt sein (`Profile.Validate`).
  - Aktionen: `SetVolumeAction`, `ToggleMuteAction`, `MediaAction`, `HotkeyAction`.
  - `AudioTarget`: trennt `DeviceFlow.Render` (Lautsprecher) und `DeviceFlow.Capture` (Mikrofon) ausdrücklich, dazu `DeviceRole` und optional eine konkrete Geräte-ID. Anwendungen werden über `AppSelector` (Exe-Name/-Pfad) beschrieben, nie über Session-IDs.
  - `ValueScaling`: 0-127 -> Pegel, mit Bereich und Invertierung.
  - `MappingDispatcher`: Regler-Aktionen laufend, Tasten nur bei steigender Flanke (Wert >= 64).
  - `MidiLearnSession`: erste sinnvolle Nachricht nach `Start()` wird als Regler übernommen.
- `Persistence/`: `ProfileSerializer` (System.Text.Json, Schema-Version, sprechende Fehler), `ProfileStore` (atomares Schreiben über Temp-Datei, defekte Datei bleibt als `.corrupt` erhalten).

## Offene Entwurfsentscheidung: Anwendungs-Identität (Hypothese, noch nicht umgesetzt)
Ein Anwendungsziel wird über den normalisierten Exe-Namen/-Pfad der Prozesse aufgelöst, nicht über Session-IDs. Alle aktuellen Sessions dieser Anwendung (mehrere Prozesse, mehrere Geräte) werden gemeinsam gesteuert; neue Sessions bekommen den zuletzt gesetzten Wert. Das ist **eine Hypothese** und wird in Meilenstein 3 mit Tests für mehrere Sessions, Neustarts und Gerätewechsel abgesichert, bevor sie als gelöst gilt.
