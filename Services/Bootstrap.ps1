<#
.SYNOPSIS
    Services Bootstrap - Lädt alle Service-Module
.DESCRIPTION
    Zentrale Initialisierung aller Service-Module in richtiger Reihenfolge
#>

$servicesPath = Split-Path $MyInvocation.MyCommand.Path -Parent

Write-Host "Loading AuswertungPro Services..." -ForegroundColor Cyan

# Laden in Abhängigkeitsreihenfolge
$serviceFiles = @(
    'Models.ps1'
    'LoggingService.ps1'
    'ValidationService.ps1'
    'MergeService.ps1'
    'ProjectStorageService.ps1'
    'AutosaveService.ps1'
    'XtfImportService.ps1'
    'Xtf405ImportService.ps1'
    'PdfImportService.ps1'
    'ExcelExportService.ps1'
    'LeistungskatalogService.ps1'
    'KostenberechnungService.ps1'
    'ProjectManagerService.ps1'
)

foreach ($file in $serviceFiles) {
    $filePath = Join-Path $servicesPath $file
    if (Test-Path $filePath) {
        try {
            . $filePath
        } catch {
            Write-Host "ERROR: Fehler beim Laden von $file" -ForegroundColor Red
            Write-Host $_.Exception.Message -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "WARNING: $file nicht gefunden" -ForegroundColor Yellow
    }
}

Write-Host "[OK] Alle Services geladen" -ForegroundColor Green
Write-Host ""
