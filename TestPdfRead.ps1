param(
    [string]$PdfPath = "E:\GEP_Altdorf_2025_Zone_1.15_29261_925_Export\GEP_Altdorf_2025_Zone_1.15_29261_925.pdf",
    [int]$PageNum = 35
)

$dllPath = "src\AuswertungPro.Next.UI\bin\Debug\net10.0-windows\UglyToad.PdfPig.dll"
if (-not (Test-Path $dllPath)) {
    Write-Host "Building UI project first..."
    dotnet build "src\AuswertungPro.Next.UI\AuswertungPro.Next.UI.csproj" -c Debug
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.Reflection.Assembly]::LoadFrom((Resolve-Path $dllPath).Path) | Out-Null

$doc = [UglyToad.PdfPig.PdfDocument]::Open($PdfPath)
Write-Host "=== Seite $PageNum von $($doc.NumberOfPages) ===" -ForegroundColor Cyan

$page = $doc.GetPage($PageNum)
$text = $page.Text
$lines = $text -split "`r?`n"

Write-Host "`nErste 30 Zeilen (Header-Analyse):" -ForegroundColor Yellow
for ($i = 0; $i -lt [Math]::Min(30, $lines.Count); $i++) {
    $line = $lines[$i].Trim()
    if ($line) {
        Write-Host "$($i+1): $line"
    }
}

$doc.Dispose()
