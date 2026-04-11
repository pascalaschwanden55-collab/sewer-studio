# SewerStudio 4.0 — Vollstaendiges Programm-Audit

**Datum:** 11. April 2026
**Erstellt von:** Claude Opus 4.6 (autonome Code-Analyse)

---

## 1. Projektueberblick

| Parameter | Wert |
|---|---|
| Name | SewerStudio 4.0 (AuswertungPro.Next) |
| Zweck | Automatisierte KI-gestuetzte Kanalinspektion |
| Standards | EN 13508-2, VSA-KEK Schweiz 2023 |
| Entworfen von | Solo-Entwickler (keine kommerzielle Nutzung) |
| Framework | WPF / .NET 8+ / MVVM |
| Betriebssystem | Windows 11 |
| Sprache | C# (51.500 Zeilen), Python (3.800 Zeilen), XAML (62 Dateien) |
| Gesamtdateien | 885 C#, 62 XAML, ~20 Python |
| Tests | 378 xUnit-Tests (61 Testdateien) |
| Lizenz | Privat, nicht kommerziell |

---

## 2. Hardware

### Workstation (Produktiv-Modus)

| Komponente | Modell | Spezifikation |
|---|---|---|
| CPU | Intel Core Ultra 9 285K | 24 Kerne, LGA 1851 |
| GPU | ASUS RTX 5090 | 32 GB GDDR7, PCIe 5.0 |
| RAM | DDR5 | 64 GB |
| Storage | NVMe SSD | D:\Haltungen (~2000 Inspektionsvideos) |

### VRAM-Budget (max 29 GB stabil)

| Zustand | YOLO | Qwen-8B | Qwen-32B | DINO | SAM | Total |
|---|---|---|---|---|---|---|
| DETECT | 1.5 GB | — | — | — | — | 1.5 GB |
| CLASSIFY | 1.5 GB | 10 GB | — | — | — | 11.5 GB |
| Normal (Pipeline) | 1.5 GB | 10 GB | — | 3 GB | 3 GB | 17.5 GB |
| ESKALATION | 1.5 GB | — | 22 GB | 3 GB | 3 GB | 29.5 GB |

### Laptop-Modus (reduziert)
Hardware-Abstraktion erlaubt Betrieb auf schwaecher ausgestatteten Geraeten mit reduziertem Modell-Set.

---

## 3. Architektur

### Schichtmodell (Clean Architecture)

```
┌─────────────────────────────────────────────┐
│  UI Layer (WPF)                              │
│  736 .cs, 62 .xaml, 37 ViewModels            │
│  Views, Dialogs, Controls, Theme             │
├─────────────────────────────────────────────┤
│  AI Layer (innerhalb UI)                     │
│  25+ Services, Pipeline, KnowledgeBase       │
│  Training, QualityGate, Overlay              │
├─────────────────────────────────────────────┤
│  Application Layer                           │
│  43 .cs — Interfaces, DTOs, Contracts        │
├─────────────────────────────────────────────┤
│  Domain Layer                                │
│  48 .cs — Models, Enums, Records             │
│  Kein I/O, keine Abhaengigkeiten             │
├─────────────────────────────────────────────┤
│  Infrastructure Layer                        │
│  58 .cs — Import/Export, VSA-Logik            │
│  PDF, Excel, WinCan, IBAK, KINS, XTF         │
└─────────────────────────────────────────────┘
         ↕ HTTP (localhost:8100)
┌─────────────────────────────────────────────┐
│  Python Sidecar (FastAPI)                    │
│  3.800 Zeilen, 7 Endpoints                   │
│  YOLO, DINO, SAM, Training, LoRA             │
└─────────────────────────────────────────────┘
         ↕ HTTP (localhost:11434)
┌─────────────────────────────────────────────┐
│  Ollama                                      │
│  Qwen3-VL-8B / 32B, nomic-embed-text        │
└─────────────────────────────────────────────┘
```

### Architektur-Prinzipien (verbindlich)
- **Thin-AI:** C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
- **VRAM-Budget:** Max 29 GB stabil, nie alle Modelle gleichzeitig
- **QualityGate:** Green/Yellow/Red muss immer durchlaufen
- **Kein grosses Refactoring** ohne explizite Diskussion
- **Laptop-Mode / Workstation-Mode** Hardware-Abstraktion erhalten

---

## 4. KI-Instanzen — Modelle und Funktionen

### 4.1 YOLO (Object Detection)

| Parameter | Wert |
|---|---|
| Modell | YOLOv11m (Ultralytics) |
| Gewichte | yolo11m.pt (Basis) / Custom trainiert |
| VRAM | ~1.5 GB |
| Device | GPU (cuda:0), TensorRT-optimiert |
| Aufgabe | Echtzeit-Vorfilter: "Hier koennte ein Schaden sein" |
| Input | Video-Frame (640x640 px) |
| Output | Bounding-Boxen + Klasse + Confidence |
| Klassen | 10 Defektkategorien (VSA 2018) |
| Betriebsmodus | Permanent geladen, ~5ms/Frame |

**10 YOLO-Klassen (VSA-Merkblatt 2018):**

| ID | Name | VSA-Codes | Beschreibung |
|---|---|---|---|
| 0 | crack | BAB | Risse (Haar-, Quer-, Laengsrisse) |
| 1 | fracture | BAC | Bruch / Einsturz |
| 2 | deformation | BAA, BAF | Verformung + Oberflaechenschaden |
| 3 | displacement | BAJ | Verschobene Rohrverbindung (Versatz) |
| 4 | intrusion | BAG, BBD | Einragender Anschluss + Eindringender Boden |
| 5 | root | BBA | Wurzeln |
| 6 | deposit | BBC, BBB | Ablagerungen + Anhaftende Stoffe/Inkrustation |
| 7 | infiltration | BBF, BBG | Infiltration / Exfiltration |
| 8 | connection | BCA | Seitlicher Anschluss |
| 9 | structural_other | BAD-BAP, BBE, BBH, BCB | Sammelklasse (seltene Schaeden) |

### 4.2 Grounding DINO 1.5 (Open-Vocabulary Detection)

| Parameter | Wert |
|---|---|
| Modell | Grounding DINO 1.5 (Meta/IDEA) |
| VRAM | ~3 GB |
| Device | GPU (cuda:0), pre-warmed |
| Aufgabe | Open-Vocabulary Detection mit Textprompts |
| Input | Frame + natuerlichsprachige Labels ("crack", "root", "deposit") |
| Output | Bounding-Boxen mit Confidence |
| Besonderheit | Findet Objekte die YOLO nicht kennt (zero-shot) |
| Einsatz | Fallback bei YOLO-Miss, BBox-Generierung fuer Training |

### 4.3 SAM 3 (Segment Anything Model)

| Parameter | Wert |
|---|---|
| Modell | Segment Anything Model v3 (Meta) |
| VRAM | ~3 GB |
| Device | GPU (cuda:0), pre-warmed |
| Aufgabe | Pixelgenaue Segmentierung aus BBoxen |
| Input | Frame + Bounding-Boxen (von YOLO/DINO) |
| Output | Masken (RLE-kodiert) + Centroid + Flaeche |
| Einsatz | Quantifizierung (% Querschnitt, mm Ausdehnung) |
| Besonderheit | Ring-Scan fuer Rohrwand-Analyse |

### 4.4 Qwen3-VL-8B (Vision Language Model — FastModel)

| Parameter | Wert |
|---|---|
| Modell | Qwen3-VL-8B (via Ollama) |
| VRAM | ~10 GB |
| Device | GPU, keep_alive=-1 (permanent geladen) |
| Aufgabe | VSA-Code-Klassifikation aus Video-Frames |
| Input | Frame (Base64) + Kontext (DINO/SAM-Ergebnisse, Rohrdurchmesser) |
| Output | JSON: VSA-Code, Severity 1-5, Uhrlage, Quantifizierung |
| Schema | Strict JSON (SchemaOverlay.cs, 732 Zeilen) |
| Besonderheit | Erkennt auch OSD-Meterstand, Material, Wasserstand |

### 4.5 Qwen3-VL-32B (Vision Language Model — ReferenceModel)

| Parameter | Wert |
|---|---|
| Modell | Qwen3-VL-32B (via Ollama) |
| VRAM | ~22 GB |
| Device | GPU, on-demand (nur bei Eskalation) |
| Aufgabe | Zweitmeinung bei unsicheren 8B-Ergebnissen |
| Trigger | allCodesNull, severity >= 4, poorQuality |
| Ablauf | 8B entladen → 32B laden → Re-Analyse → 32B entladen → 8B zurueck |
| Schutz | SemaphoreSlim(1) gegen parallele Modellwechsel |

### 4.6 nomic-embed-text (Embedding-Modell)

| Parameter | Wert |
|---|---|
| Modell | nomic-embed-text (via Ollama) |
| VRAM | Minimal (~200 MB) |
| Aufgabe | Text-Embeddings fuer KnowledgeBase (RAG) |
| Input | Beschreibungstext (Code + Label + Material + DN) |
| Output | Float-Vektor (768 Dimensionen) |
| Einsatz | Aehnlichkeitssuche, Few-Shot-Retrieval, Dedup |

### 4.7 ByteTrack / OC-SORT (Tracker)

| Parameter | Wert |
|---|---|
| Modell | ByteTrack / OC-SORT |
| Device | CPU (kein GPU noetig) |
| Aufgabe | Temporal Tracking ueber Frames hinweg |
| Input | YOLO-Detektionen pro Frame |
| Output | Track-IDs (gleicher Schaden ueber Zeit verfolgen) |

### 4.8 DetectionAggregator (Temporal Voting — NEU)

| Parameter | Wert |
|---|---|
| Typ | Regelbasierter Algorithmus (kein ML) |
| Device | CPU |
| Aufgabe | 500+ Einzelframe-Detektionen → ~20 Ereignisse |
| Prinzip | Schaden muss ueber 3+ Frames konsistent sichtbar sein |
| Parameter | MinFrames=3, MinConfidence=0.4, MeterMergeRadius=1.5m, MaxGap=5 |
| Ergebnis | 15x Verdichtung (bewiesen: 833 → 55 Events, PDF hatte 49) |

---

## 5. Pipeline-Ablauf (End-to-End)

```
Video-Input (.mpg/.mp4)
    │
    ▼
FFmpeg Frame-Extraktion (alle 1.5s)
    │
    ▼
OSD-Meterstand-Erkennung (Regex + Qwen Fallback)
    │
    ▼
YOLO Vorfilter (90% der Frames sind leer → skip)
    │
    ▼
[Optional] DINO Fallback (wenn YOLO nichts findet)
    │
    ▼
SAM Segmentierung (fuer gefundene BBoxen)
    │
    ▼
DetectionAggregator (500+ → ~20 Events)
    │
    ▼
Qwen 8B Klassifikation (nur PeakFrames, ~20 Aufrufe)
    │
    ▼
[Optional] Qwen 32B Eskalation (bei Unsicherheit)
    │
    ▼
QualityGate (Green/Yellow/Red)
    │
    ▼
Protokoll (EN 13508-2 konform)
    │
    ▼
PDF-Export (QuestPDF)
```

---

## 6. Funktionsumfang

### 6.1 Import-Formate
- **WinCan DB3** — SQLite-Datenbank mit Protokollen + Fotos
- **IBAK Daten.txt** — Textbasiertes Inspektionsprotokoll
- **KINS kiDVDaten.txt** — KINS-Format
- **XTF/SIA405** — Schweizer Austauschformat
- **PDF-Protokolle** — Fretz AG, KIT Bauinspekt, Abwasser Uri (3 Formate)
- **Fotos** — JPEG/PNG aus Inspektionen

### 6.2 Export-Formate
- **PDF** — EN 13508-2 Leitungsbildbericht (QuestPDF)
- **Excel** — Haltungsliste mit VSA-Bewertungen (ClosedXML)

### 6.3 Video-Player
- **LibVLC** Integration mit Timeline
- **Echtzeit-Overlay** (YOLO-Boxen, SAM-Masken, VSA-Codes)
- **Pausenmodus** bei KI-Befunden (Leertaste=weiter, O=OK, Delete=verwerfen)
- **Bidirektionale Sync** Befundliste ↔ Video-Position

### 6.4 KnowledgeBase (Self-Learning)
- **SQLite** mit Embeddings (nomic-embed-text)
- **Cosine-Similarity** Dedup (0.92 Schwelle)
- **Few-Shot RAG** — Top-K aehnliche Beispiele als Qwen-Kontext
- **Auto-Approve** — Konfigurierbarer Schwellenwert (default 0.60)
- **Batch-Enrichment** — Automatische KB-Anreicherung nach Nachtlauf

### 6.5 Self-Training
- **VideoSelfTrainingOrchestrator** — Einzelhaltung: Blindlauf → Vergleich mit PDF
- **BatchSelfTrainingOrchestrator** — Alle Haltungen ueber Nacht
- **DifferenceAnalyzer** — KI vs. Protokoll: TP/FN/FP/CodeMismatch
- **BenchmarkRunner** — 20 Goldstandard-Haltungen, Regressions-Erkennung
- **YoloRetrainOrchestrator** — Automatisches YOLO-Retraining mit Benchmark-Gate

### 6.6 VSA-Bewertung
- **Zustandsklassifizierung** 0-4 (VSA-Richtlinie 2023)
- **Dringlichkeitszahl** (DZ = ZN × 100 × B1 × B2 × B3 × B4)
- **Sanierungsmassnahmen** — Empfehlungen basierend auf Schadensart
- **Kosten-Schaetzung** — Devis/Offerte-Generierung

---

## 7. Projekt-Metriken

| Metrik | Wert |
|---|---|
| C# Code | 51.500 Zeilen |
| Python Code | 3.800 Zeilen |
| XAML | 62 Dateien |
| Tests | 378 (xUnit) |
| NuGet-Pakete | 16 Haupt-Dependencies |
| Services | 50+ (ServiceProvider) |
| ViewModels | 37 |
| KI-Modelle | 7 (YOLO, DINO, SAM, Qwen-8B, Qwen-32B, nomic-embed, ByteTrack) |
| YOLO-Klassen | 10 (VSA 2018) |
| Trainings-Frames | 6.806 (v3 mit 4.265 echten DINO+SAM-BBoxen) |
| Benchmark-Haltungen | 20 (alle 10 Klassen) |
| Inspektions-Videos | ~2.000 (D:\Haltungen) |

---

## 8. Aenderungen vom 11. April 2026

**29 Commits** an einem Tag:

| Kategorie | Aenderungen |
|---|---|
| Neue Komponenten | DetectionAggregator, YoloDefectTaxonomy, YoloAnnotationGenerator, InitialTrainingOrchestrator |
| Pipeline-Integration | Aggregator in MultiModelAnalysisService (FFmpeg + NVDEC), Qwen PeakFrame-Modus |
| Training | 3x YOLO-Training (v1 Full-Frame, v2 alle Klassen, v3 DINO+SAM-BBoxen) |
| Audit-Fixes | 11/12 BLOCKER, 3 HIGH, 1 MEDIUM, 1 Security |
| Tests | +45 neue Tests (DifferenceAnalyzer, BenchmarkRunner, Taxonomie, Aggregator) |
| VSA-Korrektur | Codes korrigiert nach VSA-Merkblatt 2018 |
| Tools | initial_yolo_training.py, generate_bboxes_dino_sam.py, smoke_test_pipeline.py |

---

*Erstellt am 11. April 2026 durch automatische Code-Analyse (Claude Opus 4.6)*
