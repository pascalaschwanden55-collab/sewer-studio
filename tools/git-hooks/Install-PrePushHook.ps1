param()

$ErrorActionPreference = 'Stop'
$repoRoot = (git rev-parse --show-toplevel).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    throw 'Git-Projektordner konnte nicht ermittelt werden.'
}

$source = Join-Path $repoRoot 'tools\git-hooks\pre-push'
$target = Join-Path $repoRoot '.git\hooks\pre-push'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Hook-Vorlage fehlt: $source"
}

Copy-Item -LiteralPath $source -Destination $target -Force
Write-Host "Pre-Push-Testschutz installiert: $target"
Write-Host 'Im Notfall kann ein einzelner Push mit git push --no-verify umgangen werden.'
