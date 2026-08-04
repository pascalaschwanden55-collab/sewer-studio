param(
    [string]$Holdout,
    [string]$Reviewer = "Besitzer",
    [int]$Port = 8774
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$knowledgeRoot = "C:\KI_BRAIN"
$subsetsRoot = Join-Path $knowledgeRoot "eval_set\subsets"

if ([string]::IsNullOrWhiteSpace($Holdout)) {
    $latest = Get-ChildItem -LiteralPath $subsetsRoot -Directory -ErrorAction Stop |
        Where-Object { $_.Name -like "detect_release_holdout_*" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        throw "Kein Detect-Release-Pruefbestand unter $subsetsRoot gefunden."
    }
    $Holdout = $latest.FullName
}

$holdoutPath = (Resolve-Path -LiteralPath $Holdout).Path
$holdoutName = Split-Path -Leaf $holdoutPath
if ($holdoutName -notlike "detect_release_holdout_*") {
    throw "Der Ordner ist kein Detect-Release-Pruefbestand: $holdoutPath"
}

$reviewRoot = Join-Path $knowledgeRoot "eval_review"
New-Item -ItemType Directory -Path $reviewRoot -Force | Out-Null
$output = Join-Path $reviewRoot ($holdoutName + "_review.json")
$server = Join-Path $PSScriptRoot "detect_release_holdout_review_server.py"
$venvPython = Join-Path $repositoryRoot "sidecar\.venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $server -PathType Leaf)) {
    throw "Pruefplatz fehlt: $server"
}
if (-not (Test-Path -LiteralPath $venvPython -PathType Leaf)) {
    throw "Python-Umgebung fehlt: $venvPython"
}

Write-Host "Detect-Release-Pruefplatz wird gestartet."
Write-Host "Pruefbestand: $holdoutPath"
Write-Host "Review-Datei: $output"
Write-Host "Adresse: http://127.0.0.1:$Port/"
Write-Host "Stoppen mit Strg+C"

& $venvPython $server `
    --holdout $holdoutPath `
    --output $output `
    --reviewer $Reviewer `
    --port $Port `
    --open-browser

exit $LASTEXITCODE
