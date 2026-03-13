param([string]$PdfPath)

cd "C:\Users\pasca\Desktop\AuswertungPro"

Write-Host "=== Analyzing PDF: $PdfPath ===" -ForegroundColor Cyan

if (-not (Test-Path $PdfPath)) {
    Write-Host "ERROR: PDF not found!" -ForegroundColor Red
    exit 1
}

Write-Host "`nRunning DiagnosticPdfParser..." -ForegroundColor Yellow
dotnet run --project tools\DiagnosticPdfParser\DiagnosticPdfParser.csproj -- $PdfPath
