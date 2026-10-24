# Ear Saxophone

MIDI-Mixer-Alternative für Windows 11 mit dem Korg nanoKONTROL2: Lautstärken (Master, Anwendungen, Mikrofon), Mediensteuerung und Tastenkombinationen über frei belegbare MIDI-Regler.

> **Status: in Entwicklung (Meilenstein 1 von 6).** Es gibt noch keine benutzbare Anwendung. Fortschritt und Testergebnisse: [STATUS.md](STATUS.md).

## Geplante Eigenschaften
Portable, self-contained Windows-x64-Anwendung (keine .NET-Installation nötig), dunkle Oberfläche, MIDI-Learn, Profile, Tray, Autostart.

## Bauen (Entwickler)
Voraussetzung: .NET SDK 10.

```
dotnet build EarSaxophone.sln -c Release
dotnet test tests/EarSaxophone.Core.Tests -c Release
```
Das Windows-ZIP erzeugt der Workflow `.github/workflows/build.yml` (Artefakt `EarSaxophone-win-x64`).

## Dokumentation
- [Architektur](docs/ARCHITECTURE.md)
- [Umgebung, Bibliotheken, Einschränkungen der Cloud-Entwicklung](docs/ENVIRONMENT.md)

Installation, Bedienung, bekannte Einschränkungen und Fehlersuche folgen, sobald es eine benutzbare Version gibt.
