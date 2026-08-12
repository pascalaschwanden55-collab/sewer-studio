param(
    [string]$Wurzel = 'C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1_qa',
    [int]$Port = 8891
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$python = Join-Path $repositoryRoot 'sidecar\.venv\Scripts\python.exe'
$server = Join-Path $PSScriptRoot 'osd_wahrheit_server.py'

if (-not (Test-Path -LiteralPath (Join-Path $Wurzel 'qa_manifest.json') -PathType Leaf)) {
    throw "OSD-Sichtprobe fehlt: $Wurzel"
}

Write-Host 'Blinde OSD-Sichtprobe wird gestartet.'
Write-Host "Adresse: http://127.0.0.1:$Port/"
Write-Host 'Stoppen mit Strg+C'

& $python $server --wurzel $Wurzel --port $Port
exit $LASTEXITCODE
