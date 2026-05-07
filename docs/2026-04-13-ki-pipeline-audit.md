# SewerStudio V4.1 — KI-Pipeline Audit

**Datum:** 13. April 2026, 21:30 Uhr (Zuerich)
**System:** Intel Core Ultra 9 285K, NVIDIA RTX 5090 (32 GB), 64 GB DDR5
**Branch:** feature/pdf-import-beobachtungen
**Auditor:** Claude Opus 4.6

---

## 1. Systemuebersicht

### 1.1 Hardware-Auslastung

| Ressource | Belegt | Total | Auslastung |
|---|---|---|---|
| GPU VRAM | 30.5 GB | 32.0 GB | 95% |
| System RAM | ~21 GB (32B Modell) | 64 GB | 33% |
| Disk (Programm) | 59 GB | — | — |
| Disk (KI_BRAIN) | 37 GB | — | — |
| GPU Temperatur | 64 C | — | Normal |

### 1.2 Geladene Modelle (7 Modelle gleichzeitig)

| Modell | Aufgabe | GPU | RAM | Kontext |
|---|---|---|---|---|
| qwen3-vl:8b (Q4_K_M) | VSA-Codierung (Primary) | 11.9 GB | — | 32768* |
| qwen2.5vl:32b (Q4_K_M) | Eskalation (hybrid num_gpu=10) | 11.4 GB | 20.8 GB | 4096 |
| nomic-embed-text (F16) | KB-Embedding | 0.6 GB | — | 2048 |
| YOLO26m-seg | Schadenserkennung | ~1.5 GB | — | TensorRT FP16 |
| Grounding DINO 1.5 | Text-Grounding | ~1.0 GB | — | Persistent |
| SAM 2 | Pixel-Segmentierung | ~1.0 GB | — | Persistent |
| Florence-2 | Shadow-Lernen | ~2.0 GB | — | Lazy |

*Hinweis: 8B laeuft aktuell mit ctx=32768 statt Soll 8192 — wird beim naechsten Neustart korrigiert.

### 1.3 Sidecar-Status

| Endpunkt | Status | Details |
|---|---|---|
| GET /health | OK | Alle Modelle resident |
| POST /detect/yolo | OK | TensorRT Engine aktiv |
| POST /detect/yolo/batch | NEU | Batch-Inference (V4.1) |
| POST /detect/dino | OK | DINO persistent |
| POST /detect/dino/batch | NEU | Batch-Endpunkt (V4.1) |
| POST /segment/sam | OK | SAM 2, min_score=0.50 |
| POST /segment/sam/batch | NEU | Box-Batching (V4.1) |

---

## 2. KnowledgeBase

### 2.1 Uebersicht

| Metrik | Wert |
|---|---|
| **Total Samples** | 15'703 |
| **Verschiedene VSA-Codes** | ~40+ |
| **Pfad** | C:\KI_BRAIN\KnowledgeBase.db |
| **Spiegelung** | C:\KI_BRAIN -> E:\Brain |

### 2.2 Top-15 VSA-Codes

| Code | Beschreibung | Samples |
|---|---|---|
| BCD | Rohranfang | 2'278 |
| BCE | Rohrende | 1'672 |
| BDA | Allgemeine Anmerkung | 725 |
| BDBA | Versatz vertikal | 700 |
| BCAAA | Anschluss Formstück | 548 |
| BCAEA | Anschluss eingespitzt | 482 |
| BAJC | Riss komplex | 431 |
| BAAA | Verformung vertikal | 428 |
| BDDC | Abbruch | 421 |
| BAHC | Versatz komplex | 416 |
| BCCBY | Bogen rechts | 381 |
| BCCAY | Bogen links | 365 |
| BAJB | Riss quer | 345 |
| BAFCE | Zuschlagstoffe sichtbar | 336 |
| BCBZ | Reparatur sonstige | 255 |

### 2.3 Quelltypen

| Quelle | Samples | Anteil |
|---|---|---|
| BatchImport (Self-Training) | 15'269 | 97.2% |
| PdfPhoto (Protokoll-Bilder) | 325 | 2.1% |
| VideoTimestamp (Frames) | 80 | 0.5% |
| FeedbackReview (Benutzer) | 20 | 0.1% |
| TeacherAnnotation (Lehrer) | 9 | 0.1% |

### 2.4 QualityGate-Verteilung

| Level | Samples | Anteil | Bewertung |
|---|---|---|---|
| Red (unsicher) | 15'107 | 96.2% | Hoch — viele unsichere Samples |
| Yellow (pruefen) | 585 | 3.7% | OK |
| Green (sicher) | 11 | 0.1% | Zu wenig Green-Samples |

**Bewertung:** 96% Red ist besorgniserregend. Die meisten Samples stammen aus dem Batch-Import ohne menschliche Verifizierung. Empfehlung: Review-Queue priorisiert abarbeiten um Green-Anteil zu erhoehen.

---

## 3. V4.1 Architektur

### 3.1 Pipeline-Flow

```
CPU: Frame-Extraktion (ffmpeg, 20 Threads)
  |
GPU: YOLO Batch (1 Forward Pass, TensorRT FP16)
  |
GPU: DINO Grounding (Text -> Boxen)
  |
GPU: SAM 2 Segmentierung (Box-Batching, min_score=0.50)
  |
GPU: Qwen 8B x6 parallel (VSA-Codierung, 8192 ctx)
  |   \-> Florence-2 Shadow (lernt von DINO)
  |
CPU+GPU: Qwen 32B hybrid (Eskalation bei Yellow/Red, num_gpu=10, ~13s)
  |
KB: nomic-embed-text (Few-Shot Retrieval)
  |
QualityGate: Green / Yellow / Red
```

### 3.2 Eskalationslogik

```
8B analysiert Frame (2-3s)
  |
  +-- Green -> KB direkt
  |
  +-- Yellow/Red -> Same-Model-Retry (erweiterter Prompt)
       |
       +-- Geloest -> KB
       |
       +-- Immer noch Yellow/Red -> 32B hybrid (13s, CPU+GPU)
            |
            +-- Ergebnis -> KB
```

### 3.3 VRAM-Budget

| Komponente | GPU VRAM | System RAM |
|---|---|---|
| Qwen 8B (6 Slots, Flash Attn) | 8.1 GB* | — |
| Qwen 32B (num_gpu=10) | 11.4 GB | 20.8 GB |
| nomic-embed-text | 0.6 GB | — |
| YOLO TensorRT | 1.5 GB | — |
| DINO 1.5 | 1.0 GB | — |
| SAM 2 | 1.0 GB | — |
| Florence-2 | 2.0 GB | — |
| **Total** | **~26 GB** | **~21 GB** |
| **Reserve** | **~6 GB** | **~43 GB** |

*Aktuell 11.9 GB wegen ctx=32768 — wird beim Neustart auf 8192 korrigiert (-> 8.1 GB).

---

## 4. Heutige Aenderungen (13.04.2026)

### 4.1 Konfiguration

| Aenderung | Vorher | Nachher |
|---|---|---|
| DefaultVisionModel | qwen3-vl:2b | qwen3-vl:8b |
| DefaultReferenceModel | qwen3-vl:8b | qwen2.5vl:32b |
| OLLAMA_NUM_PARALLEL | 3 | 6 |
| OLLAMA_MAX_LOADED_MODELS | 2 | 3 |
| 32B Eskalation | VRAM-Swap (30-60s) | Permanent hybrid num_gpu=10 (13s) |
| KnowledgeBase Pfad | Projektordner\Knowledge | C:\KI_BRAIN |
| Splash-Screen Version | v3.1 | v4.1 |

### 4.2 Neue Sidecar-Endpunkte

| Endpunkt | Beschreibung |
|---|---|
| POST /detect/yolo/batch | Batch-YOLO (N Bilder, 1 Forward Pass) |
| POST /detect/dino/batch | Batch-DINO (N Bilder) |
| POST /segment/sam/batch | Batch-SAM (N Bilder, Box-Batching pro Bild) |

### 4.3 Pipeline-Qualitaets-Fixes

| Fix | Impact |
|---|---|
| ApproveCorrections = false | Stoppt KB-Vergiftung durch fehlerhafte PDFs |
| SAM min_score 0.25 -> 0.50 | ~15% weniger False-Positive-Masken |
| Frame-Rejection Logging | Verworfene Frames werden gezaehlt und geloggt |
| FrameQualityFilter Thread-Safety | Interlocked.Increment statt ++ |
| IBAK-PDF-Format Parser | ~2000 Haltungen neu erkennbar |

### 4.4 Neue Dateien

| Datei | Beschreibung |
|---|---|
| EscalationQueueStore.cs | JSONL-Queue fuer 32B-Eskalation (ueberlebt Abbruch) |
| VisionPipelineDtos (Batch) | C# DTOs fuer Batch-Endpunkte |
| test_batch_endpoints.py | Smoke-Tests fuer alle Batch-Endpunkte |

---

## 5. Bekannte Probleme

### 5.1 Kritisch (P0) — Gefixt

| Problem | Status | Fix |
|---|---|---|
| KB-Vergiftung durch Auto-Approve | GEFIXT | ApproveCorrections = false |
| SAM False Positives (min_score 0.25) | GEFIXT | min_score = 0.50 |
| OLLAMA_MAX_LOADED_MODELS = 2 | GEFIXT | Auf 3 erhoeft |

### 5.2 Warnung (P1) — Offen

| Problem | Beschreibung | Aufwand |
|---|---|---|
| 8B ctx=32768 statt 8192 | Aktueller Lauf hat falschen Kontext | Fix beim Neustart |
| PipeImageWidthRatio = 0.70 | Messungen +-15% ungenau | 1 Tag (Ring-Scan verdrahten) |
| Meter-Toleranz pauschal | 0.5m fuer alle Codes statt differenziert | 4h |
| 9 verschluesselte PDFs | Font-Encoding kaputt, pdftotext versagt | OCR noetig |
| Reset() nie aufgerufen | Hash-Carry-Over zwischen Videos | 30min |

### 5.3 Bestaetigt OK

| Komponente | Befund |
|---|---|
| QualityGate Signal-Fusion | MinSignals=2, korrekt |
| Dedup / Temporal Voting | Label-Drift behandelt |
| Clock-Normalisierung | Deutsch + Englisch |
| Polly Retry + Circuit Breaker | 3 Retries, korrekt |
| YOLO Darkness-Threshold | Bereits bei 5 (nicht 15) |
| Race Conditions | Keine gefunden |

---

## 6. PDF-Format-Unterstuetzung

| Format | Firma | Haltungen | Status |
|---|---|---|---|
| Format 1: Fretz/IBAK | Fretz Kanal-Service AG | ~1600 | OK |
| Format 2: KIT | KIT Bauinspekt AG | ~200 | OK |
| Format 3: Abwasser Uri | Abwasser Uri | ~350 | OK |
| Format 4: IBAK direkt | Diverse | ~2000 | NEU (V4.1) |
| Verschluesselte Fonts | 9 Haltungen | 9 | Nicht lesbar |

---

## 7. Nachtbatch-Status

| Metrik | Wert |
|---|---|
| Haltungen gesamt | 2096 |
| Davon verarbeitbar | ~2087 (99.6%) |
| Nicht lesbar (Font) | 9 (0.4%) |
| KB vor Batch | 15'703 Samples |
| Modelle geladen | 7/7 |
| GPU Temperatur | 64 C (stabil) |
| Geschaetzte Laufzeit | ~6-10 Stunden |

---

## 8. Empfehlungen

### Kurzfristig (naechste Session)
1. App neu starten → 8B mit ctx=8192 statt 32768
2. Review-Queue abarbeiten → Green-Anteil in KB erhoehen
3. PipeImageWidthRatio kalibrieren → Messgenauigkeit verbessern

### Mittelfristig (1-2 Wochen)
4. Meter-Toleranz Code-spezifisch machen (0.3m Grundgeruest, 0.8m AE-Codes)
5. Sidecar Health-Check periodisch (alle 10 Frames)
6. Florence-2 Shadow evaluieren (Match-Rate aktuell 0%)

### Langfristig (1-3 Monate)
7. Nemotron-Parse fuer KI-basiertes PDF-Parsing fertigstellen
8. OCR-Fallback fuer verschluesselte PDFs
9. Knowledge Distillation 32B -> 8B wenn genuegend Green-Samples

---

## 9. Architektur-Diagramm

```
                    ┌─────────────────────────────────────────────┐
                    │              SewerStudio V4.1                │
                    │         WPF / .NET 8+ / Windows 11          │
                    └──────────────┬──────────────────────────────┘
                                   │
                    ┌──────────────┴──────────────────────────────┐
                    │           ServiceProvider                    │
                    │  Startup-Warmup: 8B + nomic + 32B(hybrid)   │
                    └──────┬────────────────┬─────────────────────┘
                           │                │
              ┌────────────┴───┐    ┌───────┴──────────────┐
              │  Ollama Server │    │  Python Sidecar:8100 │
              │  :11434        │    │                      │
              │                │    │  YOLO (TensorRT)     │
              │  8B  (GPU)     │    │  DINO (persistent)   │
              │  32B (hybrid)  │    │  SAM  (persistent)   │
              │  nomic (GPU)   │    │  Florence-2 (lazy)   │
              └────────────────┘    └──────────────────────┘
                                              │
                                    ┌─────────┴─────────┐
                                    │  GPU: RTX 5090    │
                                    │  32 GB VRAM       │
                                    │  ~26 GB belegt    │
                                    │  ~6 GB Reserve    │
                                    └───────────────────┘

              ┌─────────────────────────────────────────────────┐
              │           C:\KI_BRAIN (37 GB)                   │
              │  KnowledgeBase.db    15'703 Samples              │
              │  training_samples    Trainings-Daten             │
              │  fewshot_images      Few-Shot Beispiele          │
              │  frames/             Extrahierte Video-Frames    │
              │  teacher_images/     Lehrer-Annotationen         │
              │  → Spiegelung: E:\Brain (ext. Platte)           │
              └─────────────────────────────────────────────────┘
```

---

*Audit erstellt am 13.04.2026, 21:30 Uhr. Naechster Audit empfohlen nach Abschluss des Nachtbatches.*
