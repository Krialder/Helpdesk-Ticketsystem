# Startet das Ticketsystem per Doppelklick (aufgerufen von Start.cmd im
# Wurzelverzeichnis). Prueft, ob .NET 8 vorhanden ist, installiert es bei
# Bedarf lokal in den Projektordner (kein Admin noetig, dadurch portabel) und
# startet die Anwendung; deren Anmeldefenster oeffnet sich selbst.

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Wurzelverzeichnis ist der Ordner über diesem Skript (scripts\ liegt darin).
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Projekt = Join-Path $RepoRoot "src\Ticketsystem.App"
$LokalesDotnet = Join-Path $RepoRoot ".dotnet"

function Get-Dotnet8 {
    # Sucht eine dotnet-Programmdatei mit installiertem SDK ab Version 8.
    # Reihenfolge: zuerst die lokale Installation im Projekt, dann die globale
    # auf dem PATH. Gibt den Pfad zurück oder $null, wenn nichts Passendes da ist.
    $kandidaten = @()
    $lokal = Join-Path $LokalesDotnet "dotnet.exe"
    if (Test-Path $lokal) { $kandidaten += $lokal }
    $kandidaten += "dotnet"

    foreach ($d in $kandidaten) {
        try {
            $sdks = & $d --list-sdks 2>$null
            foreach ($zeile in $sdks) {
                # Format je Zeile: "8.0.128 [C:\Program Files\dotnet\sdk]".
                $major = ($zeile -split '\.')[0] -as [int]
                if ($major -ge 8) { return $d }
            }
        } catch {
            # Dieser Kandidat existiert nicht oder ist nicht lauffähig: weiter.
        }
    }
    return $null
}

Write-Host "Prüfe, ob .NET 8 vorhanden ist ..." -ForegroundColor Cyan
$dotnet = Get-Dotnet8

if (-not $dotnet) {
    Write-Host ".NET 8 wurde nicht gefunden." -ForegroundColor Yellow
    Write-Host "Installiere es lokal in den Ordner '.dotnet' (kein Administrator nötig) ..." -ForegroundColor Yellow

    try {
        # TLS 1.2 erzwingen: Das vorinstallierte Windows PowerShell 5.1 nutzt
        # es nicht von selbst, moderne Server lehnen ältere Verbindungen aber
        # ab. Ohne diese Zeile scheitert der Download mit einem SSL/TLS-Fehler.
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

        # Fortschrittsbalken aus: Invoke-WebRequest ist mit ihm um ein
        # Vielfaches langsamer, das sieht sonst aus wie ein Hänger.
        $ProgressPreference = "SilentlyContinue"

        # Offizielles Installationsskript von Microsoft holen und das SDK in
        # den Projektordner legen. Das hält den Rechner sauber und macht den
        # Ordner auf einem anderen PC ohne weitere Installation lauffähig.
        $installer = Join-Path $env:TEMP "dotnet-install.ps1"
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer -UseBasicParsing
        & $installer -Channel 8.0 -InstallDir $LokalesDotnet -NoPath
    } catch {
        Write-Host ""
        Write-Host "Der Download oder die Installation von .NET 8 ist fehlgeschlagen:" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "Bitte die Internetverbindung prüfen und erneut versuchen. Alternativ" -ForegroundColor Yellow
        Write-Host "das .NET 8 SDK von https://dot.net von Hand installieren." -ForegroundColor Yellow
        exit 1
    }

    $dotnet = Join-Path $LokalesDotnet "dotnet.exe"
    if (-not (Test-Path $dotnet)) {
        Write-Host "Nach der Installation wurde keine dotnet.exe in '.dotnet' gefunden." -ForegroundColor Red
        exit 1
    }

    # Damit die untergeordneten dotnet-Aufrufe die lokale Laufzeit finden.
    $env:DOTNET_ROOT = $LokalesDotnet
    $env:PATH = "$LokalesDotnet;$env:PATH"
    Write-Host ".NET 8 wurde lokal installiert." -ForegroundColor Green
} else {
    Write-Host "Gefunden: $dotnet" -ForegroundColor Green
}

Write-Host ""
Write-Host "Starte das Ticketsystem ..." -ForegroundColor Cyan
Write-Host "Das Anmeldefenster öffnet sich gleich; beendet wird über das Fenster." -ForegroundColor Cyan
Write-Host ""

# Beim allerersten Start laedt dotnet die Pakete und baut das Projekt; ohne
# den Hinweis sieht das wie ein Haenger aus.
Write-Host ""
Write-Host "Beim ersten Start werden Pakete geladen und gebaut; das kann einige" -ForegroundColor Yellow
Write-Host "Minuten dauern und zeigt dabei wenig Ausgabe. Bitte warten ..." -ForegroundColor Yellow
Write-Host ""
# Wenn etwas schiefgeht, muss der Grund in der Konsole stehen und nicht nur
# in einer Protokolldatei, von der der Nutzer nichts weiss.
$laufBeginn = (Get-Date).AddSeconds(-5)

# Nur um den Aufruf herum gelockert und danach zurueckgesetzt: Mit "Stop"
# verpackt Windows PowerShell 5.1 jede Zeile, die ein natives Programm nach
# stderr schreibt, in einen Fehlersatz und bricht ab. Heute geht das gut,
# weil nichts umgeleitet wird; sobald hier jemand ein 2>&1 ergaenzt, stuerbe
# das Skript ausgerechnet vor der Nachschau.
$vorher = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    & $dotnet run --project $Projekt
    # Sofort sichern: Jeder weitere native Aufruf ueberschreibt die Variable.
    $code = $LASTEXITCODE
} finally {
    $ErrorActionPreference = $vorher
}

if ($code -eq 0) { exit 0 }

Write-Host ""
Write-Host "Das Ticketsystem hat mit Code $code geendet." -ForegroundColor Red
switch ($code) {
    1 { Write-Host "Bedeutung: Der Startablauf ist gescheitert, der Grund steht im Protokoll." -ForegroundColor Yellow }
    2 { Write-Host "Bedeutung: Es ist gescheitert, BEVOR das Protokoll bereitstand; der Grund steht oben." -ForegroundColor Yellow }
    default { Write-Host "Bedeutung: unerwartet, am Fang der Anwendung vorbei." -ForegroundColor Yellow }
}

# Die Protokolldatei suchen statt die Ablagekette der Anwendung nachzurechnen:
# Eine Kopie dieser Kette liefe der Vorlage frueher oder spaeter davon.
# Gesucht wird nach SCHREIBZEIT und nicht nach dem Datum im Dateinamen, denn
# das ist UTC und steht nach Mitternacht noch auf gestern.
function Add-Protokollordner([string]$basis, [string]$unterordner) {
    # Join-Path wirft in PowerShell 5.1, wenn der erste Teil leer ist; eine
    # fehlende Umgebungsvariable darf das Skript aber nicht ausgerechnet in
    # der Nachschau beenden.
    if ([string]::IsNullOrWhiteSpace($basis)) { return @() }
    return @(Join-Path $basis $unterordner)
}

$ordner = @()
if ($env:ConnectionStrings__Default -match 'Data\s*Source\s*=\s*([^;]+)') {
    $ordner += Add-Protokollordner (Split-Path -Parent $Matches[1].Trim()) "Protokoll"
}
$ordner += Add-Protokollordner $env:LOCALAPPDATA "Ticketsystem\Protokoll"
$ordner += Add-Protokollordner $env:USERPROFILE "Ticketsystem\Protokoll"
$ordner += Add-Protokollordner $Projekt "bin\Debug\net8.0\Protokoll"

$protokoll = $ordner |
    Where-Object { $_ -and (Test-Path $_ -ErrorAction SilentlyContinue) } |
    ForEach-Object { Get-ChildItem -Path $_ -Filter "ticketsystem-*.log" -ErrorAction SilentlyContinue } |
    Where-Object { $_.LastWriteTime -ge $laufBeginn } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($protokoll) {
    Write-Host ""
    Write-Host "Protokoll: $($protokoll.FullName)" -ForegroundColor Cyan
    Write-Host "----- letzte Zeilen -----" -ForegroundColor Cyan
    # In Klammern erzwungenes Feld: Bei einer einzeiligen Datei liefert
    # Get-Content eine Zeichenkette, und die laesst sich nicht indizieren.
    $zeilen = @(Get-Content $protokoll.FullName -ErrorAction SilentlyContinue)
    # Ab der letzten Fehlerzeile, damit der Grund oben steht und nicht das
    # geordnete Herunterfahren darunter.
    $ab = ($zeilen | Select-String -Pattern '\s(fail|crit):\s' | Select-Object -Last 1).LineNumber
    if ($ab) { $zeilen[($ab - 1)..([Math]::Min($ab + 58, $zeilen.Count - 1))] }
    else { $zeilen | Select-Object -Last 40 }
} else {
    Write-Host ""
    Write-Host "Es wurde keine frische Protokolldatei gefunden. Der Fehler kam also," -ForegroundColor Yellow
    Write-Host "bevor das Protokoll bereitstand; dann steht der Grund oben in diesem" -ForegroundColor Yellow
    Write-Host "Fenster. Durchsucht wurden:" -ForegroundColor Yellow
    $ordner | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}

exit $code
