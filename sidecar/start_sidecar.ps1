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
    Write-Host "  Erstelle venv mit:" -ForegroundColor Yellow
    Write-Host "    python -m venv .venv" -ForegroundColor White
    Write-Host "    .venv\Scripts\activate" -ForegroundColor White
    Write-Host "    pip install -r requirements.txt" -ForegroundColor White
    exit 1
}

# Activate venv
$venvActivate = Join-Path $scriptDir ".venv\Scripts\Activate.ps1"
Write-Host "  Python venv aktiviert" -ForegroundColor Green
& $venvActivate

# Check GPU
Write-Host ""
$gpuInfo = & $venvPython -c "import torch; print(f'CUDA: {torch.cuda.is_available()}, Device: {torch.cuda.get_device_name(0) if torch.cuda.is_available() else \"CPU\"}')" 2>&1
Write-Host "  $gpuInfo" -ForegroundColor $(if ($gpuInfo -match "True") { "Green" } else { "Yellow" })

# Check models
Write-Host ""
$modelsDir = Join-Path $scriptDir "models"
$yoloOk = Test-Path (Join-Path $modelsDir "yolo26m\*.pt")
$dinoOk = Test-Path (Join-Path $modelsDir "grounding_dino_1.5\*.pth")
$samOk  = Test-Path (Join-Path $modelsDir "sam3\*.pth")

Write-Host "  Modelle:" -ForegroundColor White
Write-Host "    YOLO:  $(if ($yoloOk) { 'Custom Weights' } else { 'Fallback (yolo11m)' })" -ForegroundColor $(if ($yoloOk) { "Green" } else { "Yellow" })
Write-Host "    DINO:  $(if ($dinoOk) { 'OK' } else { 'FEHLT' })" -ForegroundColor $(if ($dinoOk) { "Green" } else { "Red" })
Write-Host "    SAM:   $(if ($samOk)  { 'OK' } else { 'FEHLT' })" -ForegroundColor $(if ($samOk)  { "Green" } else { "Red" })

# Set defaults
if (-not $env:SEWER_SIDECAR_HOST) { $env:SEWER_SIDECAR_HOST = "127.0.0.1" }
if (-not $env:SEWER_SIDECAR_PORT) { $env:SEWER_SIDECAR_PORT = "8100" }
$env:SEWER_SIDECAR_MODELS_DIR = $modelsDir
if (-not $env:SEWER_SIDECAR_YOLO_MODEL_NAME) { $env:SEWER_SIDECAR_YOLO_MODEL_NAME = "yolo26m.pt" }
if (-not $env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO) {
    $env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO = if ($yoloOk) { "1" } else { "0" }
}

Write-Host ""
Write-Host "  Starte auf http://$($env:SEWER_SIDECAR_HOST):$($env:SEWER_SIDECAR_PORT)" -ForegroundColor Green
Write-Host "  YOLO Modell: $($env:SEWER_SIDECAR_YOLO_MODEL_NAME)" -ForegroundColor White
Write-Host "  Require custom YOLO: $($env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO)" -ForegroundColor $(if ($env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO -eq "1") { "Green" } else { "Yellow" })
if ($env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO -eq "1" -and -not $yoloOk) {
    Write-Host "  FEHLER: RequireCustomYOLO=1, aber keine YOLO26-Gewichte gefunden." -ForegroundColor Red
    Write-Host "  Lege '$($env:SEWER_SIDECAR_YOLO_MODEL_NAME)' unter .\\models\\yolo26m\\ ab." -ForegroundColor Yellow
    exit 1
}
Write-Host "  SewerStudio erkennt den Sidecar automatisch." -ForegroundColor DarkGray
Write-Host "  Druecke Ctrl+C zum Beenden." -ForegroundColor DarkGray
Write-Host ""

python -m uvicorn sidecar.main:app --host $env:SEWER_SIDECAR_HOST --port $env:SEWER_SIDECAR_PORT --log-level info
