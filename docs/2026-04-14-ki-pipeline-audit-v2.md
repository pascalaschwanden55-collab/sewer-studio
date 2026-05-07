# SewerStudio V4.1 — KI-Pipeline Audit V2

**Datum:** 14. April 2026, 17:00 Uhr (Zuerich)
**System:** Intel Core Ultra 9 285K, NVIDIA RTX 5090 (32 GB), 64 GB DDR5
**Branch:** feature/pdf-import-beobachtungen
**Auditor:** Claude Opus 4.6

---

## 1. Modell-Inventar

### 1.1 Aktive Modelle (V4.1 Final)

| Modell | Aufgabe | Quantisierung | GPU | RAM | ctx |
|---|---|---|---|---|---|
| **qwen3-vl:8b-q8** | Primary Vision (jeder Frame) | **Q8_0** (fast BF16-Qualitaet) | 11.7 GB | — | 8192 |
| **qwen3-vl:32b** | Eskalation (Yellow/Red) | Q4_K_M | **0 GB** | **22.8 GB (komplett RAM)** | 4096 |
| nomic-embed-text | KB-Embedding | F16 | 0.6 GB | — | 2048 |
| YOLO26m-seg | Schadenserkennung | TensorRT FP16 | ~1.5 GB | — | — |
| Grounding DINO 1.5 | Text-Grounding | — | ~1.0 GB | — | — |
| SAM 2 | Pixel-Segmentierung | — | ~1.0 GB | — | — |
| Florence-2 | Shadow-Lernen | — | ~2.0 GB | — | — |

### 1.2 Modell-Upgrades (14.04.2026)

| Vorher | Nachher | Aenderung |
|---|---|---|
| qwen3-vl:8b-8k (Q4_K_M, 8.1 GB) | **qwen3-vl:8b-q8 (Q8_0, 11.7 GB)** | Qualitaet: <0.2% Verlust statt 2-3% |
| qwen2.5vl:32b (Gen 2.5, Q4_K_M) | **qwen3-vl:32b (Gen 3, Q4_K_M)** | Eine Generation neuer |
| 32B mit num_gpu=10 (11.9 GB GPU) | **32B mit num_gpu=0 (0 GB GPU)** | Komplett im RAM, kein VRAM-Konflikt |

### 1.3 Installierte Ollama-Modelle (alle)

| Modell | Parameter | Quant | Disk |
|---|---|---|---|
| qwen3-vl:8b-q8 | 8.8B | Q8_0 | 9.8 GB |
| qwen3-vl:8b-instruct-q8_0 | 8.8B | Q8_0 | 9.8 GB |
| qwen3-vl:32b | 33.4B | Q4_K_M | 20.9 GB |
| qwen3-vl:8b-8k | 8.8B | Q4_K_M | 6.1 GB |
| qwen3-vl:8b | 8.8B | Q4_K_M | 6.1 GB |
| qwen3-vl:2b | 2.1B | Q4_K_M | 1.9 GB |
| qwen2.5vl:32b | 33.5B | Q4_K_M | 21.2 GB |
| nomic-embed-text | 137M | F16 | 0.3 GB |

Zum Loeschen nach Stabilisierung: qwen2.5vl:32b (21.2 GB), qwen3-vl:8b-8k (6.1 GB), qwen3-vl:8b (6.1 GB) = **33.4 GB frei**

---

## 2. VRAM-Budget

| Komponente | GPU VRAM | System RAM |
|---|---|---|
| qwen3-vl:8b-q8 (Q8_0, 6 Slots) | 11.7 GB | — |
| qwen3-vl:32b (num_gpu=0) | 0 GB | 22.8 GB |
| nomic-embed-text | 0.6 GB | — |
| YOLO TensorRT | ~1.5 GB | — |
| DINO 1.5 | ~1.0 GB | — |
| SAM 2 | ~1.0 GB | — |
| Florence-2 | ~2.0 GB | — |
| Windows/Driver | ~2.0 GB | — |
| **Total** | **~20 GB** | **~23 GB** |
| **Frei** | **~12 GB** | **~41 GB** |

---

## 3. Pipeline-Architektur

### 3.1 Modell-Hierarchie

```
Frame eingang
  |
  +-- YOLO Pre-Screening (5ms, TensorRT GPU)
  |     |
  |     +-- Irrelevant (80%) --> uebersprungen
  |     +-- Relevant (20%) --> weiter
  |
  +-- DINO Grounding (Text -> Boxen, GPU)
  |
  +-- SAM 2 Segmentierung (Box -> Maske, GPU, min_score=0.50)
  |
  +-- Qwen 8B Q8_0 (VSA-Codierung, 6 Slots parallel, GPU)
  |     |
  |     +-- Green (95%) --> KB direkt (wenn neues Sample)
  |     |
  |     +-- Yellow (4%) --> Same-Model-Retry (erweiterter Prompt)
  |     |     |
  |     |     +-- Geloest --> KB
  |     |     +-- Immer noch Yellow --> 32B Eskalation
  |     |
  |     +-- Red (1%) --> 32B Eskalation
  |
  +-- Qwen 32B Gen3 (Eskalation, ~28s, RAM)
  |
  +-- KB Embedding (nomic-embed-text, GPU)
  |
  +-- QualityGate (Green/Yellow/Red)
```

### 3.2 Eskalationslogik (EnhancedVisionAnalysisService)

```
1. Primary: qwen3-vl:8b-q8 (GPU, 2-3s)
2. Same-Model-Retry: erweiterter Prompt bei Yellow/Red
3. 32B Eskalation: qwen3-vl:32b (RAM, ~28s)
   - Trigger: AllCodesNull, HighSeverity (>=4), PoorQuality
   - num_gpu=0, komplett im RAM
   - Stoert Primary nicht (kein VRAM-Konflikt)
```

### 3.3 Sidecar (Python FastAPI, Port 8100)

| Endpunkt | Modell | Modus |
|---|---|---|
| POST /detect/yolo | YOLO26m-seg | TensorRT FP16, permanent |
| POST /detect/yolo/batch | YOLO26m-seg | Batch N Bilder |
| POST /detect/dino | DINO 1.5 | persistent, GPU |
| POST /detect/dino/batch | DINO 1.5 | Batch N Bilder |
| POST /segment/sam | SAM 2 | persistent, min_score=0.50 |
| POST /segment/sam/batch | SAM 2 | Box-Batching |
| GET /health | — | GPU-Status, VRAM |

### 3.4 Batch-Pipeline-Konfiguration

| Parameter | Wert | Beschreibung |
|---|---|---|
| OLLAMA_NUM_PARALLEL | 6 | Parallele Qwen-Slots |
| OLLAMA_MAX_LOADED_MODELS | 3 | 8B + 32B + nomic |
| OLLAMA_FLASH_ATTENTION | 1 | Flash Attention aktiv |
| OLLAMA_NUM_CTX | 8192 | Server-Default Kontext |
| FrameStepSeconds (Batch) | 5.0 | Alle 5s ein Frame |
| FrameStepSeconds (Benchmark) | 5.0 | Alle 5s ein Frame |
| DinoFallbackEveryN | 5 | Jeden 5. irrelevanten Frame |
| PeriodicSweep | frameIndex % 5 | Jeden 5. Frame direkt |

---

## 4. KnowledgeBase

### 4.1 Statistik

| Metrik | Wert |
|---|---|
| Total Samples | 8'387 |
| Verschiedene VSA-Codes | 153 |
| Pfad | C:\KI_BRAIN\KnowledgeBase.db |
| Spiegelung | C:\KI_BRAIN -> E:\Brain |
| Disk | 37 GB |

### 4.2 Top-5 Codes

| Code | Beschreibung | Samples |
|---|---|---|
| BCD | Rohranfang | 1'315 |
| BCE | Rohrende | 906 |
| BDA | Allgemeine Anmerkung | 435 |
| BDBA | Versatz vertikal | 405 |
| BCAAA | Anschluss Formstueck | 337 |

### 4.3 QualityGate-Verteilung

| Level | Samples | Anteil |
|---|---|---|
| Red | 8'058 | 96.1% |
| Yellow | 327 | 3.9% |
| Green | 2 | 0.02% |

### 4.4 Enrichment-Policy

| Policy | Wert | Beschreibung |
|---|---|---|
| ApproveMatches | true | Treffer automatisch in KB |
| **ApproveCorrections** | **false** | Korrekturen NUR nach Review |
| LearnFromMissed | true | Frames extrahieren |

---

## 5. PDF-Format-Unterstuetzung

| Format | Firma | Haltungen | Status |
|---|---|---|---|
| Format 1: Fretz/IBAK | Fretz Kanal-Service AG | ~1'600 | OK |
| Format 2: KIT | KIT Bauinspekt AG | ~200 | OK |
| Format 3: Abwasser Uri | Abwasser Uri | ~350 | OK |
| Format 4: IBAK direkt | Diverse | ~2'000 | NEU (V4.1) |
| Verschluesselte Fonts | 9 Haltungen | 9 | Nicht lesbar |

---

## 6. Aenderungen 13-14. April 2026

### 6.1 Modell-Upgrades

| Aenderung | Impact |
|---|---|
| Primary: Q4_K_M -> Q8_0 | <0.2% Qualitaetsverlust statt 2-3%, +3.6 GB VRAM |
| Eskalation: qwen2.5vl -> qwen3-vl | Eine Generation neuer, bessere Vision |
| 32B: GPU hybrid -> RAM only | Kein VRAM-Konflikt, GPU frei fuer Primary |

### 6.2 Pipeline-Optimierungen

| Aenderung | Impact |
|---|---|
| Frame-Intervall: 1.5s -> 5.0s | 3x weniger Frames, gleiche Qualitaet |
| Periodic Sweep: % 2 -> % 5 | 60% weniger Bypass-Frames |
| DINO Fallback: 2 -> 5 | 60% weniger Fallback-Analysen |
| YOLO Pre-Warm beim Start | Kein Cold-Start beim ersten Frame |
| SAM Box-Batching | Alle Boxen in einem Forward Pass |
| YOLO/DINO/SAM Batch-Endpunkte | Bereit fuer kuenftige Parallelisierung |
| Frame-Rejection Logging | Verworfene Frames werden gezaehlt |
| ApproveCorrections = false | KB-Vergiftung gestoppt |
| SAM min_score: 0.25 -> 0.50 | ~15% weniger False-Positive-Masken |
| IBAK-PDF-Format Parser | ~2'000 neue Haltungen erkennbar |
| keep_alive: "-1" -> "8760h" | Ollama 0.20.7 Kompatibilitaet |
| OLLAMA_NUM_CTX=8192 User-Env | Verhindert ctx=32768 Overallocation |
| OLLAMA_MAX_LOADED_MODELS=3 | Verhindert Modell-Eviction |
| EscalationQueueStore | JSONL-Queue fuer gebuendelte Eskalation |
| C# Batch-Client | DetectYoloBatchAsync, SegmentSamBatchAsync |
| Wissensdatenbank -> C:\KI_BRAIN | Programmsicherung ohne KB |

### 6.3 Neue Dateien

| Datei | Beschreibung |
|---|---|
| BatchPipelineService.cs | Batch-Pipeline (temporaer deaktiviert) |
| EscalationQueueStore.cs | JSONL-Queue fuer 32B-Eskalation |
| test_batch_endpoints.py | Smoke-Tests Sidecar Batch |

---

## 7. Bekannte Probleme

### 7.1 Offen

| Problem | Prio | Beschreibung |
|---|---|---|
| BatchPipeline Deadlock | P1 | Parallele Qwen-Requests haengen bei 6 Slots — temporaer deaktiviert |
| PipeImageWidthRatio = 0.70 | P1 | Messungen +-15% ungenau |
| Echtzeit-Log leer im Nachtbatch | P2 | Frame-Fortschritt wird nicht ins Log geschrieben |
| 9 verschluesselte PDFs | P2 | Font-Encoding kaputt, nur OCR hilft |
| Reset() nie aufgerufen | P2 | FrameQualityFilter Hash-Carry-Over |
| 96% Red in KB | P2 | Review-Queue abarbeiten fuer mehr Green |

### 7.2 Geloest (13-14. April)

| Problem | Fix |
|---|---|
| KB-Vergiftung (ApproveCorrections) | Default = false |
| SAM False Positives | min_score = 0.50 |
| OLLAMA_NUM_CTX = 32768 | User-Env-Var = 8192, Modelfile |
| OLLAMA_MAX_LOADED_MODELS = 2 | Auf 3 erhoeft |
| keep_alive = "-1" | Auf "8760h" |
| FrameQualityFilter Thread-Safety | Interlocked.Increment |
| IBAK-PDF nicht erkannt | Neues Format 4 im Parser |
| NotifyCanExecuteChanged fehlt | Benchmark-Button grau |
| KB-Pfade veraltet | 15'191 Pfade korrigiert |
| Samples ohne Bild | 7'344 geloescht |

---

## 8. Hardware-Auslastung

| Ressource | Belegt | Total | Auslastung |
|---|---|---|---|
| GPU VRAM | ~20 GB | 32 GB | 63% |
| System RAM | ~23 GB | 64 GB | 36% |
| GPU Temperatur | 59-65 C | — | Normal |
| Disk (Programm) | 59 GB | — | — |
| Disk (KI_BRAIN) | 37 GB | — | — |

---

## 9. Empfehlungen

### Kurzfristig
1. Alte Modelle loeschen (qwen2.5vl:32b, qwen3-vl:8b-8k, qwen3-vl:8b) → 33 GB Disk frei
2. Review-Queue abarbeiten → Green-Anteil in KB erhoehen
3. BatchPipeline Deadlock debuggen → 10x schnellerer Nachtbatch
4. qwen3-vl:32b-thinking pullen → Beste Qualitaet fuer Red-Cases

### Mittelfristig
5. A/B-Test Q4 vs Q8: Green-Rate vergleichen ueber 10'000 Frames
6. PipeImageWidthRatio kalibrieren (Ring-Scan verdrahten)
7. Echtzeit-Log im Nachtbatch mit Frame-Fortschritt

### Langfristig
8. FP8 statt Q8_0 wenn Ollama FP8 unterstuetzt (RTX 5090 native)
9. Nemotron-Parse fuer KI-basiertes PDF-Parsing
10. Knowledge Distillation 32B -> 8B

---

## 10. Architektur-Diagramm

```
                    ┌─────────────────────────────────────────────┐
                    │              SewerStudio V4.1                │
                    │         WPF / .NET 8+ / Windows 11          │
                    └──────────────┬──────────────────────────────┘
                                   │
              ┌────────────────────┴────────────────────┐
              │           ServiceProvider                │
              │  Warmup: 8B-Q8 + nomic + 32B(RAM)       │
              └──────┬─────────────────┬────────────────┘
                     │                 │
        ┌────────────┴───┐    ┌────────┴──────────────┐
        │  Ollama :11434 │    │  Python Sidecar :8100  │
        │                │    │                        │
        │  8B-Q8 (GPU)   │    │  YOLO (TensorRT)      │
        │  32B  (RAM)    │    │  DINO (persistent)     │
        │  nomic (GPU)   │    │  SAM  (persistent)     │
        └────────────────┘    │  Florence-2 (lazy)     │
                              └────────────────────────┘

        ┌─────────────────────────────────────────────┐
        │           C:\KI_BRAIN (37 GB)               │
        │  KnowledgeBase.db    8'387 Samples          │
        │  → Spiegelung: E:\Brain                     │
        └─────────────────────────────────────────────┘
```

---

*Audit V2 erstellt am 14.04.2026. Naechster Audit nach A/B-Test Q4 vs Q8.*
