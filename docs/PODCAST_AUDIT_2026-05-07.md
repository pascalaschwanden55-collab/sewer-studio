# SewerStudio — Vollstaendiges Programm-Audit fuer Podcast

**Datum:** 2026-05-07
**Branch:** `feature/pdf-import-beobachtungen`
**Build:** gruen, 0 Warnungen, 0 Fehler
**Tests:** 773 gruen (140 Infrastructure + 633 Pipeline + UI/Common)
**Code-Umfang:** ~159.000 Zeilen C#, verteilt auf 595 Dateien
**Hardware-Annahme:** Intel Core Ultra 9 285K · ASUS RTX 5090 32 GB · 64 GB DDR5

---

## 1. Was ist SewerStudio ueberhaupt?

SewerStudio ist eine Windows-Desktop-Anwendung fuer die **automatisierte
Auswertung von Kanal-TV-Inspektionen**. Der typische Anwender ist ein
Kanalinspekteur oder ein Buero, das Inspektionsvideos von WinCan, IBAK
oder Ikas-IBSK uebernimmt und daraus EN-13508-2- und VSA-KEK-konforme
Schadensprotokolle erstellt.

Das Besondere: Die ganze KI laeuft **lokal auf der Workstation**, nicht in
der Cloud. Das ist datenschutzrechtlich und betrieblich relevant — ein
Kanalvideo zeigt unter Umstaenden Wohngebaeude, Strassenzuege oder
Industrie-Areale. Lokale Verarbeitung bedeutet: kein Upload, keine
Drittfirma sieht das Material, keine Internet-Abhaengigkeit.

Konkret kann das Programm:

- Inspektionsvideos abspielen (VLC-basiert)
- aus dem Video automatisch Schaeden erkennen und mit VSA-KEK-Codes versehen
- Bestandsdaten aus WinCan, IBAK, KIAS, XTF/SIA405 und PDFs importieren
- Stammdaten und Inspektionsergebnisse in einem Projekt verwalten
- Protokolle als druckfertige PDFs ausgeben
- Sanierungsempfehlungen generieren und als Devis (Offerte) exportieren
- aus jeder durchgefuehrten Inspektion lernen (Self-Training, KnowledgeBase)
- den eigenen KI-Stand pruefen, warten und bereinigen

Es ist kein Prototyp mehr. Es ist ein ernstes Fachprogramm mit ~4-stelliger
Datei-Zahl, einer eigenen KnowledgeBase mit ueber 21.000 gelernten Samples,
einem Test-Set von 773 automatisierten Tests und einer kompletten lokalen
KI-Pipeline aus fuenf Modellen.

---

## 2. Architektur — wie ist das Programm aufgebaut?

### 2.1 Schichten-Modell

Das Programm folgt einer klassischen Vier-Schichten-Architektur:

```
┌──────────────────────────────────────────────────────────┐
│  UI       (75.463 Zeilen)  — WPF, ViewModels, Pages       │
│  Infra    (44.726 Zeilen)  — Imports, Reports, KI-Wrapper │
│  Application (22.394 Zeilen) — Service-Vertraege, DTOs    │
│  Domain   ( 2.356 Zeilen)  — Reine Modelle, kein I/O      │
└──────────────────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────┐
│  Sidecar (Python FastAPI, Port 8100)                       │
│  YOLO26l-seg · Grounding DINO · SAM 2.1 · Florence-2      │
└──────────────────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────┐
│  Ollama (Port 11434)                                       │
│  Qwen3-VL 8B Q8 · Qwen3-VL 32B hybrid · nomic-embed       │
└──────────────────────────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────┐
│  KI_BRAIN (C:\KI_BRAIN)                                    │
│  KnowledgeBase.db · training_frames · eval_set · Modelle  │
│  ca. 113 GB, davon ~67 GB Frames                           │
└──────────────────────────────────────────────────────────┘
```

**Was bedeuten die Schichten in einfachen Worten?**

- **Domain** ist das Fachvokabular. Hier liegen reine Datentypen wie
  *Haltung*, *Schacht*, *VsaFinding*, *ProtocolEntry*. Keine Datenbank,
  keine Datei, keine UI — nur Begriffe.
- **Application** definiert *was* das Programm tut. Die Service-Vertraege
  (Interfaces), die Pipeline-DTOs, die Trainings-Modelle.
- **Infrastructure** macht das *wie*: hier liegen die echten Importer
  (WinCan-DB3, IBAK-Daten.txt), die PDF-Generatoren, die KI-Wrapper, die
  SQLite-Knowledge-Base, die Ollama-HTTP-Calls.
- **UI** ist die WPF-Oberflaeche mit ViewModels, Pages und Windows.
- **Sidecar** ist ein eigener Python-Prozess auf Port 8100, der die
  schweren KI-Modelle haelt, die in C# nicht komfortabel zu bekommen
  waeren (YOLO als TensorRT-Engine, SAM 2.1, Grounding DINO).

### 2.2 Dependency Injection — die Verkabelung

Beim App-Start wird in `App.xaml.cs` ein vollstaendiger DI-Container
aufgebaut (Microsoft.Extensions.DependencyInjection). Vorher gab es nur
einen handgeschriebenen `ServiceProvider`. Heute werden alle Services —
KnowledgeRoot, AppDataPath, KnowledgeMirrorNotifier, SidecarAuthToken,
OllamaConfig, AiRuntimeConfig, PipelineConfig, KnowledgeBasePath,
ImagePixelDecoder, OcrPdfFallback — ueber **Provider/Bridge-Pattern**
registriert. So koennen Application-Services WPF-spezifische Funktionen
nutzen (z.B. PNG-Dekodierung), ohne selber an WPF zu haengen.

Das ist die Grundlage dafuer, dass das System spaeter auch headless
(ohne UI, z.B. als Server) laufen koennen wird.

### 2.3 Wichtige Hauptklassen

| Klasse | Rolle |
|--------|-------|
| `MultiModelAnalysisService` | GPU-State-Automat, orchestriert YOLO/DINO/SAM/Qwen pro Frame |
| `EnhancedVisionAnalysisService` | Qwen-Wrapper inkl. Eskalation 8B → 32B |
| `BatchPipelineService` | Batch-Pipeline mit Frame-Persistierung |
| `VideoAnalysisPipelineService` | Video-End-to-End-Flow im Player |
| `DetectionAggregator` | Temporal Voting (Frame-uebergreifend) |
| `QualityGateService` | Green/Yellow/Red, lernt Per-Code-Gewichte |
| `OllamaClient` | HTTP-Client mit Polly Retry + Circuit Breaker |
| `PythonSidecarService` | Sidecar-Lifecycle inkl. Graceful Shutdown |
| `KnowledgeBaseManager` | KB-Indexierung + Embedding-Schreiben |
| `RetrievalService` | KB-Aehnlichkeitssuche fuer Few-Shot-Vorlagen |
| `BatchSelfTrainingOrchestrator` | Nachtbatch fuer ganze Ordner |
| `ProtocolPdfExporter` | EN 13508-2 PDF-Export |
| `HoldingFolderDistributor` | Verteilt Roh-Inspektionsordner auf Haltungen |

---

## 3. Die KI-Pipeline — die fuenf Modelle und wie sie zusammenarbeiten

### 3.1 Welche Modelle sind im Spiel?

| Modell | Rolle | VRAM | Quelle | Permanent? |
|--------|-------|------|--------|------------|
| **YOLO26l-seg** (TensorRT FP16) | Detektion + Segmentierung von 10 Schadensklassen | ~1.5 GB | Custom-Training auf D:\yolo_sewer_v1 (3254 train + 809 val) | ja |
| **Qwen3-VL 8B Q8_0** | Schadensklassifikation, Texterklaerung, JSON-Output | ~11.7 GB | Ollama, keep_alive permanent | ja |
| **Qwen3-VL 32B (hybrid)** | Eskalation bei unsicheren Faellen, num_gpu=10 | ~2 GB GPU + ~20 GB RAM | Ollama, hybrid CPU/GPU | ja (RAM, kein VRAM-Konflikt) |
| **Grounding DINO 1.5** | Open-Vocabulary-Detection (Text → Boxen) | ~1.5 GB | Sidecar, **lazy-load** seit V4.2 | nein (nur on-demand) |
| **SAM 2.1 Hiera-L** | Pixel-genaue Segmentierung (Box → Maske) | ~0.7 GB | Sidecar, models/sam2 | ja |
| **Florence-2** | Shadow-Lernen parallel zu DINO | klein | Sidecar | lazy |
| **nomic-embed-text** | KB-Embeddings fuer Aehnlichkeitssuche | ~0.6 GB | Ollama, F16 | ja |

**VRAM-Bilanz im Soll-Stand:** YOLO 1.5 + SAM 0.7 + Qwen-8B 11.7 + 32B-hybrid 2
+ nomic 0.6 ≈ ~16.5 GB. Frei bleiben ~14 GB fuer DINO (wenn geladen) und
Puffer.

### 3.2 Wann macht welches Modell was?

Die Pipeline kennt vier Zustaende:

1. **DETECT** — YOLO laeuft, ByteTrack/OC-SORT verfolgt Objekte
   ueber Frames hinweg. Sehr schnell (~30 ms pro Frame).
2. **SEGMENT** — YOLO + SAM. Sobald YOLO eine Box hat, segmentiert SAM
   das Pixel-genau. Damit sind quantitative Aussagen wie "Riss-Laenge"
   oder "Versatz-Hoehe in mm" moeglich.
3. **CLASSIFY** — YOLO + Qwen 8B. YOLO-Boxen werden zusammen mit dem
   Frame-Bild an Qwen geschickt. Qwen liefert JSON: VSA-Code, Uhrlage,
   Severity, Begruendung.
4. **ESCALATE** — wenn die 8B-Antwort unsicher ist, Severity hoch oder
   alle Codes leer, wird **Qwen3-VL 32B** angefragt. Dieses Modell
   laeuft hybrid (10 Layer GPU + Rest RAM) und braucht ca. 9 Sekunden
   pro Frame statt 28 Sekunden bei reinem CPU-Modus.

Schwellenwerte (in `EnhancedVisionAnalysisService`):
- Eskalation, wenn alle Codes null
- Eskalation, wenn Severity ≥ 4
- Eskalation bei "poor quality"-Marker

Ein `SemaphoreSlim(1)` schuetzt davor, dass parallele 32B-Anfragen sich
gegenseitig kanibalisieren.

### 3.3 Codier-Modus — Live-Analyse waehrend des Schauens

Der Inspekteur sieht das Video, drueckt **"Analysieren"** oder laesst den
Auto-Timer alle 8 Sekunden laufen. Was passiert?

```
Klick / Auto-Timer
   ▼
Snapshot vom VLC-Player (PNG-Bytes)
   ▼
YOLO via Sidecar (~2 ms)
   ├─ Funde → Tiefen-Filter (nahe am Fluchtpunkt = grau, "weit weg")
   │           Funde nahe am Bildrand oder gross = Befund
   │   ▼
   │   SAM segmentiert die Box, Befundliste rechts aktualisiert,
   │   Video pausiert, VSA-Code-Picker oeffnet sich
   │
   └─ keine YOLO-Funde → Qwen-Fallback (8B, 2-3 s)
       ├─ Prompt: DamageClassesPrompt
       ├─ Schema: strict JSON (meter, findings, view_type)
       └─ Ergebnis als Befund-Vorschlag im UI
```

Wichtige Datei-/Methoden-Punkte (Stand vor PlayerWindow-Refactor war
PlayerWindow.xaml.cs, heute liegt der Code in `PlayerWindow.CodingMode.cs`
und `PlayerWindow.LiveDetection.cs`):

- `SingleFrameMultiModelService.AnalyzeFrameAsync` — der orchestrierende
  Pfad pro Frame
- `EnhancedVisionAnalysisService.AnalyzeAsync` — der Qwen-Aufruf
- `ClockPositionToBox` — Uhrlage (z.B. "3:00") wird in eine SAM-Box
  umgerechnet, damit der Inspekteur nur einen Punkt antippen muss

### 3.4 Nachtbatch — das Programm arbeitet, waehrend du schlaefst

Der wahre Hebel des Systems ist die **Batch-Pipeline**. Du gibst einen
Ordner mit Inspektionsdaten an (Videos + WinCan-DB3 + Operateur-PDFs),
das System arbeitet ueber Nacht durch:

```
Pro Haltung:
  Phase 1: Protokoll laden (PDF / DB3 / Daten.txt) → GroundTruth-Eintraege
  Phase 2: Video-Blindanalyse
            ├─ ffmpeg extrahiert Frames im 2-Sekunden-Raster
            ├─ YOLO Batch-Screening (6 Frames pro Request)
            ├─ DINO + SAM bei relevanten Frames
            └─ Qwen-Analyse parallel x3
  Phase 3: Differenzanalyse — KI-Funde gegen Operateur-Protokoll
            ├─ True Positive (Code + Meter stimmen)
            ├─ False Negative (Operateur ja, KI nein)
            ├─ False Positive (KI ja, Operateur nein)
            └─ Code Mismatch (beide ja, aber unterschiedlich)
  Phase 4: KB-Anreicherung (KbEnrichmentService)
            └─ Samples landen in der KnowledgeBase
  Phase 5: YOLO-Trainingskandidaten (Green/Yellow/Red)
```

Das ist genau die Schleife, mit der das Modell Woche fuer Woche besser
wird — der Mensch korrigiert, das System lernt aus den Korrekturen.

### 3.5 KnowledgeBase — das langfristige Gehirn

Die KnowledgeBase ist eine SQLite-DB mit:

- 21.794 Samples (Bilder + Vektor-Embeddings + Codes + Metadaten)
- WAL-Journal, busy_timeout, Foreign Keys
- Embedding via `nomic-embed-text` ueber Ollama
- Aehnlichkeitssuche via `RetrievalService` fuer Few-Shot-Vorlagen
- Versions-Snapshots, die spaeter geprunt werden koennen
- TrainingRuns-Tabelle (Provenance: welche Run hat dieses Sample erzeugt)
- CategoryWeights-Tabelle (Per-Code-Gewichte aus echtem Feedback gelernt)

Drei Schwerpunkte:
1. **Indexierung** vor dem Schreiben pruefen (Dedup) — `KbDeduplicationService`
2. **Brain-Mirror** auf Zweit-Platte mit SHA256-Manifest — fail-closed bei
   Mismatch
3. **Wartung** ueber den neuen Diagnose-Tab (Frame-Cleanup, Versions-Pruning)

### 3.6 Quality Gate — Green / Yellow / Red

Jeder KI-Vorschlag wird durch ein Quality Gate geschickt. Es kombiniert:
- Modell-Confidence
- Aehnlichkeit zu KB-Samples
- Frame-Qualitaet (Schaerfe, Belichtung, Bewegungsunschaerfe)
- Per-Code-Gewichte aus CategoryWeights
- View-Type (Axial vs. Nahaufnahme)

Ergebnis ist eine Ampel:
- **Green** = nimmt das System mit hohem Vertrauen
- **Yellow** = Vorschlag, Mensch sollte pruefen
- **Red** = unsicher / widerspruechlich, nicht uebernehmen

Aktueller KB-Stand laut Schlussanalyse 2026-05-06:
- 11 % Green, 51 % Yellow, 38 % Red
- ValidationLog-Trefferquote: 52 %

Das ist das wichtigste Signal des ganzen Audits: Das System ist als
**Assistenz** wertvoll, aber noch nicht reif fuer **autonome** Codierung.

---

## 4. Funktionsumfang — was kann das Programm wirklich?

Das ist die Liste fuer den Podcast — was sieht der Anwender, wenn er
SewerStudio startet.

### 4.1 Sichtbare Seiten in der App

| Seite | Inhalt |
|-------|--------|
| **Overview** | Projekt-Dashboard, Anzahl Haltungen, Status |
| **Project** | Projektverwaltung, Anlegen / Oeffnen / Speichern |
| **Import** | WinCan / IBAK / KINS / XTF / PDF / Foto-Imports |
| **Builder** | Stammdaten-Erstellung, Schacht- und Haltungs-Anlage |
| **Data** | Tabellarische Haltungsuebersicht mit Bearbeitung |
| **Schaechte** | Schachtverwaltung |
| **Vsa** | VSA-Zustandsbewertung |
| **Eigendevis** | Devis/Offerten-Generator (optional, im Expertenmodus) |
| **MediaConflicts** | Konflikte zwischen Video-Zuordnungen aufloesen |
| **Export** | Excel- / PDF- / Reports-Export |
| **Diagnostics** | Sidecar-Health, Brain-Mirror-Health, Frame-Cleanup, Versions-Pruning, Logs |
| **Settings** | Konfiguration, Pfade, Expertenmodus-Toggle |

### 4.2 Wichtige Fenster (Windows)

- **PlayerWindow** — der zentrale Video-Player mit 16 Partials:
  Live-Detection, Codiermodus, Mark-Tool, Trainings-Modus, Heatmap,
  DamageMarkers auf dem Slider, OSD-Meter-Reader.
- **CodingModeWindow** — eigenstaendige Codieransicht.
- **PhotoMeasurementWindow** — Foto-Vermessung mit Kalibrierung
  (z.B. fuer Bend / Lateral / DN-Schaetzung aus Bildern).
- **TrainingCenterWindow** — der Maschinenraum: Batch-Nachtbetrieb,
  Profil-Verwaltung, Eval-Set, Goldstandard-Haltungen.
- **VideoAnalysisPipelineWindow** — End-to-End-Pipeline auf einem Video.
- **VideoTrainingReviewWindow** — Review der Trainings-Faelle.
- **BenchmarkWindow** — Benchmark-Lauf mit Regressions-Check.
- **CodeCatalogEditorWindow** — VSA-Katalog editieren.
- **CatalogManager / CatalogSelector** — Katalog-Verwaltung.
- **SanierungsmassnahmenWindow** + Rules-Window — Sanierungsregeln, Editor.
- **HydraulikPanelWindow** — hydraulische Berechnung (optional).
- **DossierPrintDialog / PrintOptionsDialog** — Druck-Steuerung.
- **MeasureSelectionWindow** + MeasureTemplateEditorWindow — Messprofile.
- **PriceCatalogEditorWindow** — Preiskatalog fuer Devis.
- **ImageAnnotationWindow** — Bild-Annotationen.
- **VsaCodeExplorerWindow** — VSA-Code-Baum durchsuchen.
- **ImportPreviewWindow** — Importvorschau.
- **MediaSearchWindow** — Mediensuche.
- **LiveFrameWindow** — Einzel-Frame mit Uhrlage-Ring.
- **ObservationCatalogWindow** — Beobachtungskatalog.
- **StartupSplashWindow** — Splash mit 3D Fibonacci-Neural-Sphere.
- **FloatingGridWindow** — Schwebender Daten-Grid.

### 4.3 Was kann importiert werden?

| Format | Quelle | Was wird gelesen |
|--------|--------|------------------|
| WinCan DB3 | WinCanVX | Stammdaten + Protokoll-Eintraege |
| IBAK Daten.txt | IBAK Panoramo / IKAS | Haltungsdaten + Inspektionseintraege |
| KINS kiDVDaten.txt | KINS-System | Haltungsdaten |
| XTF / SIA405 | Schweizer Standard | Haltungs- und VSA-Findings |
| PDF (Fretz, KIT, Abwasser Uri, IBAK direkt) | gedruckte Protokolle | Tabellenparsing inkl. Caesar-Decode bei IBAK IKAS |
| Foto-Ordner | beliebige Quellen | Photo-Import mit Geolokalisierung |

### 4.4 Was kann exportiert werden?

- EN-13508-2-konformes PDF-Protokoll
- Excel-Mappe mit Haltungsdaten
- Devis / Offerte (Excel)
- Haltungs-Dossier (PDF mit allen Befunden)

### 4.5 Welche KI-Workflows gibt es?

1. **Live-Codierung** im PlayerWindow — Mensch schaut, KI assistiert
2. **Batch-Nachtbetrieb** — Ordner einlesen, ueber Nacht komplett analysieren
3. **Quick-Scan** — Video-Heatmap pro 5-Sekunden-Segment auf dem Slider
4. **Live-Detection** — Erkennungs-Ringsektoren ueber dem laufenden Video
5. **Self-Training** — Operateur korrigiert KI, KI lernt
6. **Active Learning** — System schlaegt vor, welche Samples manuell
   gepruefte werden sollten
7. **Benchmark** — Goldstandard-Haltungen werden gemessen (Regressions-Check)

---

## 5. Wo wir herkamen — die kurze Geschichte

Dieser Abschnitt ist der spannende Teil fuer den Podcast: Wo war das
Programm vor dem Audit, und wo ist es jetzt?

### 5.1 V4.0 — Funktion da, Architektur kaputt

- 89 % der KI-Logik lebten im UI-Projekt. Headless-Betrieb unmoeglich.
- `PlayerWindow.xaml.cs` war ein 5.370-Zeilen-Monolith ("God Class").
- Sicherheit: 8 `Process.Start`-Aufrufe waren anfaellig fuer
  Command-Injection. Pfade aus Projekten wurden ohne
  Containment-Check verwendet.
- Stabilitaet: HttpClient-Leak in der Feedback-Pipeline, keine
  Locks auf JSONL-Schreibstellen, async-void-Timer ohne Closed-Guard.
- Das XML-Parsing war anfaellig fuer XXE-Angriffe (DTD nicht deaktiviert).
- ServiceProvider war ein handgeschriebenes Service-Locator-Pattern,
  keine echte DI.
- Keine Brain-Mirror-Verifikation, kein Frame-Cleanup, kein Maintenance-Layer.

### 5.2 Audit-Start 2026-04-19 bis 2026-05-07 — die intensiven 2.5 Wochen

In rund **255 Commits** (alle auf dem Branch
`feature/pdf-import-beobachtungen`) wurde die Codebase planmaessig
durchgehaertet. Hier sind die wichtigsten Bloecke:

#### Phase 0: Stabilisierung der Basis (2026-04-26 bis 2026-04-30)

- LibVLC.Dispose deaktiviert — eine native AccessViolation crashte die App.
- Window-Lifecycle-Guard + Direkt-Crash-Logger eingebaut.
- PlayerWindow.Closed-Handler darf nie die App killen.
- Doppel-ESC als Notbremse im Codiermodus.
- Pipe-Deadlock im Process-Start: zentraler `ProcessRunner` (ArgumentList,
  asynchroner Pipe-Drain, Tree-Kill bei Timeout).
- Path-Containment in der UI: `TryResolveStoredPath`, `ResolveExistingPath`,
  `ResolveDossierPhotoPath`.

#### Phase 1: KIAS-Stammdaten + reversibles Apply (2026-05-03)

- `KiasStammdatenAggregator`, `XTF-` und `FDB-Reader`, `IbakPdfStammdatenExtractor`
- DN-Pattern-Kaskade fuer Nicht-KIAS-Protokolle
- Stammdaten-Aggregation und reversibler Apply (Plan v2 Schritt 2)

#### Phase 2: KnowledgeBase + Datenhygiene (2026-05-04)

- KB Foreign Keys + ModelVersion + defensive Migration
- `KnowledgeBaseWriter` mit zentralen Robustheits-PRAGMAs
- 285 Files raus aus dem Git-Index (~6.8 GB Repo-Entlastung)
- `KbIngestionPipeline` (Channel-basiert) + Tests
- `RehabilitationRulesEngine`: Hardcode-Dictionaries auf JSON umgestellt

#### Phase 3: UI-Konsistenz (2026-05-04 bis 2026-05-05)

- App-Manifest: PerMonitorV2 DPI Awareness + longPathAware
- Spacing/Typography-Tokens, benannte DataGrid-Style-Varianten
- PageHeader + StatusBadge UserControls in alle Pages migriert
- FontSize-Token-Migration in 41 weiteren Files
- Splash-Animation: 3D Fibonacci-Neural-Sphere

#### Phase 4: Robustheit + ffmpeg-Konsolidierung (2026-05-04)

- Empty-catch-Komplett-Sweep
- `IDialogService.ShowMessage` ersetzt 99 MessageBox-Calls
- ffmpeg-Konsolidierung auf zentralen `FfmpegLocator`
- TrainingRuns + RunId — KB-Provenance-Tracking
- Sanierungs-Decision-Log (jede Empfehlung wird nachvollziehbar)

#### Phase 5: DI-Container + KI-Schicht aus UI ziehen (2026-05-05 bis 2026-05-07)

Das ist die wichtigste Architektur-Phase.

- 5.1: Microsoft.Extensions.DependencyInjection eingebaut, alle Aufrufer
  Schritt fuer Schritt umgestellt, der alte ServiceProvider geloescht.
- 5.2: ServiceProvider in Module zerlegt (AiPipelineModule,
  KnowledgeBaseModule, VsaCatalogResolver-Helper).
- 5.3: KI-Schicht migriert. Konkret: 76 Files aus UI nach
  Application/Infrastructure verschoben. UI/Ai schrumpfte von 107 auf
  22 Files. KI-Files in Application stiegen von 12 auf 93, in Infrastructure
  von 3 auf 48.
- 5.4: Expertenmodus erweitert um Diagnose-Page.
- 5.5: Sanierungs-Decision-Log mit Provenance.

#### Phase 6: PlayerWindow zerlegen (2026-05-05 bis 2026-05-07)

Die spektakulaerste Einzelarbeit:

| Stand | Zeilen |
|-------|-------:|
| 2026-05-04 | 5.370 (God Class) |
| 2026-05-05 (nach Cluster A-D) | ~4.500 |
| 2026-05-06 (nach CodingMode-Subs) | ~3.500 |
| 2026-05-07 (vorletzter Tag) | 2.681 |
| 2026-05-07 (Stand jetzt) | **842** (-84 %) |

Aufgeteilt auf 16 thematische Partials: Helpers, VideoPlayback, Hotkeys,
Snapshot, LiveDetection, Feedback, TrainingMode, CodingMode, DamageMarkers,
Heatmap, MarkTool, CodingTool, CodingOverlayRender, CodingApply,
CodingEvents, ImportProtocol, Eingabemarker, MaskTriage.

#### Phase Audit-Sweep (HIGH-Befunde) — 2026-05-06 bis 2026-05-07

- SEC-H1..H3: Command-Injection in 8 Stellen geschlossen
- SEC-H4: Path-Traversal in `SanitizePathSegment` haerten
- SEC-H5: Sidecar-Bearer-Token-Auth fuer alle administrativen Endpoints
- SEC-C1: UI-Pfade durchgaengig gegen Projekt-Root gepinnt
- L4: Zentraler `SafeXmlLoader` mit XXE-Schutz
- STAB-H1: HttpClient-Leak in Feedback-Pipeline
- STAB-H2/H3: SemaphoreSlim auf JSONL-Append-Stellen
- STAB-H4: Sidecar Graceful-Shutdown via `/shutdown`-Endpoint
- STAB-H5: async-void-Timer mit Closed-Guard
- STAB-H6: Exception-Swallowing durch Debug-Log ersetzt
- ARCH-H1: Tech-Debt-Markierung in Domain-Models
  (HaltungRecord/SchachtRecordViewModel-Wrapper als Phase 1)
- ARCH-H2: `IBatchSelfTrainingOrchestrator`-Interface
- ARCH-H3: Interfaces fuer QualityGateService + DetectionAggregator
- ARCH-H5: MultiModelAnalysisService-Helpers + Filter + ProtocolMerger als
  Partial extrahiert (von 2.185 auf 1.547 LOC)

#### Phase Audit-Tab + Wartung (2026-05-07)

Im Diagnose-Tab sind drei neue Self-Service-Tools sichtbar:

- **Brain-Mirror Health** — Green/Yellow/Red mit Bytes + Alter,
  erkennt Mismatch, fehlenden Mirror, abgelaufene Mirror-Stand.
- **Frame-Cleanup** — DryRun und Loeschen orphaner PNGs aus
  `C:\KI_BRAIN\frames`. Frames juenger als 7 Tage geschuetzt.
  Fail-closed wenn aktive Sample-IDs leer sind.
- **KB-Versionen aufraeumen** — letzte 20 + alles juenger als 30 Tage
  bleibt; aktuelle Version wird nie geloescht.

#### Sprints 1-3 — 2026-05-07 (heute, der letzte Push)

- **Sprint 1**: Robustheit + Konsistenz + erste Architektur-Refactors
- **Sprint 2**: Pipeline-Telemetry SQLite + Repo-Hygiene Schritt 1
- **Sprint 2 Wiring**: SQLite-Telemetrie + JSONL produktiv aktiviert
- **Sprint 3**: UI-Optik-Update (Spacing-Grid + Brush-Konsolidierung +
  Tester-Banner)

### 5.3 Test-Coverage — die Zahl die nicht luegt

| Stand | Tests |
|-------|------:|
| Audit-Start 2026-04-19 | 654 |
| 2026-05-07 morgens | 704 |
| 2026-05-07 abends | **773** (+18 % vs. Audit-Start) |

Neu hinzugekommen: 25 ProjectPathResolver, 10 ProcessRunner, 5
SafeXmlLoader, 6 ActiveLearningSelector (mit Bug-Fix unterwegs),
5 FrameStoreCleanupService, 6 KnowledgeMirrorHealth, 6
HaltungRecordViewModel, 5 TaskExtensions, 19 PdfProtocolHelpers,
2 TrainingCaseJsonRoundtrip, 39 weitere Pure-Utility-Tests.

---

## 6. Wohin wir gehen — Roadmap 30 / 60 / 90 Tage

Basis ist `docs/INTENSIV_AUDIT_STANDORTBESTIMMUNG_2026-05-07.md`.

### 6.1 Naechste 7 Tage

1. Externe Tests mit `TEST_BRIEFING_2026-05-07.md` starten.
2. Active-Learning-Routine festlegen: 100 unsichere Samples pro Woche labeln.
3. Tester sollen jede KI-Entscheidung als "Assistenzvorschlag" bewerten,
   nicht als Wahrheit.
4. Hotspots besonders testen: Codiermodus, Batch-SelfTraining, PDF-Import,
   Live-Detection, Quick-Scan.
5. Lokalen Stand committen oder als Sprint-1-Arbeitsstand markieren.

### 6.2 Naechste 30 Tage

1. **`HoldingFolderDistributor`** refactoren (4.691 LOC) — der letzte
   klare ARCH-CRITICAL-Hotspot. Aufteilen in Parser, Matcher, PdfOutputWriter,
   SchachtDistributor, HaltungsDistributor.
2. PipelineTelemetry nach SQLite schreiben (heute nur JSONL).
3. Sidecar-Contracttests fuer Health, Analyse, Auth, Timeout, Fehlerantworten.
4. UI-Smoke-Test fuer Start, Diagnose-Tab, Training Center, Coding Mode.
5. Repo bereinigen: Benchmarkframes, Modellartefakte und `.pyc` aus Git
   entfernen. Repo-Pack ist aktuell ~338 MB.

### 6.3 Naechste 60 Tage

1. CategoryWeights aktiv nutzen (heute aktiviert, aber leer).
2. TrainingRuns bei jedem echten Training/Export schreiben.
3. Review Queue / Curation als gefuehrten Wochenprozess ausbauen.
4. KB-Metriken als Dashboard anzeigen: Green/Yellow/Red, Accuracy, Klasse
   mit hoechstem Risiko, Drift.
5. Restore-Drill fuer Brain-Mirror dokumentieren und testen.

### 6.4 Naechste 90 Tage

1. Produktiv-Gate fuer KI definieren und messen.
2. Mindestens 500 manuell validierte Faelle sammeln.
3. Langlauf: mehrstuendiger Batch mit Sidecar-Restart, App-Close, DB-Lock,
   Mirror-Ausfall.
4. UI-Ergonomie mit echten Testern beobachten und Hotspots vereinfachen.
5. Release-Kandidaten nur noch mit Testmatrix, KB-Qualitaetsreport und
   bekannten Limitationen freigeben.

### 6.5 Langfristige Vision (Stufenmodell)

Aus dem `ki-codier-vision`-Skill:

- **Stufe 1 — Assistent (heute):** Mensch codiert, KI schlaegt vor.
- **Stufe 2 — Operateur setzt nur BCD/BCE, KI macht den Rest:** Mensch
  setzt Anfang/Ende der Haltung, alle Schadens-Codes kommen von der KI
  und werden im Block freigegeben.
- **Stufe 3 — Vollautonom mit Human-in-the-Loop:** Nur Edge-Cases gehen
  in die Review Queue, alles andere laeuft durch.

Voraussetzungen fuer Stufe 2: > 30 % Green-Samples,
> 75 % ValidationLog-Accuracy, > 500 manuell validierte Faelle,
< 20 % Red-Anteil, klare Ampel pro Code.

---

## 7. Qualitaet — wo stehen wir wirklich? (Notenskala 1-6)

Direkt aus `docs/INTENSIV_AUDIT_STANDORTBESTIMMUNG_2026-05-07.md`:

| Bereich | Note | Status |
|---------|----:|--------|
| Gesamtprodukt | 2.8 | Gut testfaehig |
| KI-Erkennungsqualitaet | **4.0** | groesstes fachliches Risiko |
| KI-Pipeline-Engineering | 2.6 | stark verbessert |
| KnowledgeBase / Brain | 3.2 | gross, aber unreif |
| Architektur | 2.9 | auf richtigem Weg |
| Codequalitaet / Wartbarkeit | 3.1 | befriedigend |
| Robustheit / Stabilitaet | 2.5 | deutlich verbessert |
| Security / Safety | 2.4 | gut mit Restkanten |
| Testabdeckung / QA | 2.3 | stark fuer Services |
| Optik / visuelle UI | 2.8 | funktional-professionell |
| Ergonomie | 3.0 | gut fuer Experten |
| Performance / Betrieb | 3.0 | solide lokal |
| **Dokumentation** | **1.9** | **sehr gut** |
| Repo- / Datenhygiene | 3.6 | Schwachstelle |

Die ehrliche Aussage fuer den Podcast:
- Die Codebasis ist **gut testfaehig** und sicher genug, um sie externen
  Testern zu geben.
- Die **Sicherheit und Stabilitaet** sind in den letzten 2.5 Wochen massiv
  besser geworden.
- Die **Architektur** hat einen klaren positiven Trend.
- Die **KI-Erkennungsqualitaet** ist das groesste Risiko: 52 %
  ValidationLog-Accuracy bedeutet, dass jede zweite KI-Entscheidung
  potenziell falsch ist. Fuer Assistenz ok, fuer autonome Codierung nein.
- Der **Hauptweg nach vorn ist nicht mehr Code**, sondern **Datenarbeit**:
  konsequent labeln, Validierungsset ausbauen, Confusion-Cluster gezielt
  bearbeiten.

---

## 8. Ehrlicher Vergleich mit anderen Programmen

Quelle: `ki-kanalinspektion`-Skill (38 wissenschaftliche Quellen).

### 8.1 Kommerzielle Marktfuehrer

#### **WinCan VX (CD Lab AG)** und Sewermatics

- Unbestrittener Marktstandard in Europa, jahrzehntelang gewachsen.
- Vollstaendiger Workflow von Aufnahme bis Bericht.
- KI-Module heissen z.B. **WinCan AI** und **Sewermatics KI-Coder**.
- Erkennungsleistung: laut Sewermatics-Whitepaper > 90 % Recall auf
  Hauptschadensklassen (Risse, Wurzeln, Korrosion).
- Stark in: Reife Hardware-Anbindung, Druckworkflow, Schnittstellen,
  Service.
- Schwach in: meist Cloud-/Online-Lizenzbindung, hohe Lizenzkosten,
  geschlossen, kaum Anpassbarkeit, mancher Kunde sieht Latenz und
  Datenschutz kritisch.
- **Vergleich SewerStudio:** WinCan ist heute fachlich praeziser im
  Schadens-Recall. SewerStudio ist offener, lokal, datenschutzfreundlich
  und billiger im Betrieb, aber hat klar weniger Reife in der KI-Praezision.
  Der Hebel zum Aufholen ist Active Learning auf der eigenen KB.

#### **VAPAR (Australien)**

- Cloud-First, Auto-Coding direkt aus dem Video.
- Einfache UI, gut fuer Versorgungsunternehmen.
- Voll-Cloud, alles geht hoch — fuer DSGVO/CH-Verhaeltnisse kritisch.
- **Vergleich:** SewerStudio ist das Gegenteil — lokal, nichts geht hoch.
  VAPAR kann mit groesseren Trainingsdaten arbeiten (Mehrere Kunden, ein
  Modell), SewerStudio lernt nur aus deinen Daten.

#### **SewerAI (USA)**

- Sehr stark bei Pre-Detection und Schaum-/Wasser-Filtern.
- Tightly integrated mit eigenen Inspektionsdienstleistern.
- Cloud, US-zentriert.
- **Vergleich:** SewerAI hat den groesseren Trainingskorpus, aber die
  US-Codierung passt nicht 1:1 auf VSA-KEK / EN 13508-2.

### 8.2 Forschungs-/Open-Source-Projekte

#### **Sewer-ML (DTU)**

- Datensatz mit ~1.3 Mio Bildern, 17 Defektklassen.
- Veroeffentlichte Modelle wie Sewer-YOLO-Slim, RT-DETR, VGG-basiert.
- Beste Forschungs-mAP liegt aktuell bei ~62 % auf Sewer-ML.
- **Vergleich:** SewerStudio nutzt aehnliche YOLO-Architektur. Eigenes
  YOLO26l-seg auf 3.254 Trainingsbildern hat nach internem Audit ~25 %
  mAP — deutlich darunter, weil der eigene Datensatz kleiner ist und
  klassenungleich verteilt.

#### **Open-Source-Detektoren (YOLOv8/YOLO26)**

- Frei verfuegbar, gut dokumentiert.
- Brauchen aber den ganzen Drumherum-Stack: Datenpipeline, Annotation,
  Trainingsinfrastruktur, KB.
- **Vergleich:** SewerStudio ist genau das Drumherum.

### 8.3 Wo SewerStudio einzigartig ist

1. **Lokale Multi-Modell-Pipeline** (YOLO + Qwen-VL + SAM + DINO + nomic)
   in einer Windows-App, ohne Cloud. Das ist nicht Standard.
2. **Vollstaendiger VSA-KEK / EN-13508-2-Codepfad** mit XML-Katalog,
   Severity, Uhrlage, Streckenschaeden, Zustandsklasse, VSA-Zustandsnoten.
3. **Self-Improving-Schleife mit Human-in-the-Loop** und transparentem
   Quality Gate. Bei kommerziellen Tools meist Black Box.
4. **Audit- und Diagnose-Tab als First-Class-Feature** — Brain-Mirror-Check,
   Frame-Cleanup, Versions-Pruning sichtbar im UI.
5. **Komplette PDF-Format-Bibliothek** fuer Schweizer Markt
   (Fretz, KIT, Abwasser Uri, IBAK direkt) inklusive Caesar-Decode bei
   IBAK-IKAS-PDFs.
6. **Hydraulik-Modul + Eigendevis** als optionale Expertenmodule.
7. **773 automatisierte Tests** in einem Solo-Projekt — das ist mehr als
   manche kommerzielle Produkte.

### 8.4 Wo SewerStudio klar zurueckliegt

1. **Datenmenge fuers Training:** 3.254 vs. 1.3 Mio bei Sewer-ML.
2. **Erkennungs-Praezision:** ~52 % ValidationLog-Accuracy ist fuer
   autonome Codierung zu wenig. Marktfuehrer liegen >85 %.
3. **Hardware-/Sensor-Anbindung:** WinCan kann direkte Live-Aufnahme aus
   Inspektionsfahrzeugen. SewerStudio arbeitet auf bereits aufgenommenem
   Material.
4. **UI-Ergonomie fuer Anfaenger:** Cockpit-Stil, viele Buttons. Marktfuehrer
   haben gelernte UX-Pfade.
5. **Onboarding-Aufwand:** lokale GPU, lokale Modelle, lokale KB —
   einfacher Setup-Klick wie bei Cloud-Tools nicht moeglich.
6. **Service / Support:** Solo-Entwicklung, kein 24/7-Hotline.

### 8.5 Fazit Vergleich

SewerStudio ist nicht "das gleiche, aber billiger". Es ist ein **anderer
Produkt-Charakter**:

- **Open**, **lokal**, **datenschutzkonform**, **anpassbar**.
- Mit transparenter KI-Pipeline und sichtbarem Quality Gate.
- Fuer den Schweizer/europaeischen Markt mit VSA-KEK, nicht generisch.
- Fuer Anwender, die Daten in der eigenen Hand behalten wollen.

Der direkte Vergleich zur Erkennungs-Praezision der Marktfuehrer ist
heute verloren. Aber: mit konsequentem Active Learning auf der eigenen
KB ueber 6-12 Monate ist die Luecke schliessbar — und das **ohne
laufende Lizenzkosten**.

---

## 9. Zusammenfassung fuer den Podcast (60-Sekunden-Pitch)

> "SewerStudio ist eine lokale, datenschutzfreundliche
> Kanal-Inspektions-Software fuer Windows. Sie macht aus
> Inspektionsvideos automatisch VSA-KEK- und EN-13508-2-konforme
> Schadensprotokolle. Im Hintergrund laufen fuenf KI-Modelle parallel
> auf einer einzigen RTX 5090 — YOLO erkennt grobe Schaeden, SAM macht
> sie pixel-genau, Grounding DINO sucht ungewoehnliche Befunde, und ein
> Qwen3-VL-Sprachmodell mit 8 Milliarden Parametern erklaert sie. Wenn
> die kleine Variante unsicher ist, eskaliert das System auf eine
> 32-Milliarden-Variante.
>
> In den letzten zweieinhalb Wochen wurde die Codebasis in 255 Commits
> komplett durchgehaertet: 8 Sicherheitsluecken geschlossen, eine
> 5.370-Zeilen-God-Class auf 842 Zeilen reduziert, 76 Dateien aus dem
> UI-Layer in saubere Architekturschichten verschoben, ein DI-Container
> eingebaut, ein Diagnose-Tab fuer Wartung hinzugefuegt, und die
> Test-Coverage von 654 auf 773 erhoeht.
>
> Die ehrliche Wahrheit: Die Architektur und die Sicherheit sind jetzt
> auf einem guten Niveau. Die Erkennungs-Praezision der KI ist es noch
> nicht. Bei 52 Prozent Treffer im Validierungslog ist das System ein
> sehr gutes **Assistenzwerkzeug**, aber noch kein **autonomer Codierer**.
> Der Weg dahin fuehrt nicht ueber mehr Code, sondern ueber Datenarbeit:
> 100 Samples pro Woche manuell pruefen, ueber Monate.
>
> Im Vergleich zu WinCan, Sewermatics oder VAPAR ist SewerStudio
> schwaecher in der KI-Praezision, aber staerker in Datenschutz,
> Offenheit und Anpassbarkeit. Es ist kein billiges Plagiat eines
> kommerziellen Tools, sondern ein anderer Produkt-Charakter — fuer
> Anwender, die ihre Inspektionsdaten in der eigenen Hand behalten
> wollen und bereit sind, in die Reifung der eigenen KI zu investieren."

---

## 10. Anhaenge

- `docs/INTENSIV_AUDIT_STANDORTBESTIMMUNG_2026-05-07.md` — die formale
  Notengebung mit Verifikation
- `docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md` — Schlussanalyse mit allen
  Befunden und Status
- `docs/TEST_BRIEFING_2026-05-07.md` — externes Testerbriefing
- `docs/KI-PIPELINE-GESAMTAUDIT.md` — vollstaendige Pipeline-Dokumentation
- `docs/CODIER-MODUS-PIPELINE.md` — Codiermodus im Detail
- `docs/AUDIT_SECURITY_ARCHITECTURE_ERRORS_2026-04-25.md` — Sicherheits-Audit
- `docs/audits/v4.3/AUDIT_SUMMARY.md` — Phasen-Status
- `CLAUDE.md` — Architektur-Prinzipien und Pipeline-Stand
