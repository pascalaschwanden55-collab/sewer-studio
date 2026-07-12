[CmdletBinding()]
param(
    [string]$ReleaseDirectory = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)

if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    $artifacts = Join-Path $repoRoot "artifacts"
    $release = Get-ChildItem -LiteralPath $artifacts -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "SewerStudio-*-win-x64-*" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $release) {
        throw "Kein Release unter '$artifacts' gefunden. Zuerst tools\Publish-SewerStudio.ps1 ausfuehren."
    }
    $ReleaseDirectory = $release.FullName
}

$releasePath = [System.IO.Path]::GetFullPath($ReleaseDirectory)
if ($releasePath -match '[\\/](bin|obj)[\\/]') {
    throw "Produktivstart aus bin/obj ist nicht erlaubt. Bitte einen Publish-Ordner waehlen."
}

$exe = Join-Path $releasePath "SewerStudio.exe"
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "SewerStudio.exe fehlt im Release-Ordner: $releasePath"
}

Start-Process -FilePath $exe -WorkingDirectory $releasePath
