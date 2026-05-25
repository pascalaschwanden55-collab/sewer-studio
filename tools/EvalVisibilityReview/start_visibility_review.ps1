param(
    [string]$Manifest = "EvalVisibilityReview_20260525\review_manifest.csv",
    [string]$Output = "EvalVisibilityReview_20260525\visibility_labels.csv",
    [int]$Port = 8771,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$manifestPath = if ([System.IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repoRoot $Manifest }
$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $repoRoot $Output }

if (-not (Test-Path $manifestPath)) {
    throw "Manifest nicht gefunden: $manifestPath"
}

$python = Join-Path $repoRoot "sidecar\.venv\Scripts\python.exe"
if (-not (Test-Path $python)) {
    $python = "python"
}

$url = "http://127.0.0.1:$Port/"
Write-Host "Starte Sichtbarkeits-Review..."
Write-Host "URL:      $url"
Write-Host "Eingabe:  $manifestPath"
Write-Host "Ausgabe:  $outputPath"
Write-Host ""
Write-Host "Tasten im Browser: 1=Ja, 2=Nein, 3=Unsicher"
Write-Host "Stoppen: Strg+C im PowerShell-Fenster"
Write-Host ""

if (-not $NoBrowser) {
    Start-Process $url
}

& $python (Join-Path $PSScriptRoot "visibility_review_server.py") `
    --manifest $manifestPath `
    --output $outputPath `
    --port $Port
