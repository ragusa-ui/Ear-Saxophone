# Entwicklungsumgebung (Cloud) - Änderungen und Einschränkungen

## Änderungen an der Cloud-Umgebung
| Datum | Änderung | Grund |
|---|---|---|
| 2026-10-24 | `apt-get update` und `apt-get install dotnet-sdk-10.0` (10.0.113, Ubuntu 24.04) | Build und Tests von .NET 10. Das Microsoft-`dotnet-install`-Skript wird vom Proxy blockiert (403). |

Die Installation gilt nur für diese Sitzung (flüchtiger Container) und muss in einer neuen Sitzung wiederholt werden.

## Was in der Cloud möglich ist
- Kompilieren aller Projekte inklusive WPF (`EnableWindowsTargeting`), NuGet-Restore über den Proxy funktioniert.
- Ausführen der plattformneutralen Tests (`EarSaxophone.Core.Tests`).

## Was nicht möglich ist
- WPF-App starten, echte WASAPI-Sessions, Mikrofone, den nanoKONTROL2 verwenden.
- Diese Teile werden per Fakes getestet (Logik) oder müssen lokal auf Windows 11 geprüft werden (siehe STATUS.md).

## Bibliotheken (geprüft am 2026-10-24)
| Paket | Version | Lizenz | .NET-10-Kompatibilität |
|---|---|---|---|
| NAudio.WinMM, NAudio.Wasapi | 3.3.0 | MIT | Ziel `net9.0` (läuft auf .NET 10); eingebunden; WinMM in CI kompiliert (Lauf #3), Wasapi lokal in der Cloud kompiliert, CI-Lauf ausstehend |
| WPF-UI | 4.3.0 | MIT | Ziel `net10.0-windows7.0`; noch nicht eingebunden |
| H.NotifyIcon.Wpf | 2.4.1 | MIT | Ziel `net10.0-windows7.0`; noch nicht eingebunden |
| xunit / Test.Sdk | 2.9.3 / 18.11.0 | Apache-2.0 | in Benutzung |

Entscheidung .NET 10 (LTS, Support bis 14.11.2028) statt .NET 8 (LTS, Support endet 10.11.2026).
