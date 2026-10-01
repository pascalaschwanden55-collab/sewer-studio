param(
    [string]$Queue = 'C:\KI_BRAIN\training\diagnostics\osd_layout_review_v1',
    [string]$Output = 'C:\KI_BRAIN\eval_review\osd_layout_review_v1.json',
    [string]$Reviewer = 'Pascal',
    [int]$Port = 18912
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$python = Join-Path $repositoryRoot 'sidecar\.venv\Scripts\python.exe'
$server = Join-Path $PSScriptRoot 'osd_layout_review_server.py'

if (-not (Test-Path -LiteralPath (Join-Path $Queue 'queue.json') -PathType Leaf)) {
    throw "OSD-Layout-Queue fehlt: $Queue"
}
Write-Host "OSD-Layout-Pruefplatz: http://127.0.0.1:$Port/"
& $python $server --queue $Queue --output $Output --reviewer $Reviewer --port $Port
exit $LASTEXITCODE
