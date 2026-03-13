$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sidecarRoot = Join-Path $repoRoot "sidecar"
$modelsDir = Join-Path $sidecarRoot "models"

Write-Host ""
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host "   AuswertungPro KI Maximum Preset (4070 Super)" -ForegroundColor Cyan
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host ""

# App / Ollama
$env:AUSWERTUNGPRO_AI_ENABLED = "1"
$env:AUSWERTUNGPRO_MULTIMODEL_ENABLED = "1"
$env:AUSWERTUNGPRO_PIPELINE_MODE = "multimodel"
$env:AUSWERTUNGPRO_AI_TIMEOUT_MIN = "60"
$env:AUSWERTUNGPRO_AI_VISION_MODEL = "qwen2.5vl:7b"
$env:AUSWERTUNGPRO_AI_TEXT_MODEL = "qwen2.5:7b"
$env:AUSWERTUNGPRO_AI_EMBED_MODEL = "nomic-embed-text"

# Recall-first thresholds for maximum inference coverage
$env:AUSWERTUNGPRO_YOLO_CONFIDENCE = "0.15"
$env:AUSWERTUNGPRO_DINO_BOX_THRESHOLD = "0.20"
$env:AUSWERTUNGPRO_DINO_TEXT_THRESHOLD = "0.15"
$env:AUSWERTUNGPRO_SIDECAR_TIMEOUT_SEC = "180"

# Ollama VRAM keep-alive (prevents model eviction between requests)
$env:AUSWERTUNGPRO_OLLAMA_KEEP_ALIVE = "24h"

# Sidecar / GPU
$env:SEWER_SIDECAR_HOST = "127.0.0.1"
$env:SEWER_SIDECAR_PORT = "8100"
$env:SEWER_SIDECAR_MODELS_DIR = $modelsDir
$env:SEWER_SIDECAR_GPU_DEVICE = "cuda:0"
$env:SEWER_SIDECAR_YOLO_DEVICE = "cpu"
$env:SEWER_SIDECAR_DINO_DEVICE = "cuda:0"
$env:SEWER_SIDECAR_SAM_DEVICE = "cuda:0"
$env:SEWER_SIDECAR_YOLO_MODEL_NAME = "yolo26m.pt"
$env:SEWER_SIDECAR_YOLO_CONFIDENCE = $env:AUSWERTUNGPRO_YOLO_CONFIDENCE
$env:SEWER_SIDECAR_DINO_BOX_THRESHOLD = $env:AUSWERTUNGPRO_DINO_BOX_THRESHOLD
$env:SEWER_SIDECAR_DINO_TEXT_THRESHOLD = $env:AUSWERTUNGPRO_DINO_TEXT_THRESHOLD

# Solange noch keine Custom-Gewichte vorhanden sind, nicht fail-fast erzwingen.
$customYolo = Get-ChildItem -Path (Join-Path $modelsDir "yolo26m") -Filter *.pt -ErrorAction SilentlyContinue
$env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO = if ($customYolo) { "1" } else { "0" }

Write-Host "  App/Ollama:" -ForegroundColor White
Write-Host "    AI Enabled:        $env:AUSWERTUNGPRO_AI_ENABLED"
Write-Host "    Pipeline Mode:     $env:AUSWERTUNGPRO_PIPELINE_MODE"
Write-Host "    Vision Model:      $env:AUSWERTUNGPRO_AI_VISION_MODEL"
Write-Host "    Text Model:        $env:AUSWERTUNGPRO_AI_TEXT_MODEL"
Write-Host "    Timeout:           $env:AUSWERTUNGPRO_AI_TIMEOUT_MIN min"
Write-Host "    Ollama Keep-Alive: $env:AUSWERTUNGPRO_OLLAMA_KEEP_ALIVE"
Write-Host "    YOLO Conf:         $env:AUSWERTUNGPRO_YOLO_CONFIDENCE"
Write-Host "    DINO Box/Text:     $env:AUSWERTUNGPRO_DINO_BOX_THRESHOLD / $env:AUSWERTUNGPRO_DINO_TEXT_THRESHOLD"
Write-Host ""
Write-Host "  Sidecar:" -ForegroundColor White
Write-Host "    GPU Device:        $env:SEWER_SIDECAR_GPU_DEVICE"
Write-Host "    YOLO Device:       $env:SEWER_SIDECAR_YOLO_DEVICE"
Write-Host "    DINO Device:       $env:SEWER_SIDECAR_DINO_DEVICE"
Write-Host "    SAM Device:        $env:SEWER_SIDECAR_SAM_DEVICE"
Write-Host "    YOLO Model:        $env:SEWER_SIDECAR_YOLO_MODEL_NAME"
Write-Host "    YOLO Conf:         $env:SEWER_SIDECAR_YOLO_CONFIDENCE"
Write-Host "    DINO Box/Text:     $env:SEWER_SIDECAR_DINO_BOX_THRESHOLD / $env:SEWER_SIDECAR_DINO_TEXT_THRESHOLD"
Write-Host "    Require Custom:    $env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO"
Write-Host "    Models Dir:        $env:SEWER_SIDECAR_MODELS_DIR"
Write-Host ""

$startSidecar = Join-Path $sidecarRoot "start_sidecar.ps1"
if (Test-Path $startSidecar) {
    Write-Host "  Starte Sidecar in neuem Fenster ..." -ForegroundColor Green
    Start-Process powershell -ArgumentList @(
        "-NoExit",
        "-ExecutionPolicy", "Bypass",
        "-File", $startSidecar
    ) | Out-Null
}
else {
    Write-Host "  WARNUNG: sidecar\\start_sidecar.ps1 nicht gefunden." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  Preset ist für diese PowerShell-Session aktiv." -ForegroundColor Green
Write-Host "  Trainingspfade bleiben unberührt; dieses Preset zieht nur die Laufzeit-KI auf Maximum." -ForegroundColor DarkGray
Write-Host "  Starte jetzt AuswertungPro aus derselben Session oder über deine IDE." -ForegroundColor DarkGray
Write-Host ""
