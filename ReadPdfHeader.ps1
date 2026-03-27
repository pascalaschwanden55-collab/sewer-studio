param(
    [string]$PdfPath = "E:\GEP_Altdorf_2025_Zone_1.15_29261_925_Export\GEP_Altdorf_2025_Zone_1.15_29261_925.pdf",
    [int]$PageNum = 35
)

$ErrorActionPreference = "Stop"

# Pfad zum UI Build-Output (enthält alle Abhängigkeiten)
$uiBinPath = "$PSScriptRoot\src\AuswertungPro.Next.UI\bin\Debug\net10.0-windows"
$dllPath = Join-Path $uiBinPath "UglyToad.PdfPig.dll"

if (-not (Test-Path $dllPath)) {
    Write-Host "DLL nicht gefunden: $dllPath" -ForegroundColor Red
    exit 1
}

# Lade alle benötigten DLLs aus dem UI-Ordner
Push-Location $uiBinPath
try {
    Add-Type -Path "UglyToad.PdfPig.Core.dll"
    Add-Type -Path "UglyToad.PdfPig.Fonts.dll"
    Add-Type -Path "UglyToad.PdfPig.Tokenization.dll"
    Add-Type -Path "UglyToad.PdfPig.Tokens.dll"
    Add-Type -Path "UglyToad.PdfPig.dll"
    
    Write-Host "DLLs erfolgreich geladen" -ForegroundColor Green
} finally {
    Pop-Location
}

if (-not (Test-Path $PdfPath)) {
    Write-Host "PDF nicht gefunden: $PdfPath" -ForegroundColor Red
    exit 1
}

$doc = [UglyToad.PdfPig.PdfDocument]::Open($PdfPath)
Write-Host "`n=== Seite $PageNum von $($doc.NumberOfPages) ===" -ForegroundColor Cyan

$page = $doc.GetPage($PageNum)
$text = $page.Text
$lines = $text -split "`r?`n"

Write-Host "`nErste 30 Zeilen (Header-Analyse, oberste Zeile ignoriert):" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Yellow
for ($i = 1; $i -lt [Math]::Min(31, $lines.Count); $i++) {
    $line = $lines[$i].Trim()
    if ($line) {
        Write-Host "$i`: $line"
    }
}

Write-Host "`n=== Suche nach 'Haltung' und 'Datum' im Header ===" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan
for ($i = 0; $i -lt [Math]::Min(50, $lines.Count); $i++) {
    $line = $lines[$i].Trim()
    if ($line -match "Haltung|Datum|Schacht|Inspektion") {
        Write-Host "$i`: $line" -ForegroundColor Yellow
    }
}

$doc.Dispose()
Write-Host "`nFertig!" -ForegroundColor Green
