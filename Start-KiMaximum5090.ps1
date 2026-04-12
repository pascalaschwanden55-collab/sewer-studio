$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sidecarRoot = Join-Path $repoRoot "sidecar"
$modelsDir = Join-Path $sidecarRoot "models"

Write-Host ""
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host "   AuswertungPro KI Maximum Preset (RTX 5090)"   -ForegroundColor Cyan
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host ""

# ── Ollama Server-Konfiguration ──────────────────────────────────────────
# Diese Variablen muessen gesetzt sein BEVOR Ollama startet.
# Bei bereits laufendem Ollama: Dienst neu starten nach Aenderung.
$env:OLLAMA_NUM_PARALLEL = "6"           # 6 parallele Inference-Slots (passend zu GpuConcurrency=6)
$env:OLLAMA_MAX_LOADED_MODELS = "2"      # Qwen3-VL-8B + nomic-embed-text gleichzeitig
$env:OLLAMA_FLASH_ATTENTION = "1"        # Flash Attention fuer schnellere Inference

# ── App / Ollama Client ──────────────────────────────────────────────────
$env:AUSWERTUNGPRO_AI_ENABLED = "1"
$env:AUSWERTUNGPRO_MULTIMODEL_ENABLED = "1"
$env:AUSWERTUNGPRO_PIPELINE_MODE = "multimodel"
$env:AUSWERTUNGPRO_AI_TIMEOUT_MIN = "60"
$env:AUSWERTUNGPRO_AI_VISION_MODEL = "qwen3-vl:8b"
$env:AUSWERTUNGPRO_AI_TEXT_MODEL = "qwen2.5:7b"
$env:AUSWERTUNGPRO_AI_EMBED_MODEL = "nomic-embed-text"

# Kontext-Groesse: 8K statt 32K = weniger VRAM pro Slot, mehr parallele Slots
$env:SEWERSTUDIO_OLLAMA_NUM_CTX = "8192"

# Recall-first Schwellenwerte fuer maximale Erkennungsabdeckung
$env:AUSWERTUNGPRO_YOLO_CONFIDENCE = "0.15"
$env:AUSWERTUNGPRO_DINO_BOX_THRESHOLD = "0.20"
$env:AUSWERTUNGPRO_DINO_TEXT_THRESHOLD = "0.15"
$env:AUSWERTUNGPRO_SIDECAR_TIMEOUT_SEC = "180"

# Ollama VRAM Keep-Alive (verhindert Modell-Eviction zwischen Requests)
$env:AUSWERTUNGPRO_OLLAMA_KEEP_ALIVE = "24h"

# ── Sidecar / GPU ────────────────────────────────────────────────────────
$env:SEWER_SIDECAR_HOST = "127.0.0.1"
$env:SEWER_SIDECAR_PORT = "8100"
$env:SEWER_SIDECAR_MODELS_DIR = $modelsDir
$env:SEWER_SIDECAR_GPU_DEVICE = "cuda:0"
$env:SEWER_SIDECAR_YOLO_DEVICE = "cuda:0"     # 5090: YOLO auf GPU (genug VRAM)
$env:SEWER_SIDECAR_DINO_DEVICE = "cuda:0"
$env:SEWER_SIDECAR_SAM_DEVICE = "cuda:0"
$env:SEWER_SIDECAR_YOLO_MODEL_NAME = "yolo26m.pt"
$env:SEWER_SIDECAR_YOLO_CONFIDENCE = $env:AUSWERTUNGPRO_YOLO_CONFIDENCE
$env:SEWER_SIDECAR_DINO_BOX_THRESHOLD = $env:AUSWERTUNGPRO_DINO_BOX_THRESHOLD
$env:SEWER_SIDECAR_DINO_TEXT_THRESHOLD = $env:AUSWERTUNGPRO_DINO_TEXT_THRESHOLD

# Custom-YOLO-Gewichte pruefen
$customYolo = Get-ChildItem -Path (Join-Path $modelsDir "yolo26m") -Filter *.pt -ErrorAction SilentlyContinue
$env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO = if ($customYolo) { "1" } else { "0" }

# ── Status-Ausgabe ───────────────────────────────────────────────────────
Write-Host "  Ollama Server:" -ForegroundColor White
Write-Host "    NUM_PARALLEL:      $env:OLLAMA_NUM_PARALLEL"
Write-Host "    MAX_LOADED_MODELS: $env:OLLAMA_MAX_LOADED_MODELS"
Write-Host "    FLASH_ATTENTION:   $env:OLLAMA_FLASH_ATTENTION"
Write-Host "    Context Size:      $env:SEWERSTUDIO_OLLAMA_NUM_CTX"
Write-Host ""
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
    Write-Host "  WARNUNG: sidecar\start_sidecar.ps1 nicht gefunden." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  Preset ist fuer diese PowerShell-Session aktiv." -ForegroundColor Green
Write-Host "  RTX 5090: 6 parallele Ollama-Slots, YOLO auf GPU, Flash Attention." -ForegroundColor DarkGray
Write-Host "  Starte jetzt AuswertungPro aus derselben Session oder ueber deine IDE." -ForegroundColor DarkGray
Write-Host ""
