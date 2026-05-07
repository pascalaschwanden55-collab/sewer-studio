#requires -Version 5
<#
Konvertiert die 2 SDF-Datenbanken Zone 14.01 + 14.02 in SQLite-.db3-Dateien.
Ausgabe wird direkt im jeweiligen WinCan-Projekt-DB-Ordner abgelegt, sodass
SewerStudio's WinCan-Folder-Import sie beim naechsten Versuch automatisch findet.

Voraussetzungen (alle vorhanden):
- SSCE 4.0 Runtime (C:\Program Files\Microsoft SQL Server Compact Edition\v4.0\)
- Python 3 im PATH
- tools\sdf-convert\convert_sdf_to_db3.ps1 + sdf_json_to_sqlite.py
#>

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$convertPs1 = Join-Path $scriptDir "convert_sdf_to_db3.ps1"
$convertPy  = Join-Path $scriptDir "sdf_json_to_sqlite.py"

if (-not (Test-Path $convertPs1)) { throw "Fehlt: $convertPs1" }
if (-not (Test-Path $convertPy))  { throw "Fehlt: $convertPy" }

$jobs = @(
    @{
        Zone = "14.01"
        Sdf  = "E:\Zone 14.01\db\Gep_Seedorf_6462_Seedorf_Zone_14.01.sdf"
        Db3  = "E:\Zone 14.01\db\Gep_Seedorf_6462_Seedorf_Zone_14.01.db3"
    },
    @{
        Zone = "14.02"
        Sdf  = "E:\Zone 14.02\Projects\Gep_Seedorf_6462_Seedorf_Zone_14.02\DB\Gep_Seedorf_6462_Seedorf_Zone_14.02.sdf"
        Db3  = "E:\Zone 14.02\Projects\Gep_Seedorf_6462_Seedorf_Zone_14.02\DB\Gep_Seedorf_6462_Seedorf_Zone_14.02.db3"
    }
)

$summary = @()

foreach ($job in $jobs) {
    Write-Host ""
    Write-Host ("=" * 70)
    Write-Host "Zone $($job.Zone)" -ForegroundColor Cyan
    Write-Host ("=" * 70)
    Write-Host "SDF:    $($job.Sdf)"
    Write-Host "Ziel:   $($job.Db3)"

    if (-not (Test-Path $job.Sdf)) {
        Write-Warning "SDF nicht gefunden - Zone uebersprungen."
        $summary += [pscustomobject]@{ Zone = $job.Zone; Status = "FEHLT"; Datei = "" }
        continue
    }

    if (Test-Path $job.Db3) {
        Write-Host "Ziel-.db3 existiert bereits - wird ueberschrieben."
        Remove-Item $job.Db3 -Force
    }

    try {
        # Schritt 1: SDF -> JSON
        Write-Host ""
        Write-Host "[1/2] SDF nach JSON exportieren ..." -ForegroundColor Yellow
        $jsonPath = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $convertPs1 -SdfPath $job.Sdf | Select-Object -Last 1
        if (-not $jsonPath -or -not (Test-Path $jsonPath)) {
            throw "PowerShell-Konvertierung lieferte keinen gueltigen JSON-Pfad: '$jsonPath'"
        }
        $jsonSize = [math]::Round((Get-Item $jsonPath).Length / 1KB, 1)
        Write-Host "JSON: $jsonPath ($jsonSize KB)"

        # Schritt 2: JSON -> SQLite
        Write-Host ""
        Write-Host "[2/2] JSON nach SQLite-.db3 schreiben ..." -ForegroundColor Yellow
        & python $convertPy $jsonPath $job.Db3
        if ($LASTEXITCODE -ne 0) { throw "Python-Konvertierung schlug fehl (Exit $LASTEXITCODE)" }

        # Cleanup
        Remove-Item $jsonPath -Force -ErrorAction SilentlyContinue

        if (Test-Path $job.Db3) {
            $db3Size = [math]::Round((Get-Item $job.Db3).Length / 1KB, 1)
            Write-Host ""
            Write-Host "FERTIG: $($job.Db3) ($db3Size KB)" -ForegroundColor Green
            $summary += [pscustomobject]@{ Zone = $job.Zone; Status = "OK"; Datei = $job.Db3 }
        }
        else {
            throw "SQLite-.db3 wurde nicht erzeugt."
        }
    }
    catch {
        Write-Error "Zone $($job.Zone) FEHLGESCHLAGEN: $_"
        $summary += [pscustomobject]@{ Zone = $job.Zone; Status = "FEHLER"; Datei = $_.Exception.Message }
    }
}

Write-Host ""
Write-Host ("=" * 70)
Write-Host "Zusammenfassung" -ForegroundColor Cyan
Write-Host ("=" * 70)
$summary | Format-Table -AutoSize

Write-Host ""
Write-Host "Naechste Schritte in SewerStudio:" -ForegroundColor Yellow
Write-Host "  1. Importieren -> WinCan-Projektordner waehlen"
Write-Host "  2. Ordner E:\Zone 14.01 (bzw. 14.02\Projects\Gep_...) waehlen"
Write-Host "  3. Import findet jetzt die .db3 und liest direkt ein"
