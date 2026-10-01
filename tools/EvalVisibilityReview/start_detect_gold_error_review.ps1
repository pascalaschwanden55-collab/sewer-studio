param(
    [string]$KnowledgeRoot = 'C:\KI_BRAIN',
    [string]$Queue = 'C:\KI_BRAIN\eval_review\detect_gold_failure_review\queues\detect_gold_failure_a46a82535c82',
    [string]$Output = 'C:\KI_BRAIN\eval_review\detect_gold_failure_review\reviews\detect_gold_failure_a46a82535c82_review.json',
    [string]$Reviewer = 'Besitzer',
    [int]$Port = 8775
)

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$python = Join-Path $repositoryRoot 'sidecar\.venv\Scripts\python.exe'
$server = Join-Path $PSScriptRoot 'detect_gold_error_review_server.py'

if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
    throw "Python-Umgebung fehlt: $python"
}
if (-not (Test-Path -LiteralPath $Queue -PathType Container)) {
    throw "Diagnose-Queue fehlt: $Queue"
}

& $python $server `
    --knowledge-root $KnowledgeRoot `
    --queue $Queue `
    --output $Output `
    --reviewer $Reviewer `
    --port $Port `
    --open-browser

exit $LASTEXITCODE
