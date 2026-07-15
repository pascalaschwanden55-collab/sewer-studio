# KI-Release-Gate: erzwingt den Golden-Lauf der KI-Pipeline gegen ein echtes
# Referenzvideo, bevor eine Release freigegeben wird.
#
# Fuehrt den maschinengebundenen Test SidecarRealVideoIntegrationTests aus
# (YOLO -> DINO -> SAM -> Quantifizierung gegen den Golden-Vertrag). Ohne dieses
# Gate faellt eine schleichende Erkennungs-Verschlechterung erst im Feld auf.
#
# Voraussetzung: GPU + laufender Sidecar (localhost:8100) auf dieser Maschine.
#
# Aufruf:
#   $env:SEWERSTUDIO_E2E_VIDEO = 'D:\Videoprojekte\golden\referenz.mpg'
#   ./scripts/ki-release-gate.ps1                 # nimmt Meter 0
#   ./scripts/ki-release-gate.ps1 -VideoAt 12.5   # oder eine bestimmte Stelle

param(
    [string]$Video = $env:SEWERSTUDIO_E2E_VIDEO,
    [string]$VideoAt = $env:SEWERSTUDIO_E2E_VIDEO_AT
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Video)) {
    Write-Host "ABBRUCH: Kein Referenzvideo gesetzt." -ForegroundColor Red
    Write-Host "Setze `$env:SEWERSTUDIO_E2E_VIDEO auf den Pfad des Golden-Referenzvideos" -ForegroundColor Yellow
    Write-Host "oder uebergib -Video <pfad>." -ForegroundColor Yellow
    exit 2
}
if (-not (Test-Path $Video)) {
    Write-Host "ABBRUCH: Referenzvideo nicht gefunden: $Video" -ForegroundColor Red
    exit 2
}

Write-Host "KI-Release-Gate: Golden-Lauf gegen $Video" -ForegroundColor Cyan
if ($VideoAt) { Write-Host "  an Position: $VideoAt" -ForegroundColor Cyan }

# Maschinengebundenen Test freischalten + Referenzvideo durchreichen
$env:SEWERSTUDIO_RUN_MACHINE_INTEGRATION = '1'
$env:SEWERSTUDIO_E2E_VIDEO = $Video
if ($VideoAt) { $env:SEWERSTUDIO_E2E_VIDEO_AT = $VideoAt }

$testProject = Join-Path $repoRoot 'tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj'

# Nur der Integration-Golden-Test (Trait Category=Integration)
& dotnet test $testProject --filter 'Category=Integration' -v minimal
$code = $LASTEXITCODE

if ($code -eq 0) {
    Write-Host "`nKI-Release-Gate BESTANDEN — Golden-Vertrag erfuellt. Release darf raus." -ForegroundColor Green
} else {
    Write-Host "`nKI-Release-Gate ROT — Golden-Vertrag verletzt. Release STOPP." -ForegroundColor Red
}
exit $code
