# SewerStudio KI-Pipeline — Gesamtaudit

**Stand:** 16. April 2026
**Hardware:** RTX 5090 (32 GB VRAM), 64 GB DDR5, Core Ultra 9 285K
**Zweck:** Vollstaendige Dokumentation aller KI-Pfade fuer Review und Optimierung

---

## 1. Architektur-Ueberblick

```
┌─────────────────────────────────────────────────────────────────┐
│                    SEWERSTUDIO V4.1                              │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ CODIER-MODUS │  │  NACHTBATCH  │  │  TRAINING CENTER     │  │
│  │ (Live, 8s)   │  │ (Offline)    │  │  (Profile, Eval-Set) │  │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬───────────┘  │
│         │                  │                      │              │
│         ▼                  ▼                      ▼              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              SIDECAR (Python, Port 8100)                  │   │
│  │  YOLO26l-seg │ Grounding DINO │ SAM 2.1 │ Florence-2    │   │
│  └──────────────────────────────────────────────────────────┘   │
│         │                  │                      │              │
│         ▼                  ▼                      ▼              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              OLLAMA (Port 11434)                           │   │
│  │  Qwen3-VL 8B (Q8_0) │ Qwen3-VL 32B (hybrid) │ nomic    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              KI_BRAIN (C:\KI_BRAIN)                       │   │
│  │  KnowledgeBase.db │ training_frames │ eval_set │ models  │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. VRAM-Belegung (gemessen)

| Modell | VRAM | Rolle | Permanent? |
|--------|------|-------|------------|
| Qwen3-VL 8B Q8_0 | 11.7 GB | Schadensklassifikation | Ja (keep_alive=-1) |
| YOLO26l-seg (TensorRT) | ~1.5 GB | Detection + Segmentation | Ja |
| Grounding DINO 1.5 | ~1.5 GB | Open-Vocabulary Detection | Ja |
| SAM 2.1 Hiera Large | ~0.7 GB | Segmentierung | Ja |
| nomic-embed-text | 0.6 GB | KB-Embeddings | Ja |
| System/Overhead | ~4.9 GB | CUDA Context, Windows | - |
| **TOTAL** | **~21 GB** | | **~11 GB frei** |

---

## 3. Codier-Modus (Live-Analyse)

### 3.1 Ablauf

```
User klickt "Analysieren" oder Auto-Timer (8s)
  │
  ├─ CaptureSnapshotAsync() → PNG-Bytes vom VLC-Player
  │
  ├─ YOLO26l-seg via Sidecar (2ms)
  │   ├─ Detektionen gefunden?
  │   │   ├─ JA: Kandidaten-Tracking (Tiefe vs. Nah)
  │   │   │   ├─ Box-Mitte nah am Fluchtpunkt + klein → TIEFE (Vorschau, grau)
  │   │   │   └─ Box am Bildrand oder gross → NAH (Befund, farbig)
  │   │   │       ├─ SAM segmentiert die Box
  │   │   │       ├─ Befundliste rechts aktualisiert
  │   │   │       └─ Pausenmodus: Video pausiert
  │   │   │
  │   │   └─ War der Fund vorher ein Tiefen-Kandidat?
  │   │       └─ JA → "Kandidat bestaetigt" (von Tiefe zu Nah)
  │   │
  │   └─ NEIN: Qwen-Fallback
  │       ├─ EnhancedVisionAnalysisService.AnalyzeAsync()
  │       ├─ Prompt: DamageClassesPrompt (300 Woerter, kurz)
  │       ├─ Few-Shot: DEAKTIVIERT (verschlechtert Erkennung!)
  │       ├─ Schema: JSON-Zwang (meter, findings, view_type)
  │       └─ ShowCodingAiResults()
  │
  └─ Status-Anzeige aktualisieren
```

### 3.2 Dateien und Zeilennummern

| Datei | Methode | Zeilen | Funktion |
|-------|---------|--------|----------|
| PlayerWindow.xaml.cs | InitCodingAi | 6439-6508 | Services initialisieren |
| PlayerWindow.xaml.cs | RunCodingAnalysisAsync | 6788-6912 | Hauptanalyse-Pfad |
| PlayerWindow.xaml.cs | ShowCodingAiResults | 7765-7857 | UI + Events |
| PlayerWindow.xaml.cs | CodingLiveAiTimer_Tick | 8457-8475 | Auto-Timer (8s) |
| PlayerWindow.xaml.cs | ClockPositionToBox | 7376-7406 | Uhrlage → SAM-Box |
| SingleFrameMultiModelService.cs | AnalyzeFrameAsync | 49-157 | YOLO→DINO→SAM |
| SingleFrameMultiModelService.cs | IsNearCenter | ~240 | Tiefenfilter YOLO |
| SingleFrameMultiModelService.cs | IsInsidePipeCircle | 167-216 | Tiefenfilter DINO |
| EnhancedVisionAnalysisService.cs | AnalyzeAsync | 369-393 | Qwen-Analyse |
| EnhancedVisionAnalysisService.cs | MapToAnalysis | 650-778 | ViewType-Filter |

### 3.3 Bekannte Probleme Codier-Modus

| # | Problem | Schwere | Status |
|---|---------|---------|--------|
| C1 | YOLO26l-seg erkennt auf vielen Frames nichts (25.5% mAP) | HOCH | Bekannt, Training laeuft |
| C2 | Qwen 8B kollabiert auf BCC bei offener Klassifikation | HOCH | Prompt gekuerzt, Few-Shot deaktiviert |
| C3 | SAM-Box aus Uhrlage ist ungenau | MITTEL | ClockPositionToBox eingebaut |
| C4 | ViewType-Klassifikator nur 89% genau | MITTEL | Soft-Filter statt Hard-Delete |
| C5 | OSD-Meter wird manchmal falsch gelesen | MITTEL | Ruecksprung-Schutz eingebaut |

---

## 4. Nachtbatch (Offline-Training)

### 4.1 Ablauf

```
Training Center → "Batch-Nachtbetrieb" starten
  │
  ├─ Ordner mit Haltungen waehlen (Video + PDF)
  │
  ├─ BatchSelfTrainingOrchestrator.RunAsync()
  │   │
  │   ├─ VORBEREITUNG
  │   │   ├─ Alle PDFs parallel vorladen (CPU, 4 Worker)
  │   │   ├─ Haltungen entdecken (Video + PDF Matching)
  │   │   └─ Deduplizierung (batch_processed.txt)
  │   │
  │   └─ PRO HALTUNG (sequentiell oder parallel):
  │       │
  │       ├─ PHASE 1: Protokoll laden
  │       │   └─ PDF parsen → GroundTruth-Eintraege
  │       │
  │       ├─ PHASE 2: Video-Blindanalyse
  │       │   └─ VideoSelfTrainingOrchestrator.RunAsync()
  │       │       │
  │       │       ├─ Protokoll → GroundTruth Mapping
  │       │       ├─ OSD-Timeline + Frame-Mapping
  │       │       │
  │       │       └─ BatchPipelineService.AnalyzeVideoAsync()
  │       │           │
  │       │           ├─ Phase A: Frame-Extraktion (ffmpeg, 2s Intervall)
  │       │           │
  │       │           ├─ Phase B: YOLO Batch-Screening
  │       │           │   ├─ POST /detect/yolo/batch (6 Frames pro Batch)
  │       │           │   ├─ Filter: IsRelevant + BCD/BCE-Zonen + Sweep
  │       │           │   └─ Ergebnis: relevante Frames
  │       │           │
  │       │           ├─ Phase C: DINO + SAM (pro relevantem Frame)
  │       │           │   ├─ POST /detect/dino → 0 Detektionen (!)
  │       │           │   └─ POST /segment/sam → nur wenn DINO Boxen hat
  │       │           │
  │       │           └─ Phase D: Qwen-Analyse (×3 parallel)
  │       │               ├─ YOLO-Skip Check: hasYoloFindings?
  │       │               │   ├─ JA → YOLO-Findings direkt nutzen
  │       │               │   └─ NEIN → Qwen AnalyzeAsync (3s/Frame)
  │       │               │
  │       │               └─ AKTUELL: Fast immer Qwen-Fallback
  │       │                   (YOLO liefert detections=0)
  │       │
  │       ├─ PHASE 3: Differenzanalyse
  │       │   └─ DifferenceAnalyzer: KI vs. Operateur
  │       │       ├─ TruePositive (Code + Meter stimmen)
  │       │       ├─ FalseNegative (Operateur hat, KI nicht)
  │       │       ├─ FalsePositive (KI hat, Operateur nicht)
  │       │       └─ CodeMismatch (beide haben, Code unterschiedlich)
  │       │
  │       ├─ PHASE 4: KB-Anreicherung
  │       │   └─ KbEnrichmentService.AutoEnrichFromReportAsync()
  │       │       ├─ ApproveMatches = true (keine manuelle Pruefung!)
  │       │       └─ KB wird automatisch gefuellt
  │       │
  │       └─ PHASE 5: YOLO-Trainingskandidaten
  │           └─ YoloTrainingDataGenerator.SaveCandidates()
  │               ├─ GREEN: TP + hohe Confidence → direkt trainierbar
  │               ├─ YELLOW: Konflikte → manuell pruefen
  │               ├─ RED: Unbrauchbar → verworfen
  │               └─ Negativ: FP-Frames → leere Labels
  │
  └─ POST-BATCH
      ├─ YOLO Auto-Retrain: PAUSIERT (autoRetrainEnabled = false)
      │   Grund: 98.9% Labels nicht manuell verifiziert
      └─ Kennzahlen-Log pro Haltung:
          YOLO-only / Qwen-Fallback / Zero-Detections / Not-Relevant
```

### 4.2 Dateien und Zeilennummern

| Datei | Methode | Zeilen | Funktion |
|-------|---------|--------|----------|
| TrainingCenterWindow.xaml.cs | StartBatchNightRun_Click | 617-760 | Batch starten |
| BatchSelfTrainingOrchestrator.cs | RunAsync | 78-456 | Hauptloop |
| BatchSelfTrainingOrchestrator.cs | ProcessSingleHaltungAsync | 459-683 | Pro Haltung |
| VideoSelfTrainingOrchestrator.cs | RunAsync | 54-240 | Video-Analyse |
| BatchPipelineService.cs | AnalyzeVideoAsync | 55-320 | YOLO→DINO→SAM→Qwen |
| MultiModelAnalysisService.cs | AnalyzeVideoAsync | 100-950 | Alternativer Pfad |
| DifferenceAnalyzer.cs | Analyze | - | KI vs. Operateur |
| KbEnrichmentService.cs | AutoEnrichFromReportAsync | - | KB fuellen |
| YoloTrainingDataGenerator.cs | SaveCandidates | - | Green/Yellow/Red |

### 4.3 Bekannte Probleme Nachtbatch

| # | Problem | Schwere | Status |
|---|---------|---------|--------|
| N1 | YOLO liefert detections=0 → Qwen pro Frame | HOCH | Bekannt, YOLO braucht bessere Daten |
| N2 | DINO findet bei Kanalbildern 0 Detektionen | HOCH | Bekannt, YOLO-Fallback eingebaut |
| N3 | 98.9% Labels nicht manuell verifiziert | HOCH | Auto-Retrain pausiert |
| N4 | Qwen pro Frame = 28 Min/Haltung | HOCH | Wird durch besseres YOLO geloest |
| N5 | boxAreaNorm hardcoded auf 0.3 | MITTEL | Sollte aus BBox berechnet werden |
| N6 | FP-Frames als "green" Negative | MITTEL | Sollte "yellow" sein |
| N7 | KB-Anreicherung ohne manuelle Pruefung | MITTEL | Policy konfigurierbar |

---

## 5. Qwen-Analyse (EnhancedVisionAnalysisService)

### 5.1 Zwei Prompts

| Prompt | Woerter | Nutzen | Wo |
|--------|---------|--------|----|
| DamageClassesPrompt (kurz) | ~300 | Codier-Modus | Zeile 80-106 |
| DamageClassesPromptFull (lang) | ~1500 | Nachtbatch | Zeile 109-583 |

### 5.2 Kurzer Prompt (Codier-Modus)

```
Kanalinspektion-Frame analysieren. Erkenne ALLE sichtbaren Befunde.
JEDER Befund kommt in findings[] mit: label (VSA-Code), severity (1-5).
view_type ist NUR: axial/nahaufnahme/schwenk/schacht. KEIN VSA-Code!

BEFUNDE (severity=1): BCD, BCE, BCC, BCA
SCHAEDEN (severity 2-5): BAB, BAC, BAF, BAJ, BAI, BAA, BBA, BBB, BBC, BBF

WICHTIG: label = VSA-Code, KEIN Freitext.
```

### 5.3 Filter-Kette nach Qwen

```
Qwen-Rohantwort (DTO)
  │
  ├─ Rohoutput-Logging (LastRawOutput) ← NEU
  │
  ├─ MapToAnalysis()
  │   ├─ Code-Extraktion (VSA-Code aus Label)
  │   ├─ ViewType normalisieren
  │   ├─ ViewType Soft-Filter:
  │   │   ├─ nahaufnahme/schwenk → Severity abstufen (nicht loeschen!)
  │   │   └─ schacht → nur BCD/BCE/BDB durchlassen
  │   └─ Suppressed-Logging (LastSuppressedLog) ← NEU
  │
  ├─ Post-Filter-Logging (LastFilterLog) ← NEU
  │
  └─ EnhancedFrameAnalysis (gefiltert)
```

### 5.4 Bekannte Probleme Qwen

| # | Problem | Schwere | Status |
|---|---------|---------|--------|
| Q1 | Kollabiert auf BCC bei offener Klassifikation | HOCH | Nur fuer Eskalation nutzen |
| Q2 | Few-Shot-Bilder verschlechtern Erkennung | HOCH | DEAKTIVIERT |
| Q3 | 3s Latenz pro Frame | MITTEL | Akzeptabel fuer Eskalation |
| Q4 | Steckt VSA-Codes in view_type statt findings | MITTEL | Prompt-Fix eingebaut |

---

## 6. Sidecar (Python FastAPI)

### 6.1 Modelle und Endpoints

| Endpoint | Modell | Latenz | Funktion |
|----------|--------|--------|----------|
| POST /detect/yolo | yolo26l-seg (TensorRT) | 2ms | Detection + Segmentation |
| POST /detect/yolo/batch | yolo26l-seg | 2ms×N | Batch-Detection |
| POST /classify/yolo | yolo26m (cls) | 1ms | Pre-Screening |
| POST /classify/viewtype | viewtype_v2 | 0.2ms | Aufnahmetechnik |
| POST /detect/dino | Grounding DINO 1.5 | 200ms | Open-Vocab (funktioniert nicht!) |
| POST /segment/sam | SAM 2.1 Large | 50ms | Segmentierung |
| POST /analyze/pipe-axis | Custom | 5ms | Rohrkreis-Erkennung |
| POST /model/reload | - | - | YOLO Hot-Swap |

### 6.2 Bekannte Probleme Sidecar

| # | Problem | Schwere | Status |
|---|---------|---------|--------|
| S1 | DINO findet 0 Detektionen bei Kanalbildern | HOCH | Bekannt, YOLO-Fallback |
| S2 | ViewType-Modell Pfad hardcoded | MITTEL | Funktioniert nur lokal |
| S3 | Florence-2 Shadow Trainingspaare nicht persistiert | NIEDRIG | Shadow laeuft aber Daten verloren |

---

## 7. Training-Daten Pipeline

### 7.1 Datenquellen

| Quelle | Anzahl | Qualitaet | Nutzen |
|--------|--------|-----------|--------|
| KnowledgeBase total | 9187 | 88% Red (ungeprueft) | Nur Green nutzen |
| KnowledgeBase Green | 722 | Vom System geprueft | Few-Shot, Eval-Basis |
| TeacherAnnotation | 6 | Manuell (beste Qualitaet) | Eval-Set Kern |
| Manuell korrigiert | 98 | Human-in-Loop | Eval-Set Basis |
| YOLO-seg Dataset | 828 | Ganz-Bild-Polygone (schlecht) | Nachtrainieren |
| WinCan DB3 Exporte | 84+ | Operateur-Codierungen (Ground Truth) | Profil-Extraktion |
| Trainings-Frames | 2412 | Aus Operateur-Videos extrahiert | YOLO-Training |

### 7.2 Inspektions-Profil-Extraktion

```
WinCan DB3 (Operateur hat codiert)
  │
  ├─ InspectionProfileExtractor
  │   └─ Pro Haltung: Zeitlinie aller Codierungen
  │       ├─ Events (Code + Meter + Zeitstempel)
  │       ├─ Segmente (axial_fahrt / stillstand / schacht)
  │       ├─ Luecken (Leerstrecken)
  │       └─ QualityFlags (fehlende BCD, monotonie, etc.)
  │
  ├─ InspectionPatternAggregator
  │   └─ Statistische Muster ueber alle Haltungen:
  │       ├─ Fahrgeschwindigkeit: 0.055 m/s (Median)
  │       ├─ Codierungen/Meter: 0.30
  │       ├─ BCD immer erst: 100%
  │       ├─ Uebergangsmatrix (Code→Code)
  │       └─ Aufnahmetechnik-Muster
  │
  ├─ InspectionFrameExtractor
  │   └─ Frames bei codierten Zeitpunkten extrahieren:
  │       ├─ 3 Frames pro Event (t-2s, t, t+2s)
  │       ├─ Negativ-Frames aus Leersegmenten
  │       └─ Aufnahmetechnik-Frames (Schacht, Axial)
  │
  └─ EvalSetGenerator
      └─ 120 eingefrorene Pruefungs-Frames:
          ├─ 60: Top-5 haeufigste Codes
          ├─ 30: Verwechslungspaare
          └─ 30: Negativbeispiele
```

### 7.3 YOLO-Trainingskandidaten (Nachtbatch Phase 5)

```
Pro Haltung nach Differenzanalyse:
  │
  ├─ TP (KI + Operateur stimmen)
  │   └─ Confidence >= 65% → GREEN
  │   └─ Confidence >= 50% + Nachbar-Frame → GREEN
  │   └─ Confidence >= 30% → YELLOW
  │
  ├─ FP (KI falsch)
  │   └─ Als Negativ-Beispiel → GREEN (leeres Label)
  │
  ├─ CodeMismatch (beide haben, Code verschieden)
  │   └─ Konflikt-Paar? → YELLOW (Review)
  │   └─ Sonst → RED
  │
  └─ FN (Operateur hat, KI nicht)
      └─ Kein YOLO-Output → RED (nicht trainierbar ohne Box)
```

---

## 8. Bekannte Konflikte und Architektur-Schulden

### 8.1 DINO ist tot aber noch geladen

DINO belegt 1.5 GB VRAM, liefert bei Kanalbildern 0 Detektionen. Wird im Codier-Modus nicht mehr genutzt, im Nachtbatch als Fallback behalten (liefert aber nichts).

**Empfehlung:** DINO entladen, 1.5 GB VRAM freigeben.

### 8.2 Zwei YOLO-Modelle mit verschiedenen Rollen

- **yolo26m:** Pre-Screener (relevant/irrelevant) — funktioniert
- **yolo26l-seg:** Detektor (19 Klassen) — 25.5% mAP, zu schwach fuer Skip

Diese Rollen sind nicht klar getrennt. Der Sidecar laedt nur EIN Modell.

**Empfehlung:** Konzeptionelle Trennung: Pre-Screener + Detektor als zwei Modelle.

### 8.3 BatchPipelineService vs. MultiModelAnalysisService

Zwei Pipeline-Services die aehnliches tun aber verschiedene Pfade nehmen:
- **BatchPipelineService:** YOLO Batch → DINO → SAM → Qwen (Nachtbatch)
- **MultiModelAnalysisService:** YOLO Stream → DINO → SAM → Aggregation → Qwen (Video)

Aenderungen an einem greifen nicht beim anderen.

**Empfehlung:** Einen Service, konfigurierbar fuer Batch vs. Stream.

### 8.4 Confirmation Bias im Datensatz

98.9% der Labels kommen vom Modell selbst (BatchImport, Auto-Approve). Nur 6 echte TeacherAnnotations, 98 manuell korrigiert.

**Massnahme:** Auto-Retrain pausiert bis Eval-Set steht.

---

## 9. Aktuelle Prioritaeten

| Prio | Was | Warum | Aufwand |
|------|-----|-------|---------|
| 1 | Eval-Set erstellen (120 Frames manuell) | Ohne Eval kein zuverlaessiges Training | 1 Woche |
| 2 | YOLO26l-seg mit echten BBoxen trainieren | Aktuell nur Ganz-Bild-Polygone | 1-2 Tage |
| 3 | DINO entladen (1.5 GB VRAM sparen) | Liefert nichts, belegt Speicher | 1 Stunde |
| 4 | Pipeline-Services vereinheitlichen | Zwei Services = doppelte Bugs | 1-2 Tage |
| 5 | Qwen auf Q4 oder 32B hybrid umstellen | VRAM sparen oder bessere Eskalation | 1 Stunde |

---

## 10. Modellversionen (exakt)

| Komponente | Modell | Version | Groesse | VRAM |
|------------|--------|---------|---------|------|
| Qwen (Primary) | qwen3-vl:8b-q8 | Q8_0, 8192 ctx | 9.8 GB disk | 11.7 GB |
| Qwen (Eskalation) | qwen3-vl:32b | Q4_K_M, hybrid | 20.9 GB disk | ~10 GB GPU |
| YOLO (Sidecar) | yolo26l-seg | 28.0M fused | 63 MB | ~1.5 GB |
| YOLO (Pre-Screen) | yolo26m | 21.9M fused | 40 MB | ~0.5 GB |
| SAM | sam2.1_hiera_l | SAM 2.1 Large | 857 MB | ~0.7 GB |
| DINO | groundingdino_swint_ogc | DINO 1.5 | 662 MB | ~1.5 GB |
| ViewType | viewtype_v2/best.pt | YOLOv8n-cls, 89% | 3 MB | <0.1 GB |
| Embeddings | nomic-embed-text | Latest | 274 MB | 0.6 GB |
