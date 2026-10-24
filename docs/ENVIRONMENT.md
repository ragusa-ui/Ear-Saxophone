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
| NAudio.WinMM, NAudio.Wasapi | 3.3.0 | MIT | Ziel `net9.0` (läuft auf .NET 10); eingebunden; in CI kompiliert (WinMM Lauf #3, Wasapi Lauf #4) |
| WPF-UI | 4.3.0 | MIT | Ziel `net10.0-windows7.0`; eingebunden, in CI gestartet und gerendert |
| H.NotifyIcon.Wpf | 2.4.1 | MIT | Ziel `net10.0-windows7.0`; eingebunden, im Normalstart in der CI erzeugt (Tray-Aussehen ungesehen) |
| xunit / Test.Sdk | 2.9.3 / 18.11.0 | Apache-2.0 | in Benutzung |

Entscheidung .NET 10 (LTS, Support bis 14.11.2028) statt .NET 8 (LTS, Support endet 10.11.2026).

## Weitere Hinweise zur Cloud-Umgebung
- Das Herunterladen von CI-Logs und Artefakten (Host `productionresultssa17.blob.core.windows.net`) ist per Netzwerkregel der Umgebung gesperrt. Logs lese ich über die GitHub-Werkzeuge. Zum Freigeben des Hosts: Einstellungen der Cloud-Umgebung > Netzwerkzugriff (siehe https://code.claude.com/docs/en/cloud-environments#network-access).
- Das Aussehen der Oberfläche prüft die CI selbst: Die App rendert ihr Fenster als PNG (Artefakt `EarSaxophone-ui-screenshot`) und schreibt einen Textbericht (Theme-Ressourcen, Position aller Elemente, Überlappungen, ASCII-Vorschau, Klickprüfungen per UI-Automation) in das Log.
- Das Python-Paket Pillow war bereits vorhanden und wurde nur zum Erzeugen des Programmsymbols genutzt (keine Änderung an der Umgebung).
