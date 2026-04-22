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
$env:OLLAMA_NUM_PARALLEL = "6"           # 6 parallele Inference-Slots fuer 8B (Flash Attention)
$env:OLLAMA_MAX_LOADED_MODELS = "3"      # 8B + 32B (hybrid) + nomic-embed-text
$env:OLLAMA_FLASH_ATTENTION = "1"        # Flash Attention fuer schnellere Inference

# ── App / Ollama Client ──────────────────────────────────────────────────
# V4.1: 8B×4 Slots permanent, kein Kaskaden-Modell, Yellow-Retry im gleichen 8B
$env:AUSWERTUNGPRO_AI_ENABLED = "1"
$env:AUSWERTUNGPRO_MULTIMODEL_ENABLED = "1"
$env:AUSWERTUNGPRO_PIPELINE_MODE = "multimodel"
$env:AUSWERTUNGPRO_AI_TIMEOUT_MIN = "60"
$env:AUSWERTUNGPRO_AI_VISION_MODEL = "qwen3-vl:8b"
$env:AUSWERTUNGPRO_AI_TEXT_MODEL = "qwen3-vl:8b"
$env:AUSWERTUNGPRO_AI_EMBED_MODEL = "nomic-embed-text"

# Kontext-Groesse: 8K fuer maximale Prompt-Qualitaet (Few-Shot + Vision-Tokens)
$env:SEWERSTUDIO_OLLAMA_NUM_CTX = "8192"

# Batch-Nachtbetrieb Parallelisierung (Max Quality)
$env:SEWERSTUDIO_GPU_CONCURRENCY = "6"                         # Parallele Ollama-GPU-Requests (= NUM_PARALLEL)
$env:SEWERSTUDIO_SELFTRAIN_CASE_PARALLELISM = "6"              # 6 Haltungen gleichzeitig
$env:SEWERSTUDIO_SELFTRAIN_PREEXTRACT_PARALLELISM = "20"       # 20 CPU-Kerne fuer PDF-Vorladen

# Recall-first Schwellenwerte fuer maximale Erkennungsabdeckung
$env:AUSWERTUNGPRO_YOLO_CONFIDENCE = "0.10"
$env:AUSWERTUNGPRO_DINO_BOX_THRESHOLD = "0.15"
$env:AUSWERTUNGPRO_DINO_TEXT_THRESHOLD = "0.15"
$env:AUSWERTUNGPRO_SIDECAR_TIMEOUT_SEC = "180"
# Eskalation: 32B permanent hybrid GPU/CPU (num_gpu=10, ~13s pro Frame, kein Swap)
$env:AUSWERTUNGPRO_AI_REFERENCE_MODEL = "qwen2.5vl:32b"

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
$env:SEWER_SIDECAR_VIDEO_WORKER_COUNT = "8"    # NVDEC + YOLO parallel (Producer-Consumer)
$env:SEWER_SIDECAR_VIDEO_QUEUE_MAXSIZE = "32"  # Groesserer Puffer fuer GPU-Vorlauf
$env:SEWER_SIDECAR_CPU_THREADS = "16"          # Torch CPU-Threads (16 von 24 Kernen)
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
Write-Host "  Batch-Training:" -ForegroundColor White
Write-Host "    GPU Concurrency:   $env:SEWERSTUDIO_GPU_CONCURRENCY"
Write-Host "    Case Parallelism:  $env:SEWERSTUDIO_SELFTRAIN_CASE_PARALLELISM"
Write-Host "    CPU PreExtract:    $env:SEWERSTUDIO_SELFTRAIN_PREEXTRACT_PARALLELISM"
Write-Host ""
Write-Host "  Sidecar:" -ForegroundColor White
Write-Host "    GPU Device:        $env:SEWER_SIDECAR_GPU_DEVICE"
Write-Host "    YOLO Device:       $env:SEWER_SIDECAR_YOLO_DEVICE"
Write-Host "    DINO Device:       $env:SEWER_SIDECAR_DINO_DEVICE"
Write-Host "    SAM Device:        $env:SEWER_SIDECAR_SAM_DEVICE"
Write-Host "    YOLO Model:        $env:SEWER_SIDECAR_YOLO_MODEL_NAME"
Write-Host "    Require Custom:    $env:SEWER_SIDECAR_REQUIRE_CUSTOM_YOLO"
Write-Host "    Video Workers:     $env:SEWER_SIDECAR_VIDEO_WORKER_COUNT"
Write-Host "    Video Queue:       $env:SEWER_SIDECAR_VIDEO_QUEUE_MAXSIZE"
Write-Host "    CPU Threads:       $env:SEWER_SIDECAR_CPU_THREADS"
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
Write-Host "  V4.1: 8B×6 Slots (8K ctx), Batch-Pipeline, YOLO+DINO+SAM+Florence-2 perm., Flash Attention." -ForegroundColor DarkGray
Write-Host "  Starte jetzt AuswertungPro aus derselben Session oder ueber deine IDE." -ForegroundColor DarkGray
Write-Host ""
