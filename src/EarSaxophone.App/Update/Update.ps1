<#
  Ear Saxophone: Update aus einer heruntergeladenen ZIP-Datei.

  Benutzung: Neue ZIP von der GitHub-Release-Seite herunterladen (am besten auch die .sha256-Datei daneben),
  dann in diesem Ordner auf Update.cmd doppelklicken. Das Skript nimmt die neueste "EarSaxophone-*-win-x64*.zip" aus dem
  Downloads-Ordner, beendet das laufende Programm, ersetzt die Programmdateien und startet es auf Wunsch neu.
  Deine Belegungen (profiles.json), Einstellungen (settings.json) und error.log bleiben unberührt.

  Parameter (für Tests und Fortgeschrittene): -Zip <Pfad>, -NoStart (nicht starten), -NoPause (nicht auf Enter warten)
#>
param(
    [string]$Zip,
    [switch]$NoStart,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
$appDir = $PSScriptRoot
$userFiles = @('profiles.json', 'settings.json', 'error.log', 'profiles.json.corrupt', 'profiles.json.tmp', 'settings.json.tmp')

function Finish([int]$code) {
    if (-not $NoPause) { Read-Host 'Zum Beenden Enter drücken' | Out-Null }
    exit $code
}

function Fail([string]$text) {
    Write-Host ''
    Write-Host "FEHLER: $text" -ForegroundColor Red
    Finish 1
}

try {
    if (-not $Zip) {
        $downloads = (New-Object -ComObject Shell.Application).NameSpace('shell:Downloads').Self.Path
        $candidate = Get-ChildItem -Path $downloads -Filter 'EarSaxophone-*-win-x64*.zip' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $candidate) {
            Fail "Im Downloads-Ordner ($downloads) liegt keine Datei 'EarSaxophone-...-win-x64.zip'. Lade die neue ZIP von der GitHub-Release-Seite herunter und starte Update.cmd erneut."
        }
        $Zip = $candidate.FullName
    }
    if (-not (Test-Path -LiteralPath $Zip)) { Fail "Datei nicht gefunden: $Zip" }

    Write-Host "Programmordner: $appDir"
    Write-Host "Neue Version:   $Zip"

    $shaFile = "$Zip.sha256"
    if (Test-Path -LiteralPath $shaFile) {
        $expected = ((Get-Content -LiteralPath $shaFile -Raw).Trim() -split '\s+')[0].ToLower()
        $actual = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash.ToLower()
        if ($expected -ne $actual) { Fail 'Die Prüfsumme der ZIP-Datei stimmt nicht (Download beschädigt?). Bitte erneut herunterladen.' }
        Write-Host 'Prüfsumme (SHA-256) stimmt.'
    } else {
        Write-Host 'Hinweis: Neben der ZIP liegt keine .sha256-Datei, die Prüfsumme wird übersprungen.'
    }

    $exe = Join-Path $appDir 'EarSaxophone.exe'
    $running = @(Get-Process -Name 'EarSaxophone' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and ($_.Path -ieq $exe) })
    $wasRunning = $running.Count -gt 0
    foreach ($process in $running) {
        Write-Host 'Beende das laufende Programm ...'
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(15000) | Out-Null
    }

    $temp = Join-Path ([IO.Path]::GetTempPath()) ('EarSaxophone-update-' + [guid]::NewGuid().ToString('N'))
    try {
        Expand-Archive -LiteralPath $Zip -DestinationPath $temp -Force
        $newExe = Get-ChildItem -Path $temp -Filter 'EarSaxophone.exe' -Recurse | Select-Object -First 1
        if (-not $newExe) { Fail 'In der ZIP-Datei wurde keine EarSaxophone.exe gefunden. Ist es die richtige Datei?' }
        $source = $newExe.DirectoryName

        $copied = 0
        Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($source.Length).TrimStart('\', '/')
            if ($userFiles -contains $relative) { return }
            $target = Join-Path $appDir $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $target -Force
            $copied++
        }
    } finally {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
    }

    $version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
    Write-Host ''
    Write-Host "Fertig: $copied Dateien aktualisiert, Version $version. Belegungen und Einstellungen sind unverändert." -ForegroundColor Green

    if (-not $NoStart) {
        $start = $wasRunning
        if (-not $start -and -not $NoPause) {
            $answer = Read-Host 'Programm jetzt starten? (J/n)'
            $start = ($answer -eq '' -or $answer -match '^[jJyY]')
        }
        if ($start) { Start-Process -FilePath $exe }
    }
    Finish 0
} catch {
    $message = $_.Exception.Message
    if ($_.Exception -is [UnauthorizedAccessException]) {
        $message = "Kein Schreibzugriff auf den Programmordner. Verschiebe das Programm in einen Ordner, in den du schreiben darfst (zum Beispiel C:\Tools\EarSaxophone), oder starte Update.cmd als Administrator. ($message)"
    }
    Fail $message
}
