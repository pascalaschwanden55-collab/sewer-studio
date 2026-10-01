param(
    [string]$Queue = 'C:\KI_BRAIN\training\diagnostics\bcc_pdf_precision_queue_v1',
    [string]$Output = 'C:\KI_BRAIN\eval_review\bcc_pdf_precision_v1_review.json',
    [string]$Reviewer = 'Pascal',
    [int]$Port = 8776
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$python = Join-Path $repositoryRoot 'sidecar\.venv\Scripts\python.exe'
$server = Join-Path $PSScriptRoot 'bcc_video_fehlalarm_review_server.py'

if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
    throw "Python-Umgebung fehlt: $python"
}
if (-not (Test-Path -LiteralPath (Join-Path $Queue 'queue.json') -PathType Leaf)) {
    throw "Precision-Warteschlange fehlt: $Queue"
}

Write-Host 'Blinde Bogen-Pruefung wird gestartet.'
Write-Host "Warteschlange: $Queue"
Write-Host "Review-Datei: $Output"
Write-Host "Adresse: http://127.0.0.1:$Port/"
Write-Host 'Stoppen mit Strg+C'

& $python $server `
    --queue $Queue `
    --output $Output `
    --reviewer $Reviewer `
    --port $Port

exit $LASTEXITCODE
