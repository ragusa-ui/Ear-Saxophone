@echo off
rem Startet das Update-Skript (siehe Update.ps1). Alles in einer Zeile, damit eine neue Update.cmd das Skript nicht stoert.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update.ps1" %* & exit /b
