param()

$ErrorActionPreference = 'Stop'
$repoRoot = (git rev-parse --show-toplevel).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    throw 'Git-Projektordner konnte nicht ermittelt werden.'
}

$hookRoot = Join-Path $repoRoot '.githooks'
$hook = Join-Path $hookRoot 'pre-push'

if (-not (Test-Path -LiteralPath $hook -PathType Leaf)) {
    throw "Versionierter Pre-Push-Hook fehlt: $hook"
}

& git -C $repoRoot config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) {
    throw 'Git konnte core.hooksPath nicht auf .githooks setzen.'
}

Write-Host "Pre-Push-Testschutz aktiviert: $hook"
Write-Host 'Im Notfall kann ein einzelner Push mit git push --no-verify umgangen werden.'
