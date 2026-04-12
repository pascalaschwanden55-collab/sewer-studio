# SewerStudio 4.0 — Vollstaendiges Programm-Audit

**Datum:** 11. April 2026 (aktualisiert)
**Erstellt von:** Claude Opus 4.6 (autonome Code-Analyse)
**Policy-Version:** 1.0.0

---

## 1. Projektueberblick

| Parameter | Wert |
|---|---|
| Name | SewerStudio 4.0 (AuswertungPro.Next) |
| Zweck | Automatisierte KI-gestuetzte Kanalinspektion |
| Standards | EN 13508-2, VSA-KEK Schweiz 2023, VSA-Merkblatt 2018 |
| Entworfen von | Solo-Entwickler (keine kommerzielle Nutzung) |
| Framework | WPF / .NET 10.0 / MVVM (CommunityToolkit.Mvvm) |
| Betriebssystem | Windows 11 Pro |
| Sprache | C# (~51.500 Zeilen), Python (~3.800 Zeilen), XAML (62 Dateien) |
| Gesamtdateien | 888 C#, 62 XAML, ~21 Python |
| Tests | 308+ xUnit-Tests (56 Testdateien) |
| Lizenz | Privat, nicht kommerziell |
| Repository | Git, Worktree-basierte Entwicklung |

---

## 2. Hardware

### Workstation (Produktiv-Modus)

| Komponente | Modell | Spezifikation |
|---|---|---|
| CPU | Intel Core Ultra 9 285K | 24 Kerne, LGA 1851 |
| GPU | ASUS RTX 5090 | 32 GB GDDR7, PCIe 5.0 |
| RAM | DDR5 | 64 GB |
| Storage | NVMe SSD | D:\Haltungen (~2.000 Inspektionsvideos) |

### VRAM-Budget (max 29 GB stabil)

| Zustand | YOLO | Qwen-8B | Qwen-32B | DINO | SAM | Total |
|---|---|---|---|---|---|---|
| DETECT | 1.5 GB | — | — | — | — | 1.5 GB |
| CLASSIFY | 1.5 GB | 10 GB | — | — | — | 11.5 GB |
| Normal (Pipeline) | 1.5 GB | 10 GB | — | 3 GB | 3 GB | 17.5 GB |
| ESKALATION | 1.5 GB | — | 22 GB | 3 GB | 3 GB | 29.5 GB |

### Laptop-Modus (reduziert)
Hardware-Abstraktion erlaubt Betrieb auf schwaecher ausgestatteten Geraeten mit reduziertem Modell-Set (nur YOLO + Qwen-8B, ohne DINO/SAM).

### Inference-Orchestrator Zustaende
1. **DETECT** → GPU: YOLO | CPU: Tracker + Aggregator
2. **SEGMENT** → GPU: YOLO + SAM | Qwen: entladen
3. **CLASSIFY** → GPU: YOLO + Qwen | SAM/DINO: entladen

---

## 3. Architektur

### Schichtmodell (Clean Architecture)

```
┌─────────────────────────────────────────────────┐
│  UI Layer (WPF)                                  │
│  888 .cs, 62 .xaml, 37 ViewModels                │
│  Views, Dialogs, Controls, Theme, Overlays       │
├─────────────────────────────────────────────────┤
│  AI Layer (innerhalb UI)                         │
│  126 .cs — Pipeline, KnowledgeBase, Training     │
│  QualityGate, Overlay, Tracking, Aggregation     │
├─────────────────────────────────────────────────┤
│  Application Layer                               │
│  43 .cs — Interfaces, DTOs, Contracts            │
│  KnowledgeBase, Sanierung, Devis, Diagnostics    │
├─────────────────────────────────────────────────┤
│  Domain Layer                                    │
│  48 .cs — Models, Enums, Records                 │
│  Kein I/O, keine Abhaengigkeiten                 │
├─────────────────────────────────────────────────┤
│  Infrastructure Layer                            │
│  58 .cs — Import/Export, VSA-Logik               │
│  PDF, Excel, WinCan, IBAK, KINS, XTF             │
└─────────────────────────────────────────────────┘
         ↕ HTTP (localhost:8100)
┌─────────────────────────────────────────────────┐
│  Python Sidecar (FastAPI)                        │
│  ~3.800 Zeilen, 9 Routen-Module, 16 Endpoints   │
│  YOLO, DINO, SAM, Training, LoRA, Video, Enhance │
└─────────────────────────────────────────────────┘
         ↕ HTTP (localhost:11434)
┌─────────────────────────────────────────────────┐
│  Ollama                                          │
│  Qwen3-VL-8B / 32B, nomic-embed-text            │
└─────────────────────────────────────────────────┘
```

### Architektur-Prinzipien (verbindlich)
- **Thin-AI:** C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
- **VRAM-Budget:** Max 29 GB stabil, nie alle Modelle gleichzeitig
- **QualityGate:** Green/Yellow/Red muss immer durchlaufen
- **Kein grosses Refactoring** ohne explizite Diskussion
- **Laptop-Mode / Workstation-Mode** Hardware-Abstraktion erhalten
- **Kein Skill darf seine eigene Freigabe erteilen** (Governance-Prinzip)

---

## 4. KI-Instanzen — Modelle und Funktionen

### 4.1 YOLO (Object Detection)

| Parameter | Wert |
|---|---|
| Modell | YOLOv11m (Ultralytics) |
| Gewichte | yolo11m.pt (Basis) / yolo_v{N}.pt (Custom trainiert) |
| VRAM | ~1.5 GB |
| Device | GPU (cuda:0), TensorRT-optimiert |
| Aufgabe | Echtzeit-Vorfilter: "Hier koennte ein Schaden sein" |
| Input | Video-Frame (640x640 px) |
| Output | Bounding-Boxen + Klasse + Confidence |
| Klassen | 10 Defektkategorien (VSA-Merkblatt 2018) |
| Betriebsmodus | Permanent geladen, ~5ms/Frame |
| Modell-Management | active.json Pointer, meta.json Metadaten |
| Training | Automatisches Re-Training via YoloRetrainOrchestrator |

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

**YOLO-Modell-Lebenszyklus:**
```
[trained] → canary → candidate → current → deprecated
```
Verwaltet durch `model-promotion-warden`, mensch-pflichtig, snapshot-pflichtig.

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
| Einsatz | QualityGate Yellow/Red Eskalation, BBox-Generierung fuer Training |

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
| Input | PeakFrame (Base64) + Kontext (YOLO-Klasse, DINO/SAM, Rohrdurchmesser, KB-Beispiele) |
| Output | JSON: VSA-Code, Severity 1-5, Uhrlage, Quantifizierung |
| Schema | Strict JSON (SchemaOverlay.cs, 732 Zeilen) |
| Besonderheit | PeakFrame-Modus: nur ~20 Aufrufe pro Video statt hunderte |
| RAG | Few-Shot-Retrieval aus KnowledgeBase (Top-3 aehnliche Befunde) |

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

### 4.8 DetectionAggregator (Temporal Voting)

| Parameter | Wert |
|---|---|
| Typ | Regelbasierter Algorithmus (kein ML) |
| Device | CPU |
| Aufgabe | 500+ Einzelframe-Detektionen → ~20 Ereignisse |
| Prinzip | Schaden muss ueber 3+ Frames konsistent sichtbar sein |
| Parameter | MinFrames=3, MinConfidence=0.4, MeterMergeRadius=1.5m, MaxGap=5 |
| Ergebnis | 15x Verdichtung (bewiesen: 833 → 55 Events, PDF hatte 49) |
| Besonderheit | Duplikat-Unterdrueckung: Steuercodes (BCD, BCE) aus altem Pfad, Defekte nur vom Aggregator |

---

## 5. Pipeline-Ablauf (End-to-End)

```
Video-Input (.mpg/.mp4)
    │
    ▼
FFmpeg Frame-Extraktion (alle 1.5s) / NVDEC GPU-Dekodierung
    │
    ▼
OSD-Meterstand-Erkennung (Regex + Qwen Fallback)
    │
    ▼
YOLO Vorfilter (90% der Frames sind leer → skip)
    │
    ▼
ByteTrack/OC-SORT Tracking (Track-IDs zuweisen)
    │
    ▼
DetectionAggregator (500+ Einzeldetektionen → ~20 Events)
    │
    ▼
[Optional] DINO Fallback (QualityGate Yellow/Red)
    │
    ▼
SAM Segmentierung (fuer gefundene BBoxen → Masken)
    │
    ▼
Qwen 8B Klassifikation (nur PeakFrames, ~20 Aufrufe)
    ├── YOLO-Klasse als Kontext
    ├── KB Few-Shot-Retrieval (Top-3 aehnliche Befunde)
    └── Rohrdurchmesser + Material als Kontext
    │
    ▼
[Optional] Qwen 32B Eskalation (bei Unsicherheit)
    │
    ▼
QualityGate (Green/Yellow/Red, 8-Signal-Fusion)
    │
    ▼
Protokoll (EN 13508-2 konform)
    │
    ▼
PDF-Export (QuestPDF) / Excel-Export (ClosedXML)
```

### QualityGate — Multi-Signal-Fusion

| Signal | Gewicht | Quelle |
|---|---|---|
| YoloConf | 0.12 | YOLO Detection Confidence |
| DinoConf | 0.18 | Grounding DINO Confidence |
| SamMaskStability | 0.10 | SAM Segmentation Quality |
| QwenVisionConf | 0.15 | Qwen Vision Model Confidence |
| LlmCodeConf | 0.12 | LLM Code-Klassifikation |
| KbSimilarity | 0.12 | KnowledgeBase Cosine-Similarity |
| KbCodeAgreement | 0.10 | KB stimmt mit Code ueberein (boolean) |
| PlausibilityScore | 0.11 | Regelbasierte Plausibilitaet |

**Schwellen:** Green >= 0.75 | Yellow >= 0.45 | Red < 0.45

---

## 6. Funktionsumfang

### 6.1 Import-Formate (7 Formate)

| Format | Service | Beschreibung |
|---|---|---|
| WinCan DB3 | IWinCanDbImportService | SQLite-Datenbank mit Protokollen + Fotos |
| IBAK Daten.txt | IIbakImportService | Textbasiertes Inspektionsprotokoll |
| KINS kiDVDaten.txt | IKinsImportService | KINS-Format |
| XTF/SIA405 | IXtfImportService | Schweizer Austauschformat |
| PDF Fretz AG | PdfProtocolTableParser | "Meter \| Code \| Beschreibung \| MPEG \| Foto \| Stufe" |
| PDF Abwasser Uri | PdfProtocolTableParser | "POSITION [m] SK \| CODE \| BEOBACHTUNG \| VIDEO \| FOTO" |
| PDF Caesar | PdfProtocolTableParser | Formularbasiertes Format + Ziffern-Decodierung |

Alle Import-Services: `Result<ImportStats> ImportXxx(string exportRoot, Project project, ImportRunContext? ctx = null)`

### 6.2 Export-Formate
- **PDF** — EN 13508-2 Leitungsbildbericht (QuestPDF)
- **Excel** — Haltungsliste mit VSA-Bewertungen (ClosedXML)
- **Devis/Offerte** — Sanierungskosten-Export

### 6.3 Video-Player
- **LibVLC** Integration mit Timeline
- **Echtzeit-Overlay** (YOLO-Boxen, SAM-Masken, VSA-Codes, Uhrlage)
- **Pausenmodus** bei KI-Befunden (Leertaste=weiter, O=OK, Delete=verwerfen)
- **Bidirektionale Sync** Befundliste ↔ Video-Position
- **NVDEC GPU-Dekodierung** als Alternative zu FFmpeg

### 6.4 KnowledgeBase (Self-Learning)

| Komponente | Beschreibung |
|---|---|
| Storage | SQLite (WAL-Modus) mit Samples, Embeddings, Versions, CategoryWeights |
| Embedding | nomic-embed-text (768 Dimensionen) via Ollama |
| Dedup | Cosine-Similarity >= 0.92 (normal), >= 0.85 (korrigiert) |
| Retrieval | Top-K aehnliche Beispiele als Qwen-Kontext (Few-Shot RAG) |
| Auto-Approve | Konfigurierbarer Schwellenwert (default 0.60) |
| Enrichment | Automatische KB-Anreicherung nach Batch-Laeufen |
| Mirror | KnowledgeMirrorService fuer Backup/Sync |
| Holdout-Disziplin | Holdout-Samples duerfen nie in Training zurueckfliessen |

### 6.5 Self-Training-System

| Service | Aufgabe |
|---|---|
| VideoSelfTrainingOrchestrator | Einzelhaltung: Blindlauf → Vergleich mit Protokoll |
| BatchSelfTrainingOrchestrator | Alle Haltungen ueber Nacht, vollautomatisch |
| DifferenceAnalyzer | KI vs. Protokoll: TP/FN/FP/CodeMismatch |
| BenchmarkRunner | 20 Goldstandard-Haltungen, F1/Precision/Recall |
| BenchmarkMetricsStore | JSON-Zeitreihen, Regressions-Erkennung |
| YoloRetrainOrchestrator | Automatisches YOLO-Retraining mit Benchmark-Gate |
| YoloAnnotationGenerator | Ground-Truth → YOLO-Format-Labels |
| ProtocolLoaderFactory | Laedt Protokoll aus WinCan/IBAK/PDF/KINS |
| ProtocolToGroundTruthMapper | ProtocolEntry → GroundTruthEntry |
| MeterToFrameResolver | Meter → Video-Frame zuordnen |
| FrameQualityFilter | Schaerfe/Helligkeit/Duplikat-Filter |
| KbDeduplicationService | Similarity-Check vor KB-Indexierung |
| KbEnrichmentService | Review-Entscheidungen → KB |

**DifferenceAnalyzer Matching-Logik:**

| Score | Code-Match | Ergebnis |
|---|---|---|
| >= 0.40 | ja | TruePositive |
| >= 0.25 | nein | CodeMismatch |
| < 0.25 | — | FalseNegative |

Score-Gewichtung: Code 40%, Meter-Naehe 30%, Schweregrad 15%, Uhrlage 15%

**Benchmark-Regression:**
- Global F1 max -5% relativ → Promotion blockiert
- Per-Code F1 max -10% relativ → Code-spezifische Warnung
- Baseline: Durchschnitt der letzten 3 Laeufe

### 6.6 VSA-Bewertung
- **Zustandsklassifizierung** 0-4 (VSA-Richtlinie 2023)
- **Dringlichkeitszahl** (DZ = ZN x 100 x B1 x B2 x B3 x B4)
- **Sanierungsmassnahmen** — Empfehlungen basierend auf Schadensart
- **Kosten-Schaetzung** — Devis/Offerte-Generierung

### 6.7 Modell-Promotion (NEU)

YOLO-Modelle durchlaufen einen formalen Lebenszyklus:

```
[trained] → canary → candidate → current → deprecated / rejected
```

| Status | Beschreibung |
|---|---|
| trained | Frisch erzeugt, noch ungeprueft |
| canary | Technisch geprueft (laedt, inferiert), kein Blind-Eval |
| candidate | Vollstaendig evaluiert, promotionsfaehig |
| current | Aktives Modell in active.json, max 1 gleichzeitig |
| deprecated | Abgeloest, 30 Tage Aufbewahrung |
| rejected | Pruefung fehlgeschlagen |

Promotion ist **mensch-pflichtig** und **snapshot-pflichtig** (SNAPSHOT_SCHEMA.md).

---

## 7. Agenten-Architektur (Skill-Governance)

SewerStudio wird durch 25 spezialisierte Skills gesteuert, organisiert in einer formalen Governance-Hierarchie.

### 7.1 Governance-Hierarchie

```
User (hoechste Autoritaet)
  └─ model-promotion-warden (Promote, Block, Mensch-pflichtig)
      └─ sewer-quality-gate-keeper (Block, Snapshot-pflichtig)
          ├─ vram-monitor (Block bei OOM)
          ├─ fastapi-sidecar-tester (Block bei Sidecar-Ausfall)
          └─ active-learning-curator (Empfehlen)
              └─ sqlite-kb-inspector (Lesen)
```

### 7.2 Skill-Uebersicht (25 Skills + 4 Standalone)

| Kategorie | Skills | Befugnisse |
|---|---|---|
| **Governance (3)** | quality-gate-keeper, model-promotion-warden, active-learning-curator | pruefen, blockieren, promoten |
| **KI-Pipeline (4)** | ai-model-engineer, ai-deployment-packager, ai-overlay-visualizer, sewer-pipeline-auditor | schreiben, pruefen, empfehlen |
| **Hardware (3)** | vram-monitor, ollama-model-manager, fastapi-sidecar-tester | pruefen, blockieren |
| **Code & Build (3)** | msbuild-error-parser, xaml-binding-checker, sewer-testing | schreiben, pruefen, blockieren |
| **Domaenen-Wissen (7)** | sewer-architektur, sewer-fachwissen, sewer-pdf-formate, sewer-wpf-ui, sewer-explain, ki-kanalinspektion, sqlite-kb-inspector | pruefen, empfehlen |
| **Meta & Utility (5+1)** | selbst-pruefung, project-architect, deutsch-only, einfach-erklaeren, lokalzeit, ehrliche-meinung | empfehlen, AlwaysApply |

**Standalone-Dokumente:**
- SKILL_GOVERNANCE.md — Governance-Matrix mit Befugnissen und Eskalationsregeln
- SKILL_INDEX.md — Einstiegskarte fuer alle Skills
- CONTRIBUTING.md — Pflichtprozess fuer neue Skills

### 7.3 Kernprinzipien

1. **Kein Skill darf seine eigene Freigabe erteilen.** Training ≠ Pruefung ≠ Promotion.
2. **Blockieren sticht empfehlen.** Ein Block kann nur durch den User aufgehoben werden.
3. **Der restriktivere Befund gewinnt.** Bei Konflikten zwischen zwei pruefenden Skills.
4. **Jeder blockierende Entscheid muss eine konkrete Ursache nennen.** Nie nur "NO-GO".
5. **Keine Policy-Aenderung ohne Versionssprung.**

### 7.4 Reifegrade

| Reifegrad | Bedeutung | Skills |
|---|---|---|
| **Kritisch (K)** | Kernverfassung, keine Aenderung ohne Versionssprung | quality-gate-keeper, model-promotion-warden, vram-monitor, fastapi-sidecar-tester, sewer-architektur, sewer-fachwissen, sqlite-kb-inspector |
| **Stabil (S)** | Produktiv, aenderbar mit Begruendung | ai-model-engineer, active-learning-curator, sewer-testing, sewer-wpf-ui, u.a. |
| **AlwaysApply (A)** | Immer aktiv bei jeder Interaktion | selbst-pruefung, ehrliche-meinung, deutsch-only, einfach-erklaeren, lokalzeit |

### 7.5 Policy-Dokumente

| Dokument | Pfad | Inhalt |
|---|---|---|
| TRAINING_STANDARD.md | quality-gate-keeper/ | Alle Schwellen, Holdout-Disziplin |
| MODEL_PROMOTION_POLICY.md | model-promotion-warden/ | 6-Stufen-Lebenszyklus, Sperrgruende-Katalog |
| SNAPSHOT_SCHEMA.md | model-promotion-warden/ | Formale Snapshot-Definition (JSON-Schema) |
| DECISION_LOG_TEMPLATE.md | model-promotion-warden/ | Audit-Log im JSONL-Format |

### 7.6 Sperrgruende-Katalog

| Code | Bedeutung |
|---|---|
| NO_SNAPSHOT | Kein Benchmark-Snapshot vorhanden |
| NO_BLIND_EVAL | Kein Blind-Eval gegen Holdout |
| GLOBAL_REGRESSION | Globaler F1 mehr als 5% unter Baseline |
| PER_CODE_REGRESSION | Mindestens ein Code mehr als 10% unter Baseline |
| HOLDOUT_MISMATCH | Holdout-Version stimmt nicht mit Baseline ueberein |
| MISSING_META | Metadaten-Datei fehlt oder unvollstaendig |
| STATUS_CONFLICT | Modell hat unerwarteten Status |
| CANARY_FAIL | Technische Canary-Pruefung fehlgeschlagen |
| POLICY_MISMATCH | Policy-Version hat sich seit Snapshot geaendert |

### 7.7 Verbotene Abkuerzungen

1. Kein direktes Aendern von `active.json` ausserhalb des model-promotion-warden
2. Kein direktes Aendern von Modellstatus in `.meta.json` ausserhalb des Warden
3. Kein Umgehen des Gate-Keepers ohne Decision-Log-Eintrag
4. Kein stilles Deaktivieren von Blockierregeln
5. Keine Policy-Aenderung ohne Versionssprung
6. Keine Holdout-Samples in Training zurueckfuehren
7. Kein Loeschen von Snapshots die zu aktiven Promotions gehoeren

---

## 8. Python Sidecar (FastAPI)

### Endpoints (9 Routen-Module, 16 Endpoints)

| Modul | Endpoints | Aufgabe |
|---|---|---|
| yolo.py | /yolo/detect, /model/reload | YOLO-Inference, Hot-Swap |
| dino.py | /dino/detect | Grounding DINO Open-Vocabulary |
| sam.py | /sam/segment | SAM Segmentierung aus BBoxen |
| training.py | /training/train-yolo, /training/jobs/{id} | YOLO-Training, Job-Status |
| lora_training.py | /training/lora | LoRA Fine-Tuning |
| health.py | /health | Sidecar Health-Check |
| video.py | /video/... | Video-Verarbeitung |
| enhance.py | /enhance/... | Bildverbesserung |
| pipe_axis.py | /pipe/axis | Rohrachsen-Erkennung |

### Modell-Management
- **active.json** — Zeigt auf aktuelles YOLO-Modell
- **TensorRT-Optimierung** — Auto-Export .pt → .engine beim ersten Laden
- **Hot-Swap** — Modellwechsel ohne Sidecar-Neustart via /model/reload
- **Inference-Locking** — Verhindert Training waehrend aktiver Inference

---

## 9. NuGet-Abhaengigkeiten

| Paket | Version | Zweck |
|---|---|---|
| CommunityToolkit.Mvvm | 8.4.0 | MVVM-Framework |
| LibreHardwareMonitorLib | 0.9.6 | Hardware-Monitoring (GPU-Temp, VRAM) |
| Microsoft.Data.Sqlite | 10.0.3 | SQLite fuer KnowledgeBase |
| Microsoft.Extensions.Logging | 10.0.2 | Logging-Framework |
| Microsoft.Playwright | 1.50.0 | Browser-Automation (OSD-Fallback) |
| LibVLCSharp | 3.9.5 | Video-Player |
| LibVLCSharp.WPF | 3.9.5 | WPF-Integration fuer LibVLC |
| Polly | 8.* | Retry/Circuit-Breaker (HTTP-Aufrufe) |
| QuestPDF | 2026.2.0 | PDF-Generierung |
| UglyToad.PdfPig | 1.7.0-custom-5 | PDF-Text-Extraktion |
| VideoLAN.LibVLC.Windows | 3.0.23 | LibVLC Native Binaries |

---

## 10. ServiceProvider — Alle Services (27)

| Property | Typ | Verwendung |
|---|---|---|
| sp.Settings | AppSettings | App-Konfiguration |
| sp.Diagnostics | DiagnosticsOptions | pdftotext-Pfad, Debug-Flags |
| sp.Logger | ILogger | Logging |
| sp.LoggerFactory | ILoggerFactory | Logger erstellen |
| sp.ErrorCodes | ErrorCodes | Fehlercodes |
| sp.Projects | IProjectRepository | Projekte laden/speichern |
| sp.PdfImport | IPdfImportService | PDF → HaltungRecord |
| sp.XtfImport | IXtfImportService | XTF/SIA405 → HaltungRecord |
| sp.WinCanImport | IWinCanDbImportService | WinCan DB3 → HaltungRecord + Protokoll |
| sp.IbakImport | IIbakImportService | IBAK Daten.txt → HaltungRecord + Protokoll |
| sp.KinsImport | IKinsImportService | KINS kiDVDaten.txt → HaltungRecord |
| sp.ExcelExport | IExcelExportService | Export nach Excel |
| sp.Vsa | IVsaEvaluationService | VSA-Zustandsbewertung |
| sp.Protocols | IProtocolService | Protokoll-Verwaltung (Revisionen) |
| sp.PhotoImport | IPhotoImportService | Foto-Import |
| sp.ProtocolPdfExporter | ProtocolPdfExporter | Protokoll → PDF |
| sp.ProtocolAi | IProtocolAiService | KI-Protokoll-Generierung |
| sp.CodeCatalog | ICodeCatalogProvider | VSA-Code-Katalog |
| sp.VsaCatalogResolvedPath | string | Pfad zum VSA-Katalog |
| sp.Retrieval | IRetrievalService? | KB-Aehnlichkeitssuche (nullable) |
| sp.PipelineCfg | PipelineConfig | Sidecar-URL, Pipeline-Einstellungen |
| sp.Sidecar | PythonSidecarService | Sidecar-Prozess starten/stoppen |
| sp.MeasureRecommendation | IMeasureRecommendationService | Sanierungsmassnahmen |
| sp.Dialogs | IDialogService | Modale Dialoge |
| sp.PlaywrightInstaller | PlaywrightInstaller | Browser-Installation |
| sp.DevisGenerator | IDevisGenerator | Devis/Offerte |
| sp.DevisExcelExporter | DevisExcelExporter | Devis → Excel |

### Factory-Methoden

```csharp
// KI-Video-Pipeline erstellen:
IVideoAnalysisPipelineService CreateVideoAnalysisPipeline(
    AiRuntimeConfig cfg,
    IAiSuggestionPlausibilityService plausibility,
    HttpClient http)

// Sanierungs-Optimierung:
IAiSanierungOptimizationService CreateSanierungOptimization(
    AiRuntimeConfig cfg, HttpClient? http = null)
```

---

## 11. Projekt-Metriken

| Metrik | Wert |
|---|---|
| **Code** | |
| C# Dateien | 888 |
| C# Zeilen | ~51.500 |
| Python Dateien (Sidecar) | 21 |
| Python Zeilen | ~3.800 |
| XAML Dateien | 62 |
| XAML Zeilen | ~17.400 |
| **Architektur** | |
| Services (ServiceProvider) | 27 Properties + 2 Factory-Methoden |
| ViewModels | 37 |
| KI-Services (Ai-Ordner) | 126 .cs Dateien |
| Training-Services | 54 .cs Dateien |
| NuGet-Pakete | 11 |
| **Tests** | |
| Testdateien | 56 |
| Testmethoden ([Fact]/[Theory]) | 308+ |
| **KI-Modelle** | |
| Aktive Modelle | 7 (YOLO, DINO, SAM, Qwen-8B, Qwen-32B, nomic-embed, ByteTrack) |
| YOLO-Klassen | 10 (VSA-Merkblatt 2018) |
| Trainings-Frames | 6.806 (v3 mit 4.265 echten DINO+SAM-BBoxen) |
| Benchmark-Haltungen | 20 (alle 10 Klassen, 28-66 Defekte) |
| **Daten** | |
| Inspektions-Videos | ~2.000 (D:\Haltungen) |
| PDF-Formate | 3 (Fretz AG, Abwasser Uri, Caesar) |
| Import-Formate | 7 (WinCan, IBAK, KINS, XTF, 3x PDF) |
| **Agenten-Architektur** | |
| Skills (Verzeichnisse) | 25 |
| Standalone-Dokumente | 4 (Governance, Index, Contributing, ehrliche-meinung) |
| Policy-Dokumente | 4 (Training-Standard, Promotion-Policy, Snapshot-Schema, Decision-Log) |
| Governance-Reifegrade | 3 (Kritisch, Stabil, AlwaysApply) |
| Sperrgruende-Codes | 9 |

---

## 12. Dateistruktur

```
SewerStudio_KI_4.0/
├── AuswertungPro.sln
├── Directory.Build.props
├── CLAUDE.md                              ← Projekt-Kontext fuer Agenten
├── docs/
│   └── SewerStudio_Programm_Audit_2026-04-11.md
├── src/
│   ├── AuswertungPro.Next.Domain/         ← Modelle, Enums, Records (kein I/O)
│   ├── AuswertungPro.Next.Application/    ← Interfaces, DTOs, Contracts
│   │   ├── Ai/KnowledgeBase/
│   │   ├── Ai/Sanierung/
│   │   ├── Devis/
│   │   ├── Export/
│   │   ├── Import/
│   │   ├── Projects/
│   │   ├── Protocol/
│   │   └── Vsa/
│   ├── AuswertungPro.Next.Infrastructure/ ← Import/Export, VSA-Logik
│   └── AuswertungPro.Next.UI/             ← WPF-App, ViewModels, KI
│       ├── Ai/
│       │   ├── KnowledgeBase/             ← KB-Manager, Embedding, Retrieval
│       │   ├── Pipeline/                  ← MultiModel, SingleFrame, Quantification
│       │   ├── Training/                  ← Self-Training, Benchmark, Batch
│       │   │   ├── Models/                ← GroundTruth, Difference, BatchModels
│       │   │   └── Services/              ← Parser, Analyzer, Resolver
│       │   ├── QualityGate/               ← Evidence, Calibration, Temperature
│       │   └── Shared/                    ← FfmpegLocator, VsaCatalog
│       ├── ViewModels/                    ← 37 MVVM ViewModels
│       ├── Views/                         ← XAML Windows + Pages
│       └── Services/                      ← UI-Services (Dialog, Theme, etc.)
├── tests/                                 ← 56 Testdateien, 308+ Tests
├── tools/                                 ← CLI-Tools (PdfParser, MdbReader)
└── sidecar/                               ← Python FastAPI (YOLO/DINO/SAM)
    ├── sidecar/
    │   ├── models/                        ← Modell-Wrapper (yolo, dino, sam)
    │   └── routes/                        ← 9 Routen-Module, 16 Endpoints
    └── models/
        └── yolo26m/                       ← Modell-Dateien + active.json
```

```
~/.claude/skills/                          ← Agenten-Governance
├── SKILL_GOVERNANCE.md                    ← Verfassung (Befugnisse, Eskalation)
├── SKILL_INDEX.md                         ← Einstiegskarte
├── CONTRIBUTING.md                        ← Pflichtprozess fuer neue Skills
├── ehrliche-meinung.md                    ← AlwaysApply Meta-Skill
├── sewer-quality-gate-keeper/
│   ├── SKILL.md                           ← Pre-Flight + Post-Run Audit
│   └── TRAINING_STANDARD.md               ← Schwellen + Holdout-Disziplin
├── model-promotion-warden/
│   ├── SKILL.md                           ← Promotion, Rollback, Metadaten
│   ├── MODEL_PROMOTION_POLICY.md          ← 6-Stufen-Lebenszyklus
│   ├── SNAPSHOT_SCHEMA.md                 ← Formale Snapshot-Definition
│   └── DECISION_LOG_TEMPLATE.md           ← Audit-Log (JSONL)
├── active-learning-curator/
│   └── SKILL.md                           ← Review-Pakete + Stop-Regeln
└── [22 weitere Skill-Verzeichnisse]
```

---

## 13. Aenderungshistorie

### 11. April 2026 — Initiales Audit + Governance-Architektur

**29+ Commits:**

| Kategorie | Aenderungen |
|---|---|
| **Neue Komponenten** | DetectionAggregator, YoloDefectTaxonomy, YoloAnnotationGenerator, InitialTrainingOrchestrator |
| **Pipeline-Integration** | Aggregator in MultiModelAnalysisService (FFmpeg + NVDEC), Qwen PeakFrame-Modus |
| **Training** | 3x YOLO-Training (v1 Full-Frame, v2 alle Klassen, v3 DINO+SAM-BBoxen mit 4.265 echten BBoxen) |
| **Audit-Fixes** | 11/12 BLOCKER, 3 HIGH, 1 MEDIUM, 1 Security |
| **Tests** | +45 neue Tests (DifferenceAnalyzer, BenchmarkRunner, Taxonomie, Aggregator) |
| **VSA-Korrektur** | Codes korrigiert nach VSA-Merkblatt 2018, Regex erweitert (BABBB, BCAXB, bis 7 Zeichen) |
| **PDF** | Caesar-Format Parser (formularbasiert + Ziffern-Decodierung) |
| **Tools** | initial_yolo_training.py, generate_bboxes_dino_sam.py, smoke_test_pipeline.py |
| **Governance** | 3 Governance-Skills, 4 Policy-Dokumente, SKILL_GOVERNANCE.md, CONTRIBUTING.md |

### Behobene Audit-Findings

| ID | Schwere | Beschreibung | Status |
|---|---|---|---|
| B3 | BLOCKER | FFmpeg-Orphan-Prozesse | Behoben (Logging statt blind kill) |
| B4 | BLOCKER | Manifest-Backup fehlt | Behoben |
| B5 | BLOCKER | Division-by-Zero in Metrics | Behoben |
| B7 | BLOCKER | KB-Dedup race condition | Behoben |
| B8 | BLOCKER | Sidecar restart bei OOM | Behoben |
| B9 | BLOCKER | DifferenceAnalyzer Tests | Behoben (17 Tests) |
| B10 | BLOCKER | BenchmarkRunner Tests | Behoben (22 Tests) |
| S1 | SECURITY | pdftotext Pfad-Injection | Behoben |
| H14 | HIGH | Pruning-Schutz fehlt | Behoben |
| H15 | HIGH | Manifest-Backup Atomaritaet | Behoben |
| H16 | HIGH | Division-by-Zero Guards | Behoben |
| M11 | MEDIUM | KillOrphanedFfmpeg blind | Behoben (mit Logging) |

---

*Policy-Version: 1.0.0 | Erstellt am 11. April 2026 durch automatische Code-Analyse (Claude Opus 4.6)*
