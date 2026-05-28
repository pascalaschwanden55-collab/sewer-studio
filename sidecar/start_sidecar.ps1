param(
    [switch]$DryRun
)

# Start Sewer-Studio Vision Sidecar
# Usage: .\start_sidecar.ps1
#
# Startet den Python FastAPI Sidecar fuer die Multi-Model KI Pipeline:
#   YOLO (Pre-Screening) -> Grounding DINO (Detection) -> SAM (Segmentation)
#
# SewerStudio erkennt den Sidecar automatisch und nutzt die Pipeline.

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host ""
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host "   Sewer-Studio Vision Sidecar" -ForegroundColor Cyan
Write-Host "   Multi-Model KI Pipeline (YOLO/DINO/SAM)" -ForegroundColor Cyan
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host ""

# Check venv
$venvPython = Join-Path $scriptDir ".venv\Scripts\python.exe"
if (-not (Test-Path $venvPython)) {
    Write-Host "  FEHLER: Python venv nicht gefunden!" -ForegroundColor Red
    Write-Host "  Bitte fuehre zuerst das Setup-Skript aus:" -ForegroundColor Yellow
    Write-Host "    .\setup.ps1" -ForegroundColor White
    exit 1
}

# Activate venv
$venvActivate = Join-Path $scriptDir ".venv\Scripts\Activate.ps1"
Write-Host "  Python venv aktiviert" -ForegroundColor Green
& $venvActivate

# Check GPU
Write-Host ""
$gpuProbe = "import torch; ok=torch.cuda.is_available(); name=torch.cuda.get_device_name(0) if ok else 'CPU'; print('CUDA: {}, Device: {}'.format(ok, name))"
$gpuInfo = & $venvPython -c $gpuProbe 2>&1
$cudaOk = $gpuInfo -match "CUDA: True"
Write-Host "  $gpuInfo" -ForegroundColor $(if ($cudaOk) { "Green" } else { "Yellow" })

# Check models
Write-Host ""
$modelsDir = Join-Path $scriptDir "models"
$yoloPtOk = Test-Path (Join-Path $modelsDir "yolo26m\*.pt")
$yoloEngineOk = Test-Path (Join-Path $modelsDir "yolo26m\yolo26m.engine")
$yoloOk = $yoloPtOk -or $yoloEngineOk
$dinoOk = Test-Path (Join-Path $modelsDir "grounding_dino_1.5\*.pth")
$samOk  = Test-Path (Join-Path $modelsDir "sam3\*.pth")

Write-Host "  Modelle:" -ForegroundColor White
$yoloStatus = if ($yoloEngineOk) { "TensorRT Engine" } elseif ($yoloPtOk) { "Custom Weights" } else { "Fallback (yolo11m)" }
Write-Host "    YOLO:  $yoloStatus" -ForegroundColor $(if ($yoloOk) { "Green" } else { "Yellow" })
Write-Host "    DINO:  $(if ($dinoOk) { 'OK' } else { 'FEHLT' })" -ForegroundColor $(if ($dinoOk) { "Green" } else { "Red" })
Write-Host "    SAM:   $(if ($samOk)  { 'OK' } else { 'FEHLT' })" -ForegroundColor $(if ($samOk)  { "Green" } else { "Red" })

# Set defaults
if (-not $env:SEWER_SIDECAR_HOST) { $env:SEWER_SIDECAR_HOST = "127.0.0.1" }
if (-not $env:SEWER_SIDECAR_PORT) { $env:SEWER_SIDECAR_PORT = "8100" }
$env:SEWER_SIDECAR_MODELS_DIR = $modelsDir
if (-not $env:SEWER_SIDECAR_YOLO_MODEL_NAME) {
    $env:SEWER_SIDECAR_YOLO_MODEL_NAME = if ($yoloEngineOk -and $cudaOk) { "yolo26m.engine" } else { "yolo26m.pt" }
}
if (-not $env:SEWER_SIDECAR_YOLO_CLS_MODEL_PATH) {
    $candidateCls = Get-ChildItem -Path (Join-Path $modelsDir "candidates\classification") -Recurse -Filter "best.pt" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($candidateCls) {
        Write-Host "  YOLO-cls Kandidat gefunden, aber nicht automatisch aktiviert:" -ForegroundColor Yellow
        Write-Host "    $($candidateCls.FullName)" -ForegroundColor DarkGray
        Write-Host "  Aktivieren mit: `$env:SEWER_SIDECAR_YOLO_CLS_MODEL_PATH = `"$($candidateCls.FullName)`"" -ForegroundColor DarkGray
    }
}
if (-not $env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO) {
    $env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO = if ($yoloOk) { "1" } else { "0" }
}

Write-Host ""
Write-Host "  Starte auf http://$($env:SEWER_SIDECAR_HOST):$($env:SEWER_SIDECAR_PORT)" -ForegroundColor Green
Write-Host "  YOLO Modell: $($env:SEWER_SIDECAR_YOLO_MODEL_NAME)" -ForegroundColor White
Write-Host "  YOLO-cls Modell: $(if ($env:SEWER_SIDECAR_YOLO_CLS_MODEL_PATH) { $env:SEWER_SIDECAR_YOLO_CLS_MODEL_PATH } else { 'auto/fallback' })" -ForegroundColor White
Write-Host "  Require custom YOLO: $($env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO)" -ForegroundColor $(if ($env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO -eq "1") { "Green" } else { "Yellow" })
if ($env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO -eq "1" -and -not $yoloOk) {
    Write-Host "  FEHLER: RequireCustomYOLO=1, aber keine YOLO26-Gewichte gefunden." -ForegroundColor Red
    Write-Host "  Lege '$($env:SEWER_SIDECAR_YOLO_MODEL_NAME)' unter .\\models\\yolo26m\\ ab." -ForegroundColor Yellow
    exit 1
}
Write-Host "  SewerStudio erkennt den Sidecar automatisch." -ForegroundColor DarkGray
Write-Host "  Druecke Ctrl+C zum Beenden." -ForegroundColor DarkGray
Write-Host ""

if ($DryRun) {
    Write-Host "  Dry-run: Sidecar wird nicht gestartet." -ForegroundColor Yellow
    exit 0
}

python -m uvicorn sidecar.main:app --host $env:SEWER_SIDECAR_HOST --port $env:SEWER_SIDECAR_PORT --log-level info
