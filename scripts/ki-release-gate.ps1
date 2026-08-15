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

# Nur der Integration-Golden-Test (Trait Category=Integration).
# Ergebnis wird als TRX mitgeschrieben: "dotnet test" liefert Exit 0 auch dann,
# wenn der Filter NULL Tests trifft. Ohne diese Zaehlung waere das Gate nach einer
# Trait-Umbenennung still gruen, ohne je etwas geprueft zu haben
# (Audit 2026-08-14, T-M2). Die Zahlen kommen aus dem XML und nicht aus der
# uebersetzten Textausgabe - ein englischer Textvergleich findet auf einem
# deutschen Windows nie etwas.
$resultsDir = Join-Path $repoRoot '.tmp/ki-release-gate'
$trxPath = Join-Path $resultsDir 'gate.trx'
if (Test-Path $trxPath) { Remove-Item $trxPath -Force }

& dotnet test $testProject --filter 'Category=Integration' -v minimal `
    --logger 'trx;LogFileName=gate.trx' --results-directory $resultsDir
$code = $LASTEXITCODE

$ausgefuehrt = 0
$gefunden = 0
if (Test-Path $trxPath) {
    try {
        $trx = [xml](Get-Content $trxPath -Raw)
        $zaehler = $trx.TestRun.ResultSummary.Counters
        if ($null -ne $zaehler) {
            $ausgefuehrt = [int]$zaehler.executed
            $gefunden = [int]$zaehler.total
        }
    } catch {
        Write-Host "WARNUNG: Testbericht nicht lesbar: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "WARNUNG: Es wurde kein Testbericht geschrieben." -ForegroundColor Yellow
}

# Beide Faelle liefern executed=0 und sind beide "nichts geprueft":
#   gefunden=0  -> der Filter trifft keinen Test mehr
#   gefunden>0  -> Tests da, aber alle uebersprungen (z. B. Maschinen-Gate nicht erfuellt)
if ($ausgefuehrt -lt 1) {
    Write-Host "`nKI-Release-Gate ROT - es wurde KEIN Test ausgefuehrt." -ForegroundColor Red
    if ($gefunden -lt 1) {
        Write-Host "Der Filter 'Category=Integration' trifft keinen Test mehr." -ForegroundColor Yellow
        Write-Host "Trait im Testprojekt pruefen." -ForegroundColor Yellow
    } else {
        Write-Host "$gefunden Test(s) gefunden, aber alle uebersprungen." -ForegroundColor Yellow
        Write-Host "Sidecar auf localhost:8100 und GPU pruefen; der Golden-Lauf braucht beides." -ForegroundColor Yellow
    }
    Write-Host "Ein Gate, das nichts geprueft hat, darf nicht gruen melden." -ForegroundColor Yellow
    exit 2
}

if ($code -eq 0) {
    Write-Host "`nKI-Release-Gate BESTANDEN - Golden-Vertrag erfuellt ($ausgefuehrt Test(s)). Release darf raus." -ForegroundColor Green
} else {
    Write-Host "`nKI-Release-Gate ROT - Golden-Vertrag verletzt. Release STOPP." -ForegroundColor Red
}
exit $code
