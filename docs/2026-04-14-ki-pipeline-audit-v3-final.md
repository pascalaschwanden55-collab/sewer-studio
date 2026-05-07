# SewerStudio V4.1 — KI-Pipeline Audit V3 Final

**Datum:** 14. April 2026, 20:00 Uhr (Zuerich)
**System:** Intel Core Ultra 9 285K, NVIDIA RTX 5090 (32 GB), 64 GB DDR5
**Auditor:** Claude Opus 4.6

---

## 1. Modell-Konfiguration (Final)

### 1.1 Aktive Modelle

| Modell | Quant | GPU VRAM | System RAM | ctx | Aufgabe |
|---|---|---|---|---|---|
| **qwen3-vl:8b-q8** | Q8_0 | 11.7 GB | — | 8192 | Primary Vision (jeder Frame) |
| **qwen3-vl:32b** | Q4_K_M | 4.2 GB | 18.9 GB | 4096 | Eskalation (hybrid num_gpu=10) |
| **nomic-embed-text** | F16 | 0.6 GB | — | 2048 | KB-Embedding |
| **YOLO26m-seg** | TensorRT FP16 | ~1.5 GB | — | — | Schadenserkennung |
| **DINO 1.5** | FP16 autocast | ~1.0 GB | — | — | Text-Grounding |
| **SAM 2** | — | ~1.0 GB | — | — | Pixel-Segmentierung |
| **Florence-2** | — | lazy (~2 GB) | — | — | Shadow-Lernen |

### 1.2 Modell-Upgrades (14.04.2026)

| Aenderung | Vorher | Nachher | Impact |
|---|---|---|---|
| Primary Quantisierung | Q4_K_M (8.1 GB) | **Q8_0 (11.7 GB)** | <0.2% Verlust statt 2-3% |
| Eskalationsmodell | qwen2.5vl:32b (Gen 2.5) | **qwen3-vl:32b (Gen 3)** | Neuer, bessere Vision |
| 32B GPU-Offload | num_gpu=0 (nur RAM) | **num_gpu=10 (hybrid)** | 9s statt 28s Latenz |
| Alte Modelle | 3 veraltete (33 GB Disk) | **Geloescht** | Disk frei |

### 1.3 Performance-Vergleich 32B Eskalation

| Metrik | num_gpu=0 (nur RAM) | num_gpu=10 (hybrid) |
|---|---|---|
| Latenz | 28s | **9s** |
| CPU-Last bei Eskalation | 100% | ~80-100% |
| GPU-Beteiligung | 0% | ~25% |
| VRAM 32B | 0 GB | 4.2 GB |
| RAM 32B | 22.8 GB | 18.9 GB |
| Qualitaet | identisch | **identisch** |

---

## 2. VRAM-Budget

| Komponente | GPU VRAM | System RAM |
|---|---|---|
| qwen3-vl:8b-q8 (Q8_0, 6 Slots) | 11.7 GB | — |
| qwen3-vl:32b (num_gpu=10) | 4.2 GB | 18.9 GB |
| nomic-embed-text | 0.6 GB | — |
| YOLO TensorRT | ~1.5 GB | — |
| DINO 1.5 (FP16 autocast) | ~1.0 GB | — |
| SAM 2 | ~1.0 GB | — |
| Windows/Driver | ~2.0 GB | — |
| **Total** | **~22 GB** | **~19 GB** |
| **Frei** | **~6.5 GB** | **~45 GB** |

---

## 3. Pipeline-Architektur

### 3.1 Eskalationslogik (3-stufig)

```
Frame → YOLO (5ms) → relevant?
  |
  +-- Nein (80%) → uebersprungen
  |
  +-- Ja → DINO (FP16) → SAM (min_score=0.50) → Qwen 8B Q8_0 (GPU, 2-3s)
        |
        +-- Green (95%) → KB
        |
        +-- Yellow/Red → Same-Model-Retry (erweiterter Prompt)
              |
              +-- Geloest → KB
              |
              +-- Immer noch Yellow/Red → 32B hybrid (10 Layers GPU, Rest CPU, ~9s)
```

### 3.2 Batch-Konfiguration

| Parameter | Wert |
|---|---|
| OLLAMA_NUM_PARALLEL | 6 |
| OLLAMA_MAX_LOADED_MODELS | 3 |
| OLLAMA_FLASH_ATTENTION | 1 |
| OLLAMA_NUM_CTX | 8192 |
| FrameStepSeconds (Batch) | 5.0 |
| PeriodicSweep | frameIndex % 5 |
| DinoFallbackEveryN | 5 |
| Parallelitaet (Haltungen) | 3 (P3) |

### 3.3 Sidecar (Python FastAPI, Port 8100)

| Modell | Optimierung | Status |
|---|---|---|
| YOLO26m-seg | TensorRT FP16 (.engine) | Permanent |
| DINO 1.5 | FP16 autocast | Permanent |
| SAM 2 | min_score=0.50 | Permanent |
| Florence-2 | Shadow-Lernen | Lazy |

### 3.4 Batch-Endpunkte (bereit, nicht aktiv)

| Endpunkt | Status |
|---|---|
| POST /detect/yolo/batch | Bereit |
| POST /detect/dino/batch | Bereit |
| POST /segment/sam/batch | Bereit |
| BatchPipelineService (C#) | Deaktiviert (Deadlock bei parallelen Qwen-Requests) |

---

## 4. KnowledgeBase

| Metrik | Wert |
|---|---|
| Total Samples | 8'592 |
| Total Embeddings | 8'592 (100% sync) |
| Pfad | C:\KI_BRAIN |
| Spiegelung | E:\Brain |
| Disk | 38 GB |

### QualityGate-Verteilung

| Level | Samples | Anteil |
|---|---|---|
| Red | 8'058 | 93.8% |
| Yellow | 338 | 3.9% |
| Green | 196 | 2.3% |

Hinweis: 96% Red sind KB-Samples ohne manuelle Verifizierung — nicht die Live-Eskalationsrate (<5%).

### Enrichment-Policy

| Policy | Wert |
|---|---|
| ApproveMatches | true |
| ApproveCorrections | **false** (KB-Vergiftung verhindert) |
| LearnFromMissed | true |

---

## 5. Durchsatz

### Gemessene Werte (14.04.2026)

| Metrik | Wert |
|---|---|
| Frames pro Sekunde | ~1 Frame/s (volle Pipeline) |
| Haltung (183 Frames) | ~136s (~2.3 Min) |
| Haltungen pro Stunde | ~20-25 |
| Geschaetzt pro 8h Nachtbatch | ~160-200 Haltungen |

### Vergleich zu Vortag

| Metrik | 13.04 | 14.04 | Faktor |
|---|---|---|---|
| Haltungen/8h | ~14 | ~160-200 | **~12x** |
| Frame-Intervall | 1.5s | 5.0s | 3x weniger Frames |
| 32B Latenz | 28s | 9s | 3x schneller |
| Primary Modell | Q4_K_M | Q8_0 | Bessere Qualitaet |

---

## 6. PDF-Format-Unterstuetzung

| Format | Haltungen | Status |
|---|---|---|
| Fretz/IBAK | ~1'600 | OK |
| KIT Bauinspekt | ~200 | OK |
| Abwasser Uri | ~350 | OK |
| IBAK direkt (NEU) | ~2'000 | OK |
| Verschluesselte Fonts | 9 | Nicht lesbar |

---

## 7. Geloeste Probleme (13-14. April)

| Problem | Fix |
|---|---|
| KB-Vergiftung (ApproveCorrections) | Default = false |
| SAM False Positives | min_score = 0.50 |
| FrameQualityFilter Thread-Safety | Interlocked.Increment |
| Frame-Rejection Logging | LogInformation + Zaehler |
| IBAK-PDF nicht erkannt | Format 4 im Parser |
| OLLAMA_NUM_CTX = 32768 | User-Env = 8192 + Modelfile |
| OLLAMA_MAX_LOADED_MODELS = 2 | Auf 3 erhoeft |
| keep_alive = "-1" | Auf "8760h" (Ollama 0.20.7) |
| Benchmark-Button grau | NotifyCanExecuteChanged |
| KB-Pfade veraltet | 15'191 Pfade korrigiert |
| Samples ohne Bild | 7'344 geloescht |
| 32B nur RAM (28s) | num_gpu=10 hybrid (9s) |
| Primary Q4_K_M | Upgrade auf Q8_0 |
| Eskalation qwen2.5vl | Upgrade auf qwen3-vl:32b |

### Verifizierte Bugs (alle gefixt)

| Bug | Status |
|---|---|
| N9: QualityGate MinSignals | GEFIXT (MinSignalsForGreen=2) |
| N5: QuickScan ohne Filter | GEFIXT (FrameQualityFilter aktiv) |
| N2: NormalizeClock Englisch | GEFIXT (top/bottom/left/right) |
| N15: Stille Frame-Verwerfung | GEFIXT (Logging aktiv) |
| T2: StufeRegex [1-4] | GEFIXT ([1-5]) |

---

## 8. Offene Punkte

| Problem | Prio | Beschreibung |
|---|---|---|
| BatchPipeline Deadlock | P1 | Parallele Qwen-Requests blockieren — deaktiviert |
| PipeImageWidthRatio 0.70 | P1 | Messungen +-15% |
| 32B Warmup Timeout | P2 | 5 Min reicht nicht fuer 23 GB — manuell nachladen |
| 96% Red in KB | P2 | Review-Queue abarbeiten |
| Reset() nie aufgerufen | P2 | Hash-Carry-Over |
| 9 verschluesselte PDFs | P3 | OCR noetig |

---

## 9. Sidecar-Optimierungen (Faktencheck)

| Feature | Status | Behauptung "nicht genutzt" |
|---|---|---|
| YOLO TensorRT | **Aktiv** (yolo26m.engine) | FALSCH |
| DINO FP16 autocast | **Aktiv** | FALSCH |
| torch.compile() | Nicht genutzt | Korrekt (~10-20% moeglich) |
| channels_last | Nicht genutzt | Korrekt (~5-10% moeglich) |
| FP8 Tensor Cores | Nicht genutzt | Korrekt (wartet auf Ollama) |
| CUDA Graphs | Nicht genutzt | Korrekt (komplex) |

Realistisches Restpotenzial: 15-25% durch torch.compile + channels_last.

---

## 10. Hardware-Auslastung

| Ressource | Belegt | Total | Auslastung |
|---|---|---|---|
| GPU VRAM | 25.6 GB | 32 GB | 79% |
| System RAM | 38.8 GB | 64 GB | 61% |
| GPU Temperatur | 57°C | — | Normal |
| CPU Temperatur | 58°C | — | Normal |
| Disk (Programm) | 59 GB | — | — |
| Disk (KI_BRAIN) | 38 GB | — | — |

---

## 11. Architektur-Diagramm

```
                    ┌─────────────────────────────────────────────┐
                    │              SewerStudio V4.1 Final          │
                    │         WPF / .NET 8+ / Windows 11          │
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
        │    11.7 GB     │    │  DINO (FP16 autocast)  │
        │                │    │  SAM  (persistent)     │
        │  32B (hybrid)  │    │  Florence-2 (lazy)     │
        │    GPU: 4.2 GB │    │                        │
        │    RAM: 18.9GB │    │                        │
        │                │    │                        │
        │  nomic (GPU)   │    │                        │
        │    0.6 GB      │    │                        │
        └────────────────┘    └────────────────────────┘

        ┌─────────────────────────────────────────────┐
        │           C:\KI_BRAIN (38 GB)               │
        │  KnowledgeBase.db    8'592 Samples          │
        │  → Spiegelung: E:\Brain                     │
        └─────────────────────────────────────────────┘
```

---

## 12. Empfehlungen

### Kurzfristig
1. 32B Warmup-Timeout erhoehen (5 Min → 10 Min)
2. Review-Queue abarbeiten (Green-Anteil erhoehen)
3. BatchPipeline Deadlock debuggen

### Mittelfristig
4. torch.compile() fuer DINO+SAM (+10-20%)
5. A/B-Test Q4 vs Q8 (Green-Rate vergleichen)
6. qwen3-vl:32b-thinking fuer Red-Cases

### Langfristig
7. FP8 via Ollama (wenn verfuegbar)
8. vLLM Evaluation fuer maximale Performance
9. Nemotron-Parse fuer KI-basiertes PDF-Parsing

---

*Audit V3 Final erstellt am 14.04.2026, 20:00 Uhr.*
*Pipeline-Durchsatz: ~12x schneller als Vortag.*
*Naechster Audit nach BatchPipeline-Fix und A/B-Test.*
