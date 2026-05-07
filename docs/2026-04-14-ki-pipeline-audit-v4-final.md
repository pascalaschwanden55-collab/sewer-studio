# SewerStudio V4.1 — KI-Pipeline Audit V4 Final

**Datum:** 14. April 2026, 21:00 Uhr
**Ort:** Altdorf, Kanton Uri, Schweiz
**Entwickler:** Pascal Aschwanden, Abwasser Uri
**System:** Intel Core Ultra 9 285K, NVIDIA RTX 5090 (32 GB), 64 GB DDR5
**Auditor:** Claude Opus 4.6

> SewerStudio ist ein Solo-Projekt von Pascal Aschwanden bei Abwasser Uri.
> Ehemaliger Kanalinspekteur — heute Auswertung der Aufnahmen, Sanierungsvorschlaege
> und Kostenzusammenstellungen. Kein Programmierer von Beruf — entwickelt mit
> KI-Unterstuetzung und Geduld. Kein kommerzielles Ziel.

---

## 1. Modell-Konfiguration

| Modell | Quant | GPU VRAM | System RAM | ctx | Aufgabe |
|---|---|---|---|---|---|
| **qwen3-vl:8b-q8** | Q8_0 | 11.7 GB | — | 8192 | Primary Vision (jeder Frame) |
| **qwen3-vl:32b** | Q4_K_M | 4.2 GB | 18.9 GB | 4096 | Eskalation (hybrid num_gpu=10) |
| **nomic-embed-text** | F16 | 0.6 GB | — | 2048 | KB-Embedding |
| **YOLO26m-seg** | TensorRT FP16 | ~1.5 GB | — | — | Schadenserkennung |
| **DINO 1.5** | FP16 autocast + torch.compile + channels_last | ~1.0 GB | — | — | Text-Grounding |
| **SAM 2** | torch.compile | ~1.0 GB | — | — | Pixel-Segmentierung |
| **Florence-2** | — | lazy | — | — | Shadow-Lernen |

---

## 2. VRAM-Budget

| Komponente | GPU VRAM | System RAM |
|---|---|---|
| qwen3-vl:8b-q8 (Q8_0, 6 Slots) | 11.7 GB | — |
| qwen3-vl:32b (num_gpu=10) | 4.2 GB | 18.9 GB |
| nomic-embed-text | 0.6 GB | — |
| YOLO TensorRT | ~1.5 GB | — |
| DINO 1.5 | ~1.0 GB | — |
| SAM 2 | ~1.0 GB | — |
| Windows/Driver | ~2.0 GB | — |
| **Total** | **~22 GB** | **~19 GB** |
| **Frei** | **~4 GB** | **~45 GB** |

---

## 3. Pipeline-Architektur

### 3.1 BatchPipeline (Nachtbatch + Benchmark)

```
Phase 1: Frame-Extraktion (ffmpeg, 5s Intervall)
  |
Phase 2: YOLO Batch (alle Frames auf einmal, TensorRT)
  |  Filter: ~80% irrelevant → uebersprungen
  |
Phase 2.5: DINO + SAM pro relevantem Frame (GPU)
  |  Grounding → Segmentierung → Quantifizierung
  |
Phase 3: Qwen 8B Q8_0 (3 parallel, mit DINO/SAM-Kontext)
  |
  +-- Green → KB
  +-- Yellow/Red → Same-Model-Retry → ggf. 32B Eskalation (9s hybrid)
```

### 3.2 Sequentielle Pipeline (Codiermodus, Live-Analyse)

```
Frame → YOLO → DINO → SAM → Qwen 8B (einzeln, mit Kontext)
```

### 3.3 Eskalationslogik

| Stufe | Modell | Latenz | Trigger |
|---|---|---|---|
| Normal | 8B Q8_0 (GPU) | ~1-3s | Jeder Frame |
| Retry | 8B Q8_0 (GPU) | ~1-3s | AllCodesNull, Severity>=4, PoorQuality |
| Eskalation | 32B (hybrid GPU+RAM) | ~9s | Retry unzureichend |

---

## 4. Batch-Konfiguration

| Parameter | Wert |
|---|---|
| OLLAMA_NUM_PARALLEL | 6 |
| OLLAMA_MAX_LOADED_MODELS | 3 |
| OLLAMA_FLASH_ATTENTION | 1 |
| OLLAMA_NUM_CTX | 8192 |
| FrameStepSeconds | 5.0 |
| Qwen Parallelitaet (BatchPipeline) | 3 |
| Haltungen parallel | 3 (P3) |
| PeriodicSweep | frameIndex % 5 |
| DinoFallbackEveryN | 5 |
| SAM min_score | 0.50 |

---

## 5. KnowledgeBase

| Metrik | Wert |
|---|---|
| Total Samples | 8'647 |
| Neue Samples/30min | **262** |
| Green | 247 (steigt) |
| Yellow | 342 |
| Red | 8'058 |
| Pfad | C:\KI_BRAIN |
| Spiegelung | E:\Brain |

### Enrichment-Policy

| Policy | Wert |
|---|---|
| ApproveMatches | true |
| ApproveCorrections | **false** |
| LearnFromMissed | true |

---

## 6. Gemessener Durchsatz

| Metrik | 13.04 (Start) | 14.04 (Final) | Faktor |
|---|---|---|---|
| Haltungen/8h | ~14 | **~1500+** | **~100x** |
| Neue Samples/h | ~2 | **~500** | **~250x** |
| Frames/Sekunde | ~0.03 | **~7** | **~230x** |
| 32B Latenz | 28s (RAM) | 9s (hybrid) | 3x |
| F1 Gesamt | — | 74% | — |
| False Positives | — | 0 | — |

---

## 7. Hardware-Auslastung (gemessen 21:00)

| Ressource | Belegt | Total | Auslastung |
|---|---|---|---|
| GPU VRAM | 28.2 GB | 32 GB | **87%** |
| GPU Compute | 67% | — | Aktiv |
| GPU Temperatur | 60°C | — | Normal |
| System RAM | ~40 GB | 64 GB | 63% |
| CPU | ~80% | — | Aktiv (32B + ffmpeg) |
| Disk (Programm) | 59 GB | — | — |
| Disk (KI_BRAIN) | 38 GB | — | — |

---

## 8. Sidecar-Optimierungen

| Feature | Status |
|---|---|
| YOLO TensorRT FP16 | Aktiv (yolo26m.engine) |
| DINO FP16 autocast | Aktiv |
| DINO torch.compile + channels_last | **Neu** |
| SAM torch.compile | **Neu** |
| SAM min_score=0.50 | Aktiv |
| SAM Box-Batching | Aktiv |
| YOLO/DINO/SAM Batch-Endpunkte | Aktiv |
| Auto-Kalibrierung (PipeImageWidthRatio) | **Neu** |
| Frame-Rejection Logging | Aktiv |

---

## 9. Aenderungen 13-14. April (komplett)

### Modell-Upgrades
- Primary: Q4_K_M → **Q8_0** (<0.2% statt 2-3% Verlust)
- Eskalation: qwen2.5vl:32b → **qwen3-vl:32b** (Gen 3)
- 32B Offload: num_gpu=0 → **num_gpu=10** (9s statt 28s)

### Pipeline-Optimierungen
- BatchPipeline: YOLO Batch → DINO+SAM → **Qwen 3-fach parallel**
- Frame-Intervall: 1.5s → **5.0s**
- Periodic Sweep: % 2 → **% 5**
- DINO Fallback: 2 → **5**
- Auto-Kalibrierung: PipeImageWidthRatio dynamisch statt 0.70 hardcoded
- torch.compile + channels_last fuer DINO+SAM
- 32B Warmup-Timeout: 5 Min → 10 Min

### Qualitaets-Fixes
- ApproveCorrections = false (KB-Vergiftung gestoppt)
- SAM min_score: 0.25 → 0.50
- FrameQualityFilter: Thread-Safe + Logging
- IBAK-PDF-Format: Neues Format 4
- keep_alive: "-1" → "8760h"
- OLLAMA_NUM_CTX: 8192 erzwungen
- Benchmark NotifyCanExecuteChanged
- KB-Pfade: 15'191 korrigiert
- Samples ohne Bild: 7'344 geloescht
- Alte Modelle: 33 GB Disk geloescht

### Verifizierte Bugs (alle gefixt)
- N9: QualityGate MinSignals=2
- N5: QuickScan mit FrameQualityFilter
- N2: NormalizeClock Englisch
- N15: Frame-Rejection Logging
- T2: StufeRegex [1-5]
- N7: SemaphoreSlim (1,1)

---

## 10. Offene Punkte

| Problem | Prio | Status |
|---|---|---|
| 96% Red in KB | P1 | Review-Queue abarbeiten |
| BatchPipeline DINO+SAM nicht gebatched | P2 | Sequentiell pro Frame (funktioniert, nicht optimal) |
| 32B Warmup manchmal zu langsam | P2 | Timeout auf 10 Min erhoeft, manuell nachladen als Fallback |
| 9 verschluesselte PDFs | P3 | OCR noetig |

---

## 11. Architektur-Diagramm

```
                    ┌─────────────────────────────────────────────┐
                    │              SewerStudio V4.1 Final          │
                    └──────────────┬──────────────────────────────┘
                                   │
              ┌────────────────────┴────────────────────┐
              │           ServiceProvider                │
              │  Warmup: 8B-Q8 + nomic + 32B(hybrid)    │
              └──────┬─────────────────┬────────────────┘
                     │                 │
        ┌────────────┴───┐    ┌────────┴──────────────┐
        │  Ollama :11434 │    │  Python Sidecar :8100  │
        │                │    │                        │
        │  8B-Q8 (GPU)   │    │  YOLO (TensorRT FP16) │
        │    11.7 GB     │    │  DINO (FP16+compile)   │
        │                │    │  SAM  (compile)        │
        │  32B (hybrid)  │    │  Florence-2 (lazy)     │
        │    GPU: 4.2 GB │    │                        │
        │    RAM: 18.9GB │    │  Auto-Kalibrierung     │
        │                │    │  Box-Batching          │
        │  nomic (GPU)   │    │  Batch-Endpunkte       │
        │    0.6 GB      │    │                        │
        └────────────────┘    └────────────────────────┘

        ┌──────────────────────────────────────────────┐
        │ BatchPipeline (Nachtbatch)                    │
        │ YOLO Batch → Filter → DINO+SAM → Qwen ×3    │
        │ ~7 Frames/s, ~1500 Haltungen/8h              │
        └──────────────────────────────────────────────┘

        ┌─────────────────────────────────────────────┐
        │           C:\KI_BRAIN (38 GB)               │
        │  KnowledgeBase.db    8'647 Samples          │
        │  262 neue Samples / 30 Min                  │
        │  → Spiegelung: E:\Brain                     │
        └─────────────────────────────────────────────┘
```

---

*Audit V4 Final erstellt am 14.04.2026, 21:00 Uhr.*
*Durchsatz: ~100x schneller als Start (13.04), ~250x mehr Samples/h.*
*Hardware-Auslastung: GPU 87% VRAM, 67% Compute, CPU 80%.*
