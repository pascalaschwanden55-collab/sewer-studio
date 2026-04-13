# V4.1 Design: Batch-Pipeline mit Qualitaetsfokus

## Ueberblick

Alle KI-Modelle laufen als Einzelinstanz mit Batch-Inference statt Modell-Duplikation.
Qwen 8B bedient 6 parallele Slots. 32B steht als Swap-Eskalation bereit.

## Hardware

- RTX 5090 (32 GB VRAM)
- OLLAMA_FLASH_ATTENTION=1 (entscheidend fuer VRAM-Effizienz)
- Intel Core Ultra 9 285K, 64 GB DDR5

## VRAM-Budget (gemessen mit Flash Attention)

| Komponente | VRAM | Status |
|---|---|---|
| Qwen3-VL:8b (6 Slots, 8192 ctx) | 8.1 GB | permanent |
| YOLO26m-seg TensorRT FP16 | 1.5 GB | permanent |
| Grounding DINO 1.5 | 3.0 GB | permanent |
| SAM 2 | 3.0 GB | permanent |
| Florence-2 Shadow | 2.0 GB | permanent (lazy load) |
| nomic-embed-text F16 | 0.6 GB | permanent |
| Windows/Driver | 2.0 GB | |
| **Total** | **~20 GB** | |
| **Reserve** | **~12 GB** | |

## Pipeline-Architektur

```
CPU: Frame-Extraktion (N Frames vorpuffern, 20 Threads)
  |
  v  Batch von 4-6 Frames
GPU: YOLO Batch-Inference (1 Forward Pass -> alle Boxen)
  |
  v  Alle Boxen gesammelt
GPU: DINO Batch-Grounding (alle Boxen -> alle Labels, 1 Pass)
  |
  v  Alle Labels + Boxen
GPU: SAM Batch-Segmentierung (alle Boxen -> alle Masken, 1 Pass)
  |
  v  6 komplette Frame-Pakete (Bild + Boxen + Labels + Masken + KB-Kontext)
GPU: Qwen 8B x6 parallel (6 VSA-Analysen gleichzeitig)
  |
  \-> Florence-2 Shadow (lernt asynchron von DINO-Detections)
```

## Ollama-Konfiguration

```
OLLAMA_NUM_PARALLEL=6
OLLAMA_MAX_LOADED_MODELS=2
OLLAMA_FLASH_ATTENTION=1
SEWERSTUDIO_OLLAMA_NUM_CTX=8192
SEWERSTUDIO_GPU_CONCURRENCY=6
SEWERSTUDIO_SELFTRAIN_CASE_PARALLELISM=6
```

## Modell-Rollen

| Modell | Rolle | Details |
|---|---|---|
| **YOLO26m-seg** | Schadenserkennung | TensorRT FP16, Batch-fuer-N-Frames |
| **DINO 1.5** | Text->Box Grounding | "Riss", "Wurzel", etc. -> Boxen |
| **SAM 2** | Box->Maske | Pixel-genaue Segmentierung |
| **Florence-2** | Lernender Shadow | Lernt von DINO, zukuenftig als Ersatz |
| **Qwen3-VL:8b** | VSA-Codierung | 6 parallele Slots, Haupt-Intelligenz |
| **Qwen2.5-VL:32b** | Eskalation | On-demand Swap bei Yellow/Red (~30-60s) |
| **nomic-embed-text** | KB-Embedding | Aehnlichkeitssuche fuer Few-Shot |

## Batching-Details

### YOLO (ultralytics)
- `model.predict(source=[img1, img2, ...], batch=N)`
- Batch-Size: 4-6 (optimal fuer RTX 5090 TensorRT)
- Qualitaet: identisch (gleiche Weights pro Frame)

### SAM (Priority #1 aus Roadmap)
- Heute: 1 Box -> 1 Forward Pass (sequentiell)
- Neu: N Boxen -> 1 Forward Pass -> N Masken
- Geschaetzter Gewinn: 3-5x schneller bei Multi-Box-Frames

### DINO
- Batch-Grounding: mehrere Prompts/Boxen in einem Pass
- Qualitaet: identisch

### Qwen 8B
- 6 Slots parallel via OLLAMA_NUM_PARALLEL=6
- Jeder Slot bekommt: Frame + YOLO-Boxen + DINO-Labels + SAM-Masken + KB-Kontext
- 8192 ctx = Platz fuer 5-6 Few-Shot-Beispiele + Vision-Tokens

## Eskalation (32B permanent hybrid GPU/CPU)

Trigger: allCodesNull || severity>=4 || poorQuality (QualityGate Yellow/Red)

**32B laeuft permanent neben 8B — kein Swap, kein Warten.**
10 von 64 Layers auf GPU, Rest auf CPU (num_gpu=10).
Alle 7 Modelle gleichzeitig geladen und einsatzbereit.

Ablauf:
1. 8B analysiert Frame (2-3s auf GPU)
2. Bei Yellow/Red: sofort 32B aufrufen (13s auf hybrid GPU/CPU)
3. Kein Entladen, kein Neuladen, kein Batch-Unterbruch

VRAM-Budget:
- 8B: 8.1 GB (GPU)
- 32B: 11.4 GB (GPU) + 19 GB (RAM)
- Sidecar: 3 GB (GPU)
- nomic: 0.6 GB (GPU)
- Total GPU: ~23 GB / 32 GB (7 GB Reserve)
- Total RAM: ~19 GB / 64 GB

## Qualitaets-Hebel

| Hebel | Beschreibung |
|---|---|
| 8192 ctx | Platz fuer 5-6 Few-Shot-Beispiele pro Prompt |
| KB-Retrieval | Top-3 aehnliche Samples als Kontext (nomic permanent) |
| Confidence-Ensemble | YOLO + DINO muessen uebereinstimmen |
| 32B Eskalation | Beste Qualitaet fuer schwierige Frames |
| Florence-2 Lernen | Kontinuierliche Verbesserung der Erkennung |
| Batch-Effizienz | Gleiche Qualitaet, 3-5x mehr Frames pro Zeiteinheit |

## Geaenderte Dateien

| Datei | Aenderung |
|---|---|
| `Start-KiMaximum5090.ps1` | NUM_PARALLEL=6, 8B primary, 32B Eskalation |
| `OllamaConfig.cs` | DefaultVisionModel=8B, DefaultReference=32B |
| `GpuModelSelector.cs` | Workstation: 8B×6, 32B Eskalation, 2-Tier |
| `ServiceProvider.cs` | Warmup: 8B + nomic, Kommentare V4.1 |
| `main.py` (Sidecar) | YOLO Pre-Warm beim Start (bereits implementiert) |

## Offene Punkte (Implementierung)

1. **SAM Box-Batching** — sam_wrapper.py anpassen (Roadmap Priority #1)
2. **YOLO Batch-Inference** — yolo_wrapper.py `detect_batch()` Methode
3. **DINO Batch** — dino_wrapper.py Batch-Endpunkt
4. **Pipeline-Orchestrator** — Producer-Consumer Architektur
5. **32B Swap-Logik** — EnhancedVisionAnalysisService Eskalation anpassen
