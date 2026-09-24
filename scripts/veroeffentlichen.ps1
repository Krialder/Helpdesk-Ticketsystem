# Baut die auslieferbare Fassung fuer Windows.
#
# Ergebnis ist ein Ordner mit einer Programmdatei, dazu als ZIP zum Kopieren.
# Bewusst eigenstaendig: Auf dem Zielrechner muss kein .NET installiert sein.
# Das kostet Groesse (ueber 100 MB), spart aber die Vorbedingung, an der eine
# Verteilung sonst als Erstes scheitert.

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Projekt = Join-Path $RepoRoot 'src\Ticketsystem.App'
$Ziel = Join-Path $RepoRoot 'veroeffentlicht'
$Zip = Join-Path $RepoRoot 'Ticketsystem-windows.zip'

Write-Host ''
Write-Host '============================================'
Write-Host '  Ticketsystem veroeffentlichen (Windows)'
Write-Host '============================================'
Write-Host ''

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'Kein dotnet gefunden. Fuer das Veroeffentlichen wird das .NET SDK 8 gebraucht.' -ForegroundColor Red
    Write-Host 'Das ist nur auf diesem Rechner noetig, nicht auf dem Zielrechner.' -ForegroundColor Red
    exit 1
}

if (Test-Path $Ziel) {
    # Ohne das Leeren blieben Dateien einer aelteren Fassung liegen; genau
    # diese Stolperfalle hat der ZIP-Weg schon einmal gekostet.
    Remove-Item $Ziel -Recurse -Force
}

Write-Host 'Baue eigenstaendige Fassung, das dauert einige Minuten ...' -ForegroundColor Cyan
& dotnet publish $Projekt `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    --output $Ziel

if ($LASTEXITCODE -ne 0) {
    Write-Host 'Das Veroeffentlichen ist fehlgeschlagen, siehe Meldungen oben.' -ForegroundColor Red
    exit $LASTEXITCODE
}

$Exe = Join-Path $Ziel 'Ticketsystem.App.exe'
if (-not (Test-Path $Exe)) {
    Write-Host "Erwartete Programmdatei fehlt: $Exe" -ForegroundColor Red
    exit 1
}

if (Test-Path $Zip) { Remove-Item $Zip -Force }
Compress-Archive -Path (Join-Path $Ziel '*') -DestinationPath $Zip

$Groesse = [math]::Round((Get-Item $Zip).Length / 1MB, 1)

Write-Host ''
Write-Host 'Fertig.' -ForegroundColor Green
Write-Host "  Ordner: $Ziel"
Write-Host "  ZIP:    $Zip ($Groesse MB)"
Write-Host ''
Write-Host 'Auf dem Helpdesk-Laptop: ZIP in einen eigenen Ordner entpacken und'
Write-Host 'Ticketsystem.App.exe doppelklicken. Die Daten landen im Benutzerprofil,'
Write-Host 'nicht in diesem Ordner; der genaue Pfad steht im Protokoll und unter'
Write-Host 'Daten in der Anwendung.'
Write-Host ''
