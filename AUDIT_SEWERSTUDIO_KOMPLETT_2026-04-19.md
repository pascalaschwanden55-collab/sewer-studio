# SewerStudio — Komplett-Audit v4.1/4.2

**Stand:** 2026-04-19
**Branch:** `feature/pdf-import-beobachtungen`
**Autor:** Claude (Opus 4.7) im Auftrag von Pascal Aschwanden, Abwasser Uri
**Umfang:** Architektur · KI-Pipeline · Training · Wissensdatenbank · UI · Hardware · Stärken & Baustellen

---

## 0. Executive Summary

SewerStudio ist eine WPF-Desktop-Anwendung (.NET 10 / C#) für die automatisierte Auswertung von Kanalinspektions-Videos nach **EN 13508-2** und **VSA-KEK 2023 (Schweiz)**. Sie wurde entwickelt, um die Auswertung von ~3000 WinCan/IBAK-Videos beim Kanton Uri zu beschleunigen.

**Kernzahlen:**
- **4 Haupt-Projekte** (Domain / Application / Infrastructure / UI) + 2 Test-Suites
- **~25 DI-registrierte Services** in der UI-Schicht
- **Über 30 spezialisierte Fenster** für Datenverwaltung, Analyse, Tools
- **6 KI-Modelle** im Orchester (YOLO · DINO · SAM 2 · Qwen 8B · Qwen 32B · Nomic-Embed)
- **4 unterstützte PDF-Formate** (Fretz AG · KIT Bauinspekt · Abwasser Uri · IBAK)
- **Hardware-Budget:** ~25 GB permanent auf 32 GB RTX 5090, +3 GB SAM on-demand
- **Audit-Status:** Build grün (0 Warn/0 Err), keine Security-Findings, 2 Code-Review-Blocker heute gefixt

**Einschätzung:** Das System ist **produktiv einsetzbar**. Es gibt keine blockierenden Fehler, die Architektur ist sauber geschichtet und die KI-Pipeline ist vollständig. Die grössten offenen Baustellen sind (a) Batch-Pipeline-Deadlock, (b) Überrepräsentation roter Samples in der KB (~96%), (c) OSD-Erkennung variiert je nach Kamera.

---

## 1. Was macht SewerStudio?

### 1.1 Aufgabe
Ein Kanalbetreiber führt mit Kameraroboter (WinCan/IBAK) Inspektionsfahrten in Abwasserrohren durch. Dabei entstehen:
- **Video** (MPEG/MP4/AVI, 30–80 m Haltungslänge, ~5 min pro Haltung)
- **Protokoll** (PDF oder DB3) mit manuell codierten Schäden
- **Fotos** (pro auffällige Stelle)

SewerStudio übernimmt **vier Hauptaufgaben**:

| Aufgabe | Manuell heute | Mit SewerStudio |
|---------|---------------|-----------------|
| **Befunde codieren** | Inspektor schaut Video, codiert nach VSA | KI schlägt Code vor, Mensch bestätigt/korrigiert |
| **Mess-Werkzeuge** | Auge + Erfahrung | Pixel → mm Kalibrierung, geometrische Werkzeuge |
| **Sanierungs-Empfehlung** | Erfahrung + Katalog | Trainiertes Modell empfiehlt Massnahmen |
| **Kostenvoranschlag (Devis)** | Excel-Formeln | DevisGenerator: Schaden → Massnahme → Preis |

### 1.2 Standards
- **EN 13508-2** — europäischer Standard für Zustandsbeschreibung
- **VSA-KEK 2023** — Schweizer Kennzeichnung Erhaltung Kanalisation
- **SIA 405 / XTF** — Schweizer Datenaustausch-Format

### 1.3 Nicht-Ziele (explizit aus CLAUDE.md)
- **Kein Cloud-Service** — alles läuft lokal auf der Workstation
- **Kein kommerzielles Produkt** — Solo-Entwicklung, keine Multi-Tenancy
- **Kein Online-LLM** — Qwen läuft über Ollama, YOLO/DINO/SAM im lokalen Python-Sidecar

---

## 2. Architektur-Übersicht

### 2.1 Projekt-Schichten (Clean Architecture)

```
┌───────────────────────────────────────────────────┐
│  AuswertungPro.Next.UI  (WPF, ViewModels, KI)    │
│  ↳ ViewModels, Windows, Pages, AI-Orchestration  │
└─────────────────┬─────────────────────────────────┘
                  │ verweist auf
┌─────────────────▼─────────────────────────────────┐
│  AuswertungPro.Next.Infrastructure                │
│  ↳ SQLite, Import (PDF/XTF/WinCan/IBAK), Excel    │
└─────────────────┬─────────────────────────────────┘
                  │ verweist auf
┌─────────────────▼─────────────────────────────────┐
│  AuswertungPro.Next.Application                   │
│  ↳ Use Cases, Service-Schnittstellen              │
└─────────────────┬─────────────────────────────────┘
                  │ verweist auf
┌─────────────────▼─────────────────────────────────┐
│  AuswertungPro.Next.Domain (keine Dependencies)   │
│  ↳ Entities, Value Objects, Contracts             │
└───────────────────────────────────────────────────┘

Python-Sidecar (FastAPI, Port 8100)
  ↳ YOLO · DINO · SAM 2 · Florence-2 (Shadow)
  — wird parallel als eigener Prozess gestartet
```

**Regel:** Domain kennt niemanden. UI kennt alle. Keine zirkulären Referenzen.

### 2.2 Dependency Injection — [ServiceProvider.cs](src/AuswertungPro.Next.UI/ServiceProvider.cs)

Ein schlanker, selbstgeschriebener DI-Container (kein Microsoft-Extensions-Hosting nötig). Ca. **25 Haupt-Services** werden registriert, in logischen Kategorien:

| Kategorie | Beispiele |
|-----------|-----------|
| **Import/Export** | `PdfImportService`, `XtfImportService`, `WinCanDbImportService`, `IbakImportService`, `ExcelExportService` |
| **Daten-Layer** | `JsonProjectRepository`, `ProtocolService`, `PhotoImportService` |
| **KI-Pipeline** | `AiPlatformConfig`, `PipelineConfig`, `PythonSidecarService`, `OllamaProtocolAiService` |
| **KnowledgeBase** | `KnowledgeBaseContext`, `EmbeddingService`, `RetrievalService`, `KnowledgeMirrorService` (C:\KI_BRAIN ↔ E:\Brain) |
| **Inference** | `EnhancedVisionAnalysisService`, `MultiModelAnalysisService` |
| **Massnahmen/Devis** | `MeasureRecommendationService`, `DevisGenerator`, `DevisExcelExporter` |
| **Plausibilität** | `RuleBasedAiSuggestionPlausibilityService` |

**Start-Warmup** (lädt Modelle beim App-Start parallel vor):
1. `VisionModel` (qwen3-vl:8b-q8) → GPU permanent
2. `EmbedModel` (nomic-embed-text) → GPU (~0.6 GB)
3. `ReferenceModel` (qwen3-vl:32b) → RAM mit `num_gpu=10` (hybrid, ~9 s statt 28 s)

### 2.3 Hardware-Modus-Erkennung — [GpuModelSelector.cs](src/AuswertungPro.Next.UI/Ai/GpuModelSelector.cs)

Beim Start wird `nvidia-smi` ausgeführt und das passende Profil gewählt:

| VRAM | Profil | Parallele Qwen-Slots | DINO-Modus |
|------|--------|---------------------|------------|
| ≥ 24 GB | **Workstation** (RTX 5090) | 8B × 6 | permanent geladen |
| 12–24 GB | **Desktop** | 8B × 2 | on-demand |
| < 12 GB | **Minimal** | 8B × 1 | deaktiviert |
| kein GPU | **CPU-Fallback** | Qwen 32B RAM | keine Vision-Pipeline |

Alle Profile nutzen `num_ctx=8192` und `OLLAMA_FLASH_ATTENTION=1` für minimalen KV-Cache.

### 2.4 Knowledge-Speicher — `C:\KI_BRAIN`

Zentrales Verzeichnis (kein Projektordner mehr — Migration automatisch beim Start, siehe [KnowledgeRoot.cs](src/AuswertungPro.Next.UI/Ai/KnowledgeRoot.cs)):

```
C:\KI_BRAIN\
├── KnowledgeBase.db              (SQLite: Samples, Embeddings, Versions, ValidationLog)
├── training_samples.json         (manuell kuratierte Samples + .bak/.bak.2/.bak.3-Rotation)
├── training_settings.json        (Pipeline-Settings)
├── benchmark_metrics.json        (Zeitreihen, FIFO 50 Einträge)
├── benchmark_set.json            (Haltungen für Benchmark)
├── eval_set\                     (120-Frame Eval-Set, images/ + labels/)
├── frames\                       (extrahierte Video-Frames pro Haltung)
├── fewshot_images\               (Few-Shot-Beispiele für Qwen-Prompt)
└── measures-model.zip            (Trainiertes Massnahmen-ML-Modell)

Mirror (automatische Spiegelung): E:\Brain  ← via KnowledgeMirrorService
```

---

## 3. KI-Pipeline im Detail

### 3.1 Pipeline-Diagramm

```
Video (MPEG/MP4)
  ↓
VideoFrameStream  ── persistenter ffmpeg-Prozess
  │  fps=1/1.5 → alle 1.5 s ein Frame
  │  PNG via stdout, 64 KB Chunks
  ↓
FrameQualityFilter  ── Laplacian + Luminanz + dHash
  │  Schärfe ≥ 100, Helligkeit 30–240, Hamming ≤ 5
  │  ~5–15% werden verworfen (dunkel/unscharf/Duplikat)
  ↓
YOLO Pre-Screening  ── Sidecar, TensorRT FP16
  │  yolo26m.engine, confidence ≥ 0.25
  │  Batch-Endpunkt verfügbar, Class-Prefilter
  ↓  (nur auffällige Frames, normale übersprungen)
Grounding DINO 1.5  ── Sidecar, autocast + float16
  │  73 englische Labels: crack, root, corrosion, infiltration, ...
  │  box_threshold=0.25, text_threshold=0.20
  │  torch.compile + channels_last (Blackwell-Opt.)
  │  Shadow: Florence-2 alle 5 Frames (Daten-Sammlung)
  ↓  (Bounding Boxes pro Schadens-Region)
SAM 2 Segmentation  ── Sidecar, hiera_l
  │  min_score=0.50, Ring-Scan für Rohr-Querschnitt
  │  NMS zwischen Kandidaten, Annulus-Clip
  │  torch.compile aktiv
  ↓  (pixel-genaue Maske)
MaskQuantificationService  ── C#
  │  Pixel × PipeImageWidthRatio → mm
  │  Ausdehnung %, Position (Uhrlage)
  ↓
DetectionAggregator  ── C# State-Machine
  │  Meter-Merge-Radius 1.5 m, max_gap=5 Frames
  │  min_consecutive=3, Peak-Confidence-Tracking
  │  DedupWindowFrames=3
  ↓  (pro Schaden nur Peak-Frame an Qwen)
Qwen Vision 8B (Q8_0)  ── Ollama
  │  JSON-Schema erzwungen (format:<schema>)
  │  VSA-Code + Severity 1–5 + Uhrlage + Extent %
  │  deutscher Prompt mit VSA-Code-Liste
  ↓
QualityGateService  ── C#
  │  8 Signale gewichtet fusioniert: Composite Score [0..1]
  │  Green ≥ 0.75 + min. 2 Signale / Yellow ≥ 0.45 / Red sonst
  ↓
  ├─ Green → direkt in Ergebnis
  └─ Yellow/Red → Eskalation: Qwen 32B (RAM, num_gpu=10, ~9 s)
                  ↓
                  Re-Analyse mit grösserem Modell
  ↓
RawVideoDetection-Liste → Protokoll, PDF-Report, Excel-Export
```

### 3.2 Komponente für Komponente

#### 3.2.1 Video-Framestream
- **Datei:** `VideoFrameStream.cs:16-236`
- **Implementierung:** Persistenter ffmpeg-Prozess (kein Re-Spawn pro Frame)
- **Sicherheit:** 30 s Frame-Timeout, max. 3 aufeinanderfolgende Timeouts → Abort
- **Speicherlimit:** 50 MB Akkumulator-Puffer gegen ffmpeg-Hänger

#### 3.2.2 FrameQualityFilter
- **Datei:** `Ai/Training/Services/FrameQualityFilter.cs`
- **Kriterien:**
  - Schärfe: Laplacian-Varianz ≥ 100 (3×3 Kernel `[0,1,0 / 1,-4,1 / 0,1,0]`)
  - Helligkeit: Luminanz-Mittelwert 30–240 (`0.299R + 0.587G + 0.114B`)
  - Duplikat: dHash 9×8 Pixel, Hamming-Distanz ≤ 5
- **Thread-Safety:** alle Counter mit `Interlocked.Increment` (Fix aus April 2026)
- **`Reset()` wird jetzt beim Batch-Wechsel aufgerufen** (vorher: Hash-Carry-Over-Bug)

#### 3.2.3 YOLO (Sidecar)
- **Dateien:** `sidecar/routes/yolo.py`, `sidecar/models/yolo_wrapper.py`
- **Modell:** `yolo26m.engine` (TensorRT FP16) — alternativ `yolo26l-seg.pt` (Fallback)
- **Precision:** Standard FP16; optional FP4 auf RTX 5090 (NVFP4)
- **Pre-Filter** vor Inferenz (`_is_frame_usable`):
  - Zu dunkel (< 5), zu hell (> 245), zu wenig Kanten (std < 3), zu unscharf (Laplace < 3)
- **Batch-Endpunkt:** `/yolo/batch` — mehrere Bilder in einem Forward Pass

#### 3.2.4 Grounding DINO 1.5
- **Datei:** `sidecar/routes/dino.py`, `sidecar/models/dino_wrapper.py`
- **Modell:** Grounding DINO 1.5 (Config + Weights in `models/grounding_dino_1.5/`)
- **Labels:** 73 Open-Vocabulary-Begriffe in Englisch
- **Optimierungen:** `torch.cuda.amp.autocast()`, `channels_last` Memory-Format, `torch.compile(mode="reduce-overhead")`
- **Florence-2 Shadow:** parallel im ThreadPoolExecutor, alle 5 Frames, nur Datensammlung (noch nicht produktiv)

#### 3.2.5 SAM 2
- **Datei:** `sidecar/models/sam_wrapper.py`
- **Version:** **SAM 2** (nicht SAM 3 wie in manchen Docs behauptet)
- **Config:** Hydra `sam2.1_hiera_l.yaml`
- **Ring-Scan:** Sektorbasiertes Annulus-Abtasten für Rohr-Querschnitt-Segmentierung — Zentrum als Negative Constraint, um Rohr-Innenfläche auszuschliessen
- **Batch-Grösse:** dynamisch berechnet aus verfügbarem VRAM: `max(10, min(100, avail_gb × 15))`

#### 3.2.6 Qwen Vision
- **Dateien:** `Ai/EnhancedVisionAnalysisService.cs`, `Ai/OllamaClient.cs`, `Ai/Ollama/OllamaConfig.cs`
- **Modelle:**
  - `qwen3-vl:8b-q8` (Q8_0, ~8.5 GB VRAM, ~3 s Inferenz)
  - `qwen3-vl:32b` (Q4_K_M, hybrid RAM+GPU, `num_gpu=10`, ~9 s Inferenz)
- **Eskalations-Trigger** (in `EnhancedVisionAnalysisService`):
  - `allCodesNull` — kein Code erkannt
  - `severity ≥ 4` — schwerer Schaden
  - `poorQuality` — Bildqualität laut Qwen selbst schlecht
- **JSON-Schema** erzwungen via Ollama `format` Parameter → kein freier Text erlaubt
- **Prompts:** deutsch, kurzer & ausführlicher Prompt je nach Kontext (Nahaufnahme vs. Axialsicht)
- **Retry:** 3 Attempts, exponentielles Backoff (2/4/8 s)
- **Circuit Breaker:** 5 Failures in 60 s → 30 s Fast-Fail
- **Keep-Alive:** `24h` (Modell bleibt permanent geladen)

#### 3.2.7 Temporal-Tracking (kein ByteTrack!)
- **Datei:** `Ai/DetectionAggregator.cs`
- **Realität vs. Annahme:** Die Codebasis nutzt **keinen ByteTrack und kein OC-SORT**, sondern eine eigene Meter-basierte State-Machine:
  - Gleiche YOLO-Klasse + Meter innerhalb 1.5 m → gleiches Ereignis
  - Gap > 5 Frames → Ereignis geschlossen
  - min. 3 aufeinanderfolgende Frames für Validierung
  - Peak-Confidence-Frame wird an Qwen geschickt

#### 3.2.8 QualityGate
- **Datei:** `Ai/QualityGate/QualityGateService.cs`
- **8 Signale** (gewichtet, renormalisiert über verfügbare):
  1. `YoloConf` · 2. `DinoConf` · 3. `SamMaskStability` · 4. `QwenVisionConf`
  5. `LlmCodeConf` · 6. `KbSimilarity` · 7. `KbCodeAgreement` · 8. `PlausibilityScore`
- **Schwellen:**
  - **Green** — Composite ≥ 0.75 **UND** `≥ MinSignalsForGreen` (default 2)
  - **Yellow** — Composite ≥ 0.45
  - **Red** — sonst
- **CategoryWeights:** pro VSA-Code-Familie trainierbar (in SQLite-Tabelle)

#### 3.2.9 Embeddings
- **Datei:** `Ai/KnowledgeBase/EmbeddingService.cs`
- **Modell:** `nomic-embed-text` via Ollama `/api/embed`
- **Verwendung:** KB-Ähnlichkeit (Cosine-Similarity) für (a) Dedup, (b) Few-Shot-Retrieval, (c) Stale-Detection

---

## 4. Anwendungsfunktionen & UI

### 4.1 Hauptseiten (Sidebar-Navigation)

| Seite | Zweck |
|-------|-------|
| **ProjectPage** | Projekt neu/öffnen/speichern |
| **DataPage** | Haltungsdaten erfassen (Karten-Layout, redesigned 2026) |
| **SchächtePage** | Schacht-Objekte in Tabellenform |
| **ImportPage** | PDF- und XTF-Import mit Vorschau |
| **ExportPage** | Excel-Export (Haltungen.xlsx, Schächte.xlsx) |
| **VsaPage** | Zustandsklassifizierung (VSA-KEK Formeln) |
| **EigendevisPage** | Kostenrechner / Offertgenerierung |
| **DiagnosticsPage** | Fehler-Logs, Systemstatus |

### 4.2 Spezialisierte Fenster (Auswahl der wichtigsten)

| Fenster | Rolle |
|---------|-------|
| **TrainingCenterWindow** | Herzstück der KI-Arbeit: Batch-Import, Self-Training, KB-Indizierung, Review-Queue, Eval-Runner |
| **CodingModeWindow** | Live-Codier-Modus: Video abspielen + 7 Mess-Werkzeuge + KI-Ampel alle 5 s |
| **PhotoMeasurementWindow** | Geometrische Vermessung von Foto-Schäden (Kalibrierung + Pixel-zu-mm) |
| **VideoAnalysisPipelineWindow** | Pipeline-Dashboard — Frame für Frame live |
| **BenchmarkWindow** | F1/Precision/Recall-Dashboard mit Regressions-Alarm |
| **OfferCalculatorWindow** | Einzelne Haltung oder kombinierte Offerte |
| **VsaCodeExplorerWindow** | Katalog-Browser mit Suche/Filter |
| **BeobachtungenWindow** | Befunde-Editor (Haltungs-/Schachtbefunde) |
| **PlayerWindow** | Video-Viewer mit Frame-Navigation in 0.5 m Schritten |
| **MeasureTemplateEditorWindow** | 3D-Messmuster-Vorlagen definieren |
| **CombinedOfferWindow** | Multi-Haltungs-Offerte → PDF (Playwright/Chromium) |
| **ImportPreviewWindow** | Zeile-für-Zeile Validierung vor Speicherung |

### 4.3 Live-Coding-Werkzeuge

Im **CodingModeWindow** gibt es 7 Zeichen-Werkzeuge auf dem Video-Overlay:

| Icon | Werkzeug | Output → VSA-Code |
|------|----------|-------------------|
| ⊕ | **Kalibrierung** | Rohrdurchmesser → Skalierung |
| ━ | **Linie** (Riss) | Länge + Uhrlage → BAA/BAB/BAC |
| ⌒ | **Bogen** (Umfangsschaden) | Winkel + Umfang → BAF/BAG |
| ▢ | **Fläche** (Korrosion) | H × B in mm → BBA/BBB |
| ● | **Punkt** (Loch/Anschluss) | Uhrlage → BAJ/BDA |
| ↔ | **Strecke** | Meter-Bereich → BCB |
| ⚡ | **Live-KI** | Ampel Grün/Gelb/Rot alle 5 s |

Bei Gelb/Rot erscheint der **3-Button-Flow**: ✓ Übernehmen · ✎ Code ändern · ✗ Verwerfen.

### 4.4 UI-Theme
- **Light-Theme** (Standard), SCI-FI Engineering-Palette
- Accent `#2563EB` (Engineer-Blau) · Success `#16A34A` · Danger `#DC2626` · Warning `#F59E0B`
- Kartensystem mit `CornerRadius` 6–14 px, Glass-Overlays
- **Brushes:** als DynamicResource — einfacher Theme-Wechsel (Light/Dark) möglich

### 4.5 PowerShell-Hilfstools (Repo-Root)

| Skript | Zweck |
|--------|-------|
| `Start-KiMaximum5090.ps1` | Ollama-Tuning für RTX 5090 (6 Slots, 24 h Keep-Alive, Flash-Attention) |
| `HaltungenTool.ps1` | SIA 405 / XTF-Parsing + Datenaktualisierung |
| `AuswertungTool.ps1` | Batch-Auswertungen |

---

## 5. Video-Selbsttraining (V4.2 Highlight)

Das Selbsttraining ist das ambitionierteste KI-Feature: **Die Pipeline lernt aus vorhandenen Protokollen selbst.**

### 5.1 Ablauf pro Haltung

```
1. PDF-Protokoll laden → PdfProtocolTableParser oder PdfProtocolExtractor
   ↓ GroundTruthEntry-Liste (Meter, VSA-Code, Uhrlage, Foto-Pfad)

2. Aufnahmetechnik bewerten (EINMAL am ersten Frame)
   ↓ TechniqueAssessmentService → axial/nahaufnahme/schwenk/schacht

3. Pro Protokoll-Eintrag mit Foto:
   a) Frame laden
   b) BLINDE Qwen-Analyse (120 s Timeout)
   c) Vergleich mit Ground-Truth → ExactMatch/PartialMatch/Mismatch/NoFindings
   d) Auto-Approve wenn: Foto vorhanden + PartialMatch + CodeMatched + Score ≥ Schwelle
   e) Few-Shot-Speicherung bei ExactMatch (nicht für Grundgerüst BCD/BCE/BDA/BDB/BDC/AEC/AED/AEF)

4. Parallel-Analyse mit konfigurierbarer GPU-Concurrency
   ↓ ManualResetEventSlim für Pause/Resume/Cancel

5. Unsichere Samples → ReviewQueue (Active Learning)
```

### 5.2 PDF-Protokoll-Parser

Zwei komplementäre Services:

| Service | Zweck |
|---------|-------|
| `PdfProtocolTableParser` | Regex-basierter Tabellen-Parser, 4 Formate (Fretz · KIT · Uri · IBAK) |
| `PdfProtocolExtractor` | Höherwertig: Bildbericht-Blöcke + eingebettete Fotos extrahieren |

**Regex-Patterns** in `PdfProtocolTableParser.cs`:
- `MeterRegex` — Meter-Erkennung mit Negative-Lookahead gegen Datumswerte
- `CodeRegex` — VSA-Code-Pattern `B[A-Z]{2,5}[A-Z]?|AE[A-Z]{1,4}` (5–6 Zeichen, hierarchisch)
- `SchachtRegex` — Schacht-ID-Erkennung im Header
- `ClockRegex` — Uhrlagen-Phrasen ("bei 6 Uhr", "von 9 Uhr bis 3 Uhr")
- `OcrMeterZeroFixRegex` — OCR-Artefakt: "O.O" → "0.0"

**`KlartextToCode`-Dictionary** (~80 Einträge) mappt deutsche Klartext-Begriffe auf VSA-Codes:
- BC-Familie: "Rohranfang"→BCD · "Bogen"→BCC · "Anschluss"→BCA
- BA-Familie: "Längsriss"→BABA · "Riss"→BAB · "Korrosion"→BAFJ
- AE-Familie: "Inspektionsende"→AEF

**Fix heute (2026-04-19):** Die alte Lookup-Logik gab den **ersten** Treffer zurück, was bei "Rohrende mit Korrosion" zufällig BCE oder BAFJ ergeben konnte. Jetzt: **längster Key zuerst** (sortierter Cache `KlartextToCodeByLength`).

**Tool:** `pdftotext` wird als Subprozess aufgerufen (kein PdfPig/PDFSharp). `FfmpegLocator` sucht die Binary.

### 5.3 DifferenceAnalyzer — Greedy-Matching

- **Datei:** `Ai/Training/Services/DifferenceAnalyzer.cs`
- **Toleranz:** ±0.5 m
- **Algorithmus:**
  1. Für jeden Protokoll-Eintrag → Kandidaten-KI-Detektionen im Radius
  2. Bei keinem Kandidat → **TruePositive** (wenn Grundgerüst erwartet) oder **FalseNegative**
  3. Bei Kandidaten → höchster Score, wenn Code stimmt → **TruePositive**, sonst **Mismatch**
  4. `IsAssigned = true` → keine Doppelzuordnung

**Matching-Levels:**

| Level | Bedeutung |
|-------|-----------|
| **ExactMatch** | Code + Meter + Uhr stimmen |
| **PartialMatch** | Code OK, Meter/Uhr leicht ab |
| **Mismatch** | KI erkannte was, aber falscher Code |
| **NoFindings** | KI hat nichts gesehen |

### 5.4 KB-Anreicherung & Dedup

- **Auto-Approve-Schwelle:** Confidence ≥ 0.92 (`AutoApprovalService.cs`)
- **Dedup-Schwelle:** Cosine-Similarity > 0.85 → neues Sample ist "bereits abgedeckt" → skip
- **QualityGate-Filter:** nur Yellow + Green kommen in die KB (Red wird abgelehnt)

### 5.5 Review-Queue & Active Learning

- **Datei:** `Ai/SelfImproving/ReviewQueueService.cs`
- **Priorität:** `0.6 × UncertaintyScore + 0.4 × (1 − MatchConfidenceScore) + CategoryBoost`
- **3-Button-Flow:** Akzeptieren / Ablehnen / Korrigieren (direkt in KB-Status geschrieben)
- **UncertaintySamplingService** (V4.2): Top-N unsicherste Frames, Cool-Down gegen Nachbar-Duplikate

### 5.6 KB-Qualitätsdienst

- **Datei:** `Ai/SelfImproving/KbQualityService.cs`
- **Stale-Detection:** Samples, die ≥ 5× im ValidationLog und > 50% Fehlerrate auslösen → markiert
- **Coverage-Gaps:** VsaCode ≠ FinalCode häufen sich → fehlende Abdeckung

---

## 6. Qualitätsmessung

### 6.1 BenchmarkRunner
- **Datei:** `Ai/Training/BenchmarkRunner.cs`
- **Inhalt:** Vorab definierte Haltungen mit bekanntem Protokoll → Pipeline läuft komplett → F1/Precision/Recall **pro VSA-Code-Hauptgruppe**
- **Speicher:** `benchmark_metrics.json` (FIFO 50 Einträge)
- **UI:** `BenchmarkWindow.xaml` mit Regressions-Alarm

### 6.2 EvalRunnerService (neu, V4.2, Commit 6efa65ee)
- **120-Frame Eval-Set** in `C:\KI_BRAIN\eval_set\images\` + `labels\` (YOLO-Format)
- Pro Frame: Qwen-Analyse → Top-Finding-Code → Metriken pro VSA-Code
- CSV-Output: `Timestamp,Code,F1,Precision,Recall,TP,FN,FP,GitCommit`
- Damit: Regressions-Tracking nach jedem Commit

---

## 7. Hardware-Budgets (RTX 5090, 32 GB, gemessen)

```
Permanent Resident (~24.8 GB GPU)
├── Qwen 8B × 4 Slots (Q8_0, num_ctx=8192, Flash-Attention)  ~16.0 GB
├── YOLO 26m-seg TensorRT FP16 (yolo26m.engine)               ~2.5 GB
├── Grounding DINO 1.5                                        ~2.0 GB
├── nomic-embed-text                                          ~1.0 GB
└── Overhead (PyTorch, Kernels, Caches)                       ~3.3 GB

On-Demand
├── SAM 2 (hiera_l)                                           ~3.0 GB
└── Florence-2 Shadow (optional)                              ~3.5 GB

Reserve: ~4.2 GB

Eskalation (kurzfristig)
└── Qwen 32B (Q4_K_M, hybrid RAM+GPU, num_gpu=10)             ~8.0 GB GPU + 14.8 GB RAM
```

**Konfig-Env-Vars:**
- `OLLAMA_NUM_PARALLEL=6`
- `OLLAMA_FLASH_ATTENTION=1`
- `OLLAMA_NUM_CTX=8192`
- `SEWERSTUDIO_SIDECAR_URL=http://localhost:8100`

---

## 8. Stärken

| Bereich | Stärke |
|---------|--------|
| **Architektur** | Saubere Schichtung, keine zirkulären Referenzen, klare Verantwortlichkeiten |
| **Hardware-Optimierung** | TensorRT FP16 für YOLO, autocast+channels_last für DINO, torch.compile für SAM 2 |
| **Ausfall-Robustheit** | Polly-Retry + Circuit-Breaker in OllamaClient, 50 MB Puffer-Limit, 30 s Timeouts |
| **Thread-Safety** | FrameQualityFilter mit Interlocked, SemaphoreSlim für Modell-Wechsel |
| **Qualitäts-Fusion** | 8-Signal-QualityGate mit renormalisierten Gewichten |
| **Offline-First** | Keine Cloud, keine externen API-Calls; nur localhost |
| **Selbst-Lern-Loop** | Protokoll → Vergleich → Auto-Approve → KB → bessere Erkennung |
| **Robuste Persistenz** | Backup-Rotation (.bak/.bak.2/.bak.3), Atomic-Save, Disk-Space-Guard |
| **Diagnose-Infrastruktur** | ocr_debug.log, ocr_dumps/, _diag_assignment.txt, Florence-2 Shadow-Log |

---

## 9. Aktuelle Baustellen

### 9.1 Kritisch (bekannt aus Memory + Reviews)
| # | Problem | Status |
|---|---------|--------|
| **B1** | **Batch-Pipeline-Deadlock** — parallele Qwen-Requests mit 6 Slots hängen. Aktuell deaktiviert. | offen, architektonisch, nicht Hardware |
| **B2** | **96% Red-Samples in KB** — Review-Queue muss abgearbeitet werden für mehr Green-Trainingsdaten | manuelle Arbeit |

### 9.2 Wichtig (aus heutigem Code-Review)
| # | Finding | Datei |
|---|---------|-------|
| W1 | `pdftotext`-Subprozess: `stderrTask` wird nicht awaited (Handle-Leak im Timeout-Pfad) | PdfProtocolTableParser.cs, PdfProtocolExtractor.cs |
| W2 | `Thread.Sleep` in async `MoveWithRetry` blockiert ThreadPool | TrainingSamplesStore.cs:318 |
| W3 | Silent-Catch (`catch { }`) in PDF-Parsern ohne Logging | mehrere Stellen |
| W4 | Wortgrenzen im `"Plan"`-Dateifilter — filtert auch `Haltungsplan_*.pdf` heraus | BatchSelfTrainingOrchestrator.cs |

### 9.3 Bekannte Audit-Punkte (aus 04/2026)
| # | Thema | Status |
|---|-------|--------|
| N9 | QualityGate: 1 Signal kann Green erzeugen (kein Min-Count auf Category-Ebene) | teilweise gefixt (`MinSignalsForGreen=2` global) |
| N5 | `QuickScanService` ohne FrameQualityFilter | offen |
| N2 | `NormalizeClock` ohne englische Begriffe (top/bottom/left/right) | offen |
| N8 | `PipeImageWidthRatio` hardcoded 0.70 — Messungen ±15% ungenau | offen |
| — | OSD-Erkennung kameraabhängig | offen |
| — | Neue PDF-Formate (neue Firmen) brauchen neuen Parser | Erweiterungs-Pattern etabliert |

### 9.4 Heute gefixt
| # | Fix | Datei |
|---|-----|-------|
| ✓ | `TryMapKlartext` längster Key zuerst (Cache) | `PdfProtocolTableParser.cs:962-988` |
| ✓ | Regex-Timeout 2 s gegen Catastrophic-Backtracking | `PdfProtocolExtractor.cs:86-89` |

---

## 10. Empfehlungen (priorisiert)

### 10.1 Sofort (< 1 h Aufwand)
1. **W1 (stderr-Leak)** in `pdftotext`-Subprozess-Aufrufen: `await stderrTask.ConfigureAwait(false)` nach `WaitForExit` ergänzen
2. **W3 (Silent-Catch)** in PDF-Parsern durch `Debug.WriteLine(ex)` ergänzen — Bug-Hunting später erheblich einfacher

### 10.2 Kurzfristig (halber Tag)
3. **W2 (`Thread.Sleep` in async)** auf `await Task.Delay` umstellen — verhindert ThreadPool-Blockaden bei parallelem Batch-Import
4. **B2 (Red-Samples)** systematisch abarbeiten: 1 h/Tag Review-Queue → binnen 2 Wochen balancierte KB
5. **N2 (englische Clock-Begriffe)** ergänzen: `top/bottom/left/right → 12/6/9/3`

### 10.3 Mittelfristig (1–2 Tage)
6. **B1 (Batch-Deadlock)** untersuchen: Producer-Consumer-Muster mit `Channel<T>`, oder gezieltes Profiling mit `dotnet-dump`
7. **N8 (PipeImageWidthRatio)** aus Kalibrierungsdaten ableiten statt hardcoden — `PhotoMeasurementService` hat dafür die Daten
8. **OSD-Erkennung** pro Kamera-Profil kalibrieren: `OsdProfile.cs` mit bekannten Kamera-Modellen (IBAK, CUES, iPEK, Rausch)

### 10.4 Langfristig (Wochen)
9. **DINO-Batch-Inferenz** im Sidecar: aktuell sequenziell, Ultralytics-ähnlicher Batch möglich
10. **ViewType-YOLO** produktiv: 89% Accuracy erreicht, Nahaufnahme-Klasse noch ergänzen
11. **Stufe 2 Roadmap** (aus Memory: Operateur setzt BCD/BCE + Stammdaten, KI codiert den Rest) — Ablauf-Lerner fertig, jetzt Integration im CodingModeWindow

---

## 11. Glossar

| Begriff | Bedeutung |
|---------|-----------|
| **Haltung** | Kanalabschnitt zwischen zwei Schächten (meist 30–80 m) |
| **OSD** | On-Screen Display im Video — zeigt Meter, Zeit, Haltungsname |
| **VSA-KEK** | Schweizer Kennzeichnungs-Standard für Zustandsprotokolle |
| **EN 13508-2** | Europäischer Standard für Kanal-Zustandsbeschreibung |
| **Uhrlage** | Position am Rohr-Querschnitt: 12 Uhr = Scheitel, 6 Uhr = Sohle |
| **Streckenschaden** | Schaden über Strecke (Meter-Start bis Meter-Ende), z.B. Korrosion |
| **Punktschaden** | Schaden an einer Stelle (z.B. Riss, Anschluss) |
| **Severity 1–5** | 1=optisch, 2=leicht, 3=mittel, 4=schwer, 5=kritisch |
| **Few-Shot** | Beispiel-Bilder, die dem Qwen-Prompt mitgegeben werden |
| **Ground-Truth** | Verifizierte Wahrheit (aus Protokoll), gegen die KI gemessen wird |
| **QualityGate** | Ampel-Logik Green/Yellow/Red aus 8 Signalen |
| **Eskalation** | 8B hat Yellow/Red → 32B-Modell analysiert erneut |
| **ByteTrack / OC-SORT** | Tracking-Algorithmen (in SewerStudio NICHT verwendet — eigene Meter-State-Machine) |
| **ALP** | Active Learning Pipeline (Review-Queue mit Unsicherheit) |

---

## 12. Anhang: Commit-Historie (letzte 5)

```
6efa65ee  V4.2: Eval-Runner für automatisierte Qualitätsmessung
f82a44c7  V4.2: Fehleranalyse-Fixes (kritisch)
d904fb37  V4.2: Review-Queue 3-Button-Flow (Akzeptieren/Ablehnen/Korrigieren)
be201d43  V4.2: Protokoll-First + Active Learning + DINOv2-Infrastruktur
ec66099b  Gesamtaudit: KI-Pipeline Training + Analyse + Sidecar + Daten
```

---

**Abschluss-Einschätzung:** SewerStudio ist ein **ingenieurmässig sauber gebautes, produktiv einsetzbares System** für einen klar umrissenen Use-Case. Die grössten Risiken liegen in (a) der noch nicht balancierten Trainingsdaten-Verteilung (96% Red) und (b) dem noch nicht aktivierten Batch-Pipeline-Modus. Beide sind adressierbar ohne Refactoring. Die Architektur unterstützt die Roadmap zu Stufe-2-Autonomie (Operateur codiert BCD/BCE, KI den Rest) sehr gut.

— *Audit erstellt am 2026-04-19 durch Claude Opus 4.7 unter Anwendung der Skills `sewer-pipeline-auditor`, `sewer-architektur`, `sewer-fachwissen`, `ki-kanalinspektion` und gezielter Explorer-Agents.*
