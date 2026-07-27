param(
    [string]$EvalRoot = "C:\KI_BRAIN\eval_set",
    [string]$Output = "C:\KI_BRAIN\eval_review\v1_event_metadata_review.json",
    [string]$Reviewer = "Pascal",
    [int]$Port = 8772,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$server = Join-Path $PSScriptRoot "eval_metadata_review_server.py"
$candidates = Join-Path $EvalRoot "_candidates.json"

if (-not (Test-Path -LiteralPath $candidates -PathType Leaf)) {
    throw "Eval-Kandidaten nicht gefunden: $candidates"
}

$python = Join-Path $repoRoot "sidecar\.venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
    $python = "python"
}

$url = "http://127.0.0.1:$Port/"
Write-Host "Starte KI-Pruefsatz..."
Write-Host "URL:      $url"
Write-Host "Eval-Set: $EvalRoot (nur lesen)"
Write-Host "Ausgabe:  $Output"
Write-Host ""
Write-Host "Stoppen: Strg+C im PowerShell-Fenster"
Write-Host ""

if (-not $NoBrowser) {
    Start-Process $url
}

& $python $server `
    --eval-root $EvalRoot `
    --output $Output `
    --reviewer $Reviewer `
    --port $Port
