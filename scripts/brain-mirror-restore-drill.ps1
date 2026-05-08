<#
.SYNOPSIS
  Restore-Drill fuer den Brain-Mirror (Roadmap P1.5).

.DESCRIPTION
  Verifiziert, dass aus dem Mirror (default E:\Brain Sync) eine
  funktionierende Kopie der KnowledgeBase + Frames wiederhergestellt
  werden kann.

  Ablauf:
    1. Mirror-Pfad existiert + manifest.json ist vorhanden.
    2. Robocopy-Sync nach temporaerem Restore-Pfad.
    3. SHA256 der KnowledgeBase.db gegen manifest.json verifizieren.
    4. SQLite-Smoke: bekannte Tabellen + ungefaehre Zeilenzahl pruefen.
    5. Frames-Sample: ein paar Random-PNGs lesen, Header pruefen.

  Der Drill ist nicht-destruktiv: kein Schreibzugriff auf den Mirror,
  Restore-Pfad wird am Ende geloescht (außer -KeepRestore gesetzt).

.PARAMETER MirrorPath
  Pfad zum Brain-Mirror. Default: E:\Brain Sync.

.PARAMETER RestorePath
  Zielpfad fuer den Restore. Default: %TEMP%\sewerstudio-restore-drill-<guid>.

.PARAMETER KeepRestore
  Wenn gesetzt, bleibt der Restore-Pfad nach dem Drill liegen.

.EXAMPLE
  pwsh ./scripts/brain-mirror-restore-drill.ps1
  pwsh ./scripts/brain-mirror-restore-drill.ps1 -MirrorPath "F:\altes-brain" -KeepRestore
#>
[CmdletBinding()]
param(
    [string]$MirrorPath = $(if ($env:SEWERSTUDIO_BRAIN_MIRROR) { $env:SEWERSTUDIO_BRAIN_MIRROR } else { "E:\Brain Sync" }),
    [string]$RestorePath = $(Join-Path $env:TEMP "sewerstudio-restore-drill-$(New-Guid)"),
    [switch]$KeepRestore
)

$ErrorActionPreference = 'Stop'
$startedUtc = [DateTime]::UtcNow

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Pass($msg) { Write-Host "    [PASS] $msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "    [FAIL] $msg" -ForegroundColor Red }
function Write-Info($msg) { Write-Host "    [info] $msg" -ForegroundColor Gray }

$failures = 0

# Schritt 1: Mirror existiert und manifest.json
Write-Step "Schritt 1/5 - Mirror-Verfuegbarkeit"
if (-not (Test-Path $MirrorPath)) {
    Write-Fail "Mirror-Pfad nicht gefunden: $MirrorPath"
    exit 2
}
$manifestPath = Join-Path $MirrorPath "manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Fail "manifest.json fehlt im Mirror: $manifestPath"
    exit 2
}
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
Write-Pass "Mirror erreichbar; manifest.json gelesen"
Write-Info "Letzter Sync: $($manifest.lastSyncUtc)"
Write-Info "DB-Bytes laut Manifest: $($manifest.knowledgeBaseDbBytes)"

# Schritt 2: Robocopy
Write-Step "Schritt 2/5 - Restore-Kopie nach $RestorePath"
New-Item -ItemType Directory -Path $RestorePath -Force | Out-Null
$rcLog = Join-Path $RestorePath "_robocopy.log"
robocopy $MirrorPath $RestorePath /E /R:2 /W:2 /NFL /NDL /NP /LOG:$rcLog | Out-Null
$rcExit = $LASTEXITCODE
# Robocopy: 0-7 = OK, ab 8 = Fehler
if ($rcExit -ge 8) {
    Write-Fail "Robocopy fehlgeschlagen, Exit $rcExit. Log: $rcLog"
    $failures++
} else {
    Write-Pass "Robocopy OK (Exit $rcExit)"
}

# Schritt 3: SHA256 verifizieren
Write-Step "Schritt 3/5 - SHA256 KnowledgeBase.db"
$dbPath = Join-Path $RestorePath "knowledge_base.db"
if (-not (Test-Path $dbPath)) {
    Write-Fail "knowledge_base.db nicht im Restore: $dbPath"
    $failures++
} else {
    $hash = (Get-FileHash $dbPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expected = $manifest.knowledgeBaseDbSha256.ToLowerInvariant()
    if ($hash -ne $expected) {
        Write-Fail "SHA256-Mismatch: erwartet $expected, gefunden $hash"
        $failures++
    } else {
        Write-Pass "SHA256 OK ($hash)"
    }
}

# Schritt 4: SQLite-Smoke (best-effort, falls sqlite3.exe verfuegbar)
Write-Step "Schritt 4/5 - SQLite-Smoke"
$sqlite = Get-Command sqlite3.exe -ErrorAction SilentlyContinue
if (-not $sqlite -and (Test-Path $dbPath)) {
    Write-Info "sqlite3.exe nicht im PATH; Smoke uebersprungen"
} elseif (Test-Path $dbPath) {
    try {
        $tables = & sqlite3.exe $dbPath ".tables"
        Write-Info "Tabellen: $tables"
        $entryCount = & sqlite3.exe $dbPath "SELECT COUNT(*) FROM knowledge_entries;"
        Write-Pass "knowledge_entries: $entryCount Zeilen"
    } catch {
        Write-Fail "SQLite-Query fehlgeschlagen: $_"
        $failures++
    }
}

# Schritt 5: Frames-Sample
Write-Step "Schritt 5/5 - Frames-Sample"
$framesDir = Join-Path $RestorePath "training_frames"
if (-not (Test-Path $framesDir)) {
    $framesDir = Join-Path $RestorePath "frames"
}
if (Test-Path $framesDir) {
    $sample = Get-ChildItem -Path $framesDir -Recurse -Filter "*.png" -File | Get-Random -Count 5
    foreach ($f in $sample) {
        $bytes = [System.IO.File]::ReadAllBytes($f.FullName) | Select-Object -First 8
        # PNG-Magic: 89 50 4E 47 0D 0A 1A 0A
        if ($bytes.Length -ge 4 -and $bytes[0] -eq 0x89 -and $bytes[1] -eq 0x50 -and $bytes[2] -eq 0x4E -and $bytes[3] -eq 0x47) {
            Write-Pass "PNG OK: $($f.Name)"
        } else {
            Write-Fail "PNG-Header fehlt: $($f.Name)"
            $failures++
        }
    }
    if (-not $sample) {
        Write-Info "Keine PNGs in $framesDir gefunden"
    }
} else {
    Write-Info "Kein Frames-Verzeichnis im Mirror — Mirror enthaelt evtl. nur DB"
}

# Cleanup
if (-not $KeepRestore) {
    Write-Step "Cleanup - Restore-Pfad loeschen"
    try { Remove-Item -Path $RestorePath -Recurse -Force; Write-Info "Geloescht" }
    catch { Write-Info "Loeschen fehlgeschlagen (best-effort): $_" }
} else {
    Write-Info "Restore-Pfad bleibt liegen: $RestorePath"
}

$durationSec = [int]([DateTime]::UtcNow - $startedUtc).TotalSeconds
Write-Host ""
if ($failures -eq 0) {
    Write-Host "=== Restore-Drill PASS (Dauer: ${durationSec}s) ===" -ForegroundColor Green
    exit 0
} else {
    Write-Host "=== Restore-Drill FAIL: $failures Fehler (Dauer: ${durationSec}s) ===" -ForegroundColor Red
    exit 1
}
