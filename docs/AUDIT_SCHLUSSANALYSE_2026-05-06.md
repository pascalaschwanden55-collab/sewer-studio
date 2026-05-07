# SewerStudio — Komplette Schlussanalyse

**Stand:** 2026-05-06, nach Phase-5.3-Migrationssession (50 Commits, 76 Files migriert)
**Branch:** `feature/pdf-import-beobachtungen` — Push erfolgreich auf GitHub
**Build:** ✅ green · **Tests:** ✅ 654 grün (140 Infrastructure + 514 Pipeline)

---

## 1. Architektur

### 1.1 Aktuelle Schichten-Verteilung

| Schicht | KI-Files | Sonstige | Gesamt-LOC |
|---------|----------|----------|------------|
| **Domain** | 4 | viele | — |
| **Application** | 93 | viele | — |
| **Infrastructure** | 48 | viele | — |
| **UI** | 30 | viele | — |
| **Sidecar (Python)** | — | 5524 LOC | — |
| **Source-Total** | — | — | **~65 400 LOC** |
| **Tests** | — | — | **12 265 LOC** |

### 1.2 Was heute migriert wurde (Phase 5.3, Audit-Befund ARCH-C1)

**Vorher:** UI/Ai = 107 Files, Application/Ai = 12, Infrastructure/Ai = 3 → 89 % der KI-Logik im UI.
**Jetzt:** UI/Ai = 30 Files, Application/Ai = 93, Infrastructure/Ai = 48 → 71 % der KI-Logik im richtigen Layer.

**Erfolgte Migrationen (Auswahl):**
- Pure DTOs/Records (VisionPipelineDtos 64 Records, DifferenceModels, Vision-Records)
- Statische Algorithmen (DifferenceAnalyzer, MaskQuantification, AutoApprovalService, OverlayToolService)
- KnowledgeBase-Familie (KnowledgeBaseManager, EmbeddingService, RetrievalService, KbDeduplication, KbIngestion → Infrastructure)
- VsaCodeTree + VsaCodeResolver + LiveDetectionMapper (914 LOC) → Application
- Pipeline-Orchestratoren (BatchPipelineService, EnhancedVisionAnalysisService, MultiModelAnalysisService → Infrastructure)
- ReviewQueue-Familie (Item, Selector, Store, Service, UncertaintySampling)
- Stores (TrainingSamples, FewShot, Benchmark, Frame, Escalation, AiOptimization)

### 1.3 Eingeführte Bridge-Patterns

Sechs **Provider/Notifier** in `Application/Ai/`, beim App-Start in `App.xaml.cs` registriert:

1. `KnowledgeRootProvider` — liefert C:\KI_BRAIN-Pfad
2. `AppDataPathProvider` — LocalAppData-Pfad
3. `KnowledgeMirrorNotifier` — E:\Brain Sync-Trigger
4. `KnowledgeBasePathProvider` — KB-DB-Pfad (Sub-D)
5. `SidecarAuthTokenAccessor` — Sidecar-Auth-Token
6. `OllamaConfigProvider` — Ollama-Konfig
7. `AiRuntimeConfigProvider` — KI-Laufzeit-Konfig

Dadurch sind Application/Infrastructure-Services entkoppelt, ohne dass die UI-Implementierungen umgeschrieben werden mussten.

### 1.4 Was bleibt in UI/Ai (30 Files) — und warum

| Kategorie | Files | Blocker |
|-----------|-------|---------|
| **WPF-Bitmap-Operations** | 6 | `System.Windows.Media.Imaging.BitmapDecoder/FormatConvertedBitmap` (PdfProtocolExtractor, FrameQualityFilter, AutoCalibrationService, ImageInversionHelper, TechniqueAssessmentService, TrainingAnnotationExportService) |
| **WPF-Layout/Render** | 5 | `Canvas`, `Shape`, `Color`, `Cursor` (SamMaskRenderer, MultiModelAnalysisService, PhotoAssistantTools, BendAngleToolService, DeformationToolService, LateralToolService) |
| **TrainingCenter MVVM** | 4 | `CommunityToolkit.Mvvm.ObservableObject` in TrainingCase (TrainingCenterModels, TrainingCenterStore, TrainingCenterImportService, TrainingSampleGenerator) |
| **UI-Lifecycle/Plumbing** | 4 | AppSettings.Load, KnowledgeRoot mit Migration-Logik, PythonSidecarService Process-Lifecycle, AiPlatformConfig (AiPlatformConfig, KnowledgeRoot, PythonSidecarService) |
| **ViewModel-Dependencies** | 3 | DifferenceEntryViewModel, AiSuggestion, ProtocolTrainingStore (KbEnrichmentService, OllamaProtocolAiService) |
| **PDF-OCR** | 4 | `Windows.Media.Ocr` braucht net10.0-**windows**-Target (OcrPdfFallbackService, PdfProtocolTableParser, PdfProtocolExtractor, ProtocolLoaderFactory) |
| **Big-Orchestratoren** | 4 | Mix aus oben (BatchSelfTrainingOrchestrator, SelfTrainingOrchestrator, FewShotExampleBuilder, InitialTrainingOrchestrator, VideoAnalysisPipelineService, QuickScanService) |

### 1.5 Architektur-Stärken

✅ Klare Schicht-Pyramide (Domain ← Application ← Infrastructure ← UI), durchgesetzt via Project-References
✅ DI-Container statt Service-Locator-Anti-Pattern (Phase 5.1.B abgeschlossen)
✅ Ollama, Sidecar-API und KB sauber in Infrastructure
✅ POCO-Records mit `record`/`init` durchgehend, immutable wo möglich
✅ Provider-Pattern als sauberer Ausweg aus zirkulären UI-Abhängigkeiten

### 1.6 Architektur-Schwächen

⚠️ **PlayerWindow.xaml.cs: 5 370 Zeilen** — God-Class (VLC + AI + Measure + Protocol + Hotkeys + Overlay). Wurde im Audit ARCH-C3 als CRITICAL geflagged.
⚠️ **TrainingCase mit ObservableObject** blockiert 4 Migrationen — POCO/Wrapper-Split überfällig
⚠️ **WPF-Imaging-Code** verteilt über 11 Files. Zentraler `IBitmapAnalyzer`-Adapter würde die Migration aller bitmap-bound Services ermöglichen.
⚠️ **Domain-Models mit `INotifyPropertyChanged`** (HaltungRecord, SchachtRecord, Project) blockiert Headless-Use (ARCH-H1)
⚠️ Keine Interfaces für KI-Kern-Services (MultiModelAnalysisService, QualityGateService, DetectionAggregator) — Tests schwer

### 1.7 Verbesserungen

1. **WPF-Imaging-Adapter (Sub-A):** `IBitmapAnalyzer` in Application + `WpfBitmapAnalyzer` in UI. Entsperrt 6 Bitmap-bound Services. Aufwand: 1-2 Sessions.
2. **TrainingCase POCO/Wrapper-Split (Sub-B):** ✅ **abgearbeitet** 2026-05-07. POCO `TrainingCase` in Application, `TrainingCaseViewModel` in UI. TrainingCenterStore → Application, TrainingCenterImportService → Infrastructure. JSON-Roundtrip-Tests (Legacy + neu) gruen. TrainingSampleGenerator + SelfTrainingOrchestrator bleiben in UI bis WPF-Imaging-Adapter (Sub-A) den PdfProtocolExtractor entkoppelt.
3. **PlayerWindow zerschlagen:** Stufenweise via `partial class` (PlayerWindow.Vlc.cs, .Ai.cs, .Measure.cs, .Protocol.cs, .Hotkeys.cs). Risiko niedrig wenn auf partials umgestellt.
4. **Interfaces für Kern-Services (ARCH-H3):** `IMultiModelAnalysisService`, `IQualityGateService`, `IDetectionAggregator`. TDD wird möglich.
5. **Code-Konvertierung Domain-Models (ARCH-H1):** ObservableObject-Wrapper im UI, POCO-Records in Domain. Aufwand: 2-3 Sessions.

---

## 2. KI-Pipeline

### 2.1 Aktuelle Modelle (CLAUDE.md, Stand 2026-04-30)

| Modell | Mode | VRAM | Zweck |
|--------|------|------|-------|
| **YOLO26l-seg** (TensorRT) | permanent GPU | ~1.5 GB | Detection + Segmentation, 10 Klassen, Custom-Weights `D:/yolo_sewer_v1` (3 254 train + 809 val) |
| **Qwen3-VL-8B-Q8** | FastModel GPU | ~11 GB | Permanent geladen, num_gpu=999 erzwungen |
| **Qwen3-VL-32B** | ReferenceModel HYBRID | ~2 GB VRAM + 20 GB RAM | Eskalation (num_gpu=10) — 9 s vs. 28 s bei num_gpu=0 |
| **Grounding DINO 1.5** | lazy | — | Erst bei Request, ausser SEWER_SIDECAR_PREWARM_DINO=1 |
| **SAM 2.1 Hiera-L** | pre-warmed | ~2.5 GB | Rollback von SAM 3 wegen Stabilitaet |
| **nomic-embed-text** | on-demand | ~0.6 GB | KB-Retrieval, 137 M Parameter, F16 |
| **ByteTrack/OC-SORT** | CPU | — | Permanent aktiv |

**VRAM-Budget Soll-Stand:** ~17.6 GB. Reserve: ~14 GB für DINO + Puffer (RTX 5090 32 GB).

### 2.2 Inference-Orchestrator (4 States)

Implementiert in `MultiModelAnalysisService`:
1. **DETECT** — YOLO + Tracker + Aggregator
2. **SEGMENT** — YOLO + SAM
3. **CLASSIFY** — YOLO + Qwen-8B
4. **ESCALATE** — YOLO + Qwen-8B + 32B-hybrid (Trigger: `allCodesNull || severity≥4 || poorQuality`)

**SemaphoreSlim(1)** schützt vor parallelen 32B-Anfragen.

### 2.3 Sidecar (Python FastAPI, Port 8100)

10 Wrapper-Module + 9 Routes:
- `yolo_wrapper`, `sam_wrapper`, `dino_wrapper`, `dinov2_wrapper`, `changenet_wrapper`, `nemotron_parse_wrapper`, `pipe_axis`, `video_decoder`, `vsr_wrapper`
- Routes: `health`, `yolo`, `sam`, `dino`, `dinov2`, `changenet`, `enhance`, `parse`, `pipe_axis`, `training`, `lora_training`

5 524 LOC eigener Python-Code (ohne `.venv`).

### 2.4 Pipeline-Stärken

✅ Hybride Eskalation (8B → 32B mit num_gpu=10 statt Swap) ist eine elegante VRAM-Lösung
✅ TensorRT-Custom-Weights für YOLO — produktionsreif
✅ Sidecar-Architektur mit FastAPI ist sauber von WPF entkoppelt
✅ Pre-Warm + Keep-Alive für Latenz-kritische Modelle
✅ Strict-JSON-Schema für Qwen-Outputs verhindert Halluzinations-Mismatch

### 2.5 Pipeline-Schwächen

⚠️ Rollback von SAM 3 → SAM 2.1 ohne A/B-Test (CLAUDE.md erwähnt es, aber nichts dokumentiert wie SAM 3.1 wieder rein soll)
⚠️ Florence-2 läuft nur als "Shadow/Lernmodus" — unklar, ob Daten genutzt werden
⚠️ Pipeline-Telemetry (Phasen-Latency, P95) wird zwar erfasst (PipelineTelemetry → Application/Ai/Pipeline), aber nicht persistiert — verloren nach App-Neustart
⚠️ Keine zentrale Pipeline-Test-Infrastruktur — `MultiModelAnalysisServiceTests` ist nur ein einziger File

### 2.6 Verbesserungen

1. **Pipeline-Telemetry persistieren:** SQLite-Tabelle `PipelineRuns` mit Phasen-Latencies, dann Trend-Analyse über Zeit. Erkennt ob Sidecar/Modelle langsamer werden.
2. **A/B-Framework für SAM 3.1-Test:** Boolean-Flag in Settings, beide Wrapper koexistieren, Output-Diff loggen. Wenn 3.1 stabil → Default umschalten.
3. **Florence-2 Shadow-Output auswerten:** Vergleich-Logger gegen YOLO-Klasse. Wenn Florence-2 systematisch besser auf bestimmten Klassen → in Pipeline aufnehmen.
4. **Interfaces für Pipeline-Services:** Erlaubt Mock-Testing und Isolations-Tests.
5. **Frame-Step automatisch anpassen:** Bei "leeren" Haltungen größerer Step (5 s), bei aktiven 1 s. Aktuell hardcoded in Settings.

---

## 3. KnowledgeBase (KI-Datenbank Brain)

### 3.1 KB-Statistik (Stand jetzt)

```
KnowledgeBase.db        187 MB
Samples                 21 794 Eintraege
Embeddings              21 794 (nomic-embed-text, 768-Dim, 3 072 Bytes)
Versions                1 478 Snapshots (seit 2026-04-02 bis 2026-05-02)
ValidationLog           290 Eintraege
TrainingRuns            0
SanierungDecisionLog    0
CategoryWeights         0

Distinct VSA-Codes:     207
```

### 3.2 Top-15 VSA-Codes in KB

| Code | Anzahl | Bedeutung |
|------|--------|-----------|
| BCE | 2 656 | Rohrende |
| BCD | 2 219 | Rohranfang |
| BDA | 1 327 | Allgemeinzustand |
| BCAAA | 929 | Anschluss A.A |
| BCAEA | 711 | Anschluss E.A |
| BAJC | 537 | Korrosion C |
| BAJB | 529 | Korrosion B |
| BCCBY | 526 | Bogen B.Y |
| BDDC | 515 | Streckenschaden |
| BCCAY | 511 | Bogen A.Y |
| BAFCE | 451 | Deformation C.E |
| BAAA | 440 | (Wandbeschaedigung A) |
| BAHC | 439 | Versatz C |
| BDBA | 439 | (B-Code) |
| BCC | 412 | Bogen (allg.) |

### 3.3 QualityGate-Verteilung (Auffaellig!)

| Grade | Anzahl | Anteil |
|-------|--------|--------|
| **Green** | 2 452 | 11 % |
| **Yellow** | 10 951 | **50 %** |
| **Red** | 8 391 | **38 %** |

⚠️ **Nur 11 % der KB-Samples sind als Green eingestuft.** Der Rest ist Yellow/Red — Samples die eigentlich Review brauchen, aber trotzdem in der KB sind. Das ist mit dem Sample-Quality-Gate-Service (Sub-D Teil 1) jetzt erkennbar.

### 3.4 SourceType-Verteilung

| Quelle | Anzahl |
|--------|--------|
| BatchImport | 16 051 (74 %) |
| VideoTimestamp | 2 849 (13 %) |
| DB3Profile | 2 229 (10 %) |
| PdfPhoto | 482 (2 %) |
| FeedbackReview | 104 (0.5 %) |
| TeacherAnnotation | 79 (0.4 %) |

### 3.5 Korrekturen (IsKorrigiert)

| Status | Anzahl |
|--------|--------|
| 0 (unkorrigiert) | 21 106 (97 %) |
| 1 (korrigiert) | 688 (3 %) |

⚠️ Nur 3 % der Samples wurden je manuell korrigiert. Die Active-Learning-Pipeline ist vorhanden, aber unterausgenutzt.

### 3.6 ValidationLog (KI vs. Mensch)

| WasCorrect | Anzahl |
|------------|--------|
| 0 (KI falsch) | 140 |
| 1 (KI korrekt) | 150 |

→ **52 % Accuracy in den 290 Validierungen.** Stichprobe ist klein, aber zeigt: KI ist nur knapp besser als Münzwurf bei diesen Codes.

### 3.7 Brain-Verzeichnis-Volumen

```
113 GB Total (C:/KI_BRAIN/)
  67 GB  frames/                  (extrahierte Video-Frames)
  21 GB  training_frames/
   9 GB  images/
   5.9 GB snapshots/              (KB-Snapshots; 1478 sind viel)
   3.5 GB yolo_top6_dataset/
   3.5 GB yolo_top5_dataset/
   1.4 GB yolo_viewtype_dataset/
   825 MB yolo_viewtype_dataset_v2/
   506 MB yolo_seg_dataset/
   408 MB yolo_seg_runs/
   238 MB fewshot_images/
   241 MB teacher_images/
   187 MB KnowledgeBase.db
```

### 3.8 KB/Brain-Stärken

✅ 21 794 Samples mit Embeddings — solide KB-Basis
✅ 1 478 Versions-Snapshots ermöglichen Rollback bis 2026-04-02
✅ Brain-Mirror-Logik (C:\KI_BRAIN → E:\Brain) abgedeckt
✅ 207 distinct VSA-Codes — gute Code-Diversität
✅ Embedding-Dimension 768 (nomic-embed-text) ist Stand der Technik

### 3.9 KB/Brain-Schwächen

⚠️ **89 % Yellow/Red-Samples** in KB → Active-Learning-Pipeline ungenutzt
⚠️ **97 % unkorrigiert** → menschliche Validierung fast nie passiert
⚠️ **Long-Tail-Problem:** Top-15 Codes haben >400 Samples, aber 192 weitere Codes (= ~92 %) haben weniger
⚠️ **Versions-Inflation:** 1 478 Snapshots in 30 Tagen = ~50 pro Tag. Wahrscheinlich pro Index-Operation einer. Bläht DB unnötig auf.
⚠️ **TrainingRuns + SanierungDecisionLog + CategoryWeights leer** — diese Tabellen werden nicht benutzt
⚠️ **15 Backup-Files für training_samples.json** — kein automatisches Aufräumen alter Backups
⚠️ **67 GB frames-Ordner** — keine Bereinigungsstrategie. Wird nur größer.
⚠️ **KB-Audit (kb_audit/)** wurde am 2026-05-03 zuletzt geschrieben — kein automatischer Job

### 3.10 KB/Brain Verbesserungen

1. **Yellow/Red-Eskalation aktivieren:** Active-Learning-Curator Skill nutzen, mind. 100 unsicherste Samples/Woche zur manuellen Pruefung. Ziel: Green-Anteil von 11 % auf 30 % bringen.
2. **Versions-Cleanup:** Aelter als 7 Tage und nicht-tagged → loeschen. `sqlite_vacuum`. Spart geschaetzt 80 % der Versions-Tabelle und frees ~50 MB.
3. **Long-Tail-Sampling:** Codes mit <50 Samples gezielt aus alten Inspektionen extrahieren. Oder synthetisch via Qwen-Generation augmentieren.
4. **Backup-Rotation:** Pro File max. 3 Backups, alle aelteren loeschen. Ein Cron-Job/Background-Service.
5. **frames/-Bereinigung:** Frames ohne zugehörigen Sample (orphan) nach 90 Tagen loeschen. Spart geschaetzt 30-40 GB.
6. **TrainingRuns aktiv nutzen:** Jeder YOLO/Qwen-Train sollte einen Eintrag schreiben (mAP, F1, Datum, JobId, GoldStandard-Vergleich). Aktuell unbenutzt.
7. **CategoryWeights nutzen:** Für QualityGate-Calibration. Pipeline-Logik schreibt Weights, aber Tabelle leer → Bug oder ungenutztes Feature
8. **KB-Backup auf E:\Brain validieren:** ✅ **abgearbeitet** — `KnowledgeMirrorService.GetHealth()` liefert Green/Yellow/Red mit Message + Bytes + Alter. UI-Anbindung im Audit-Tab als Folge-Aufgabe offen.

---

## 4. Fehler & Bekannte Probleme

### 4.1 Audit-Befunde (CRITICAL aus AUDIT_SEWERSTUDIO_2026-04-23.md)

- **ARCH-C1** KI-Schicht im UI — heute 71 % abgearbeitet
- **ARCH-C2** Service-Locator-Anti-Pattern (`App.Services`) — durch DI-Migration in Phase 5.1.B abgeschlossen
- **ARCH-C3** PlayerWindow.xaml.cs 5 370 Zeilen — **NICHT** abgearbeitet, immer noch CRITICAL
- **SEC-C1** UI-Pfade ohne Containment-Check — Status unklar
- **SEC-H5** Sidecar-Auth — vor Migration: SidecarAuthTokenAccessor jetzt integriert

### 4.2 HIGH-Befunde (Stand laut Audit-Doc 2026-04-23)

**Sicherheit:**
- SEC-H1-H3, M4: Command-Injection in 8 Process.Start-Stellen — ✅ **alle 8 Stellen behoben** (auf ProcessRunner oder ArgumentList.Add). Letzte 3 Stellen heute 2026-05-06 fixiert (PdfProtocolTableParser, BatchPipelineService, VideoSelfTrainingOrchestrator).
- SEC-H4: Path-Traversal via Haltungs-ID — ✅ **behoben** in `ProjectPathResolver.SanitizePathSegment` (Trim trailing dots, Reject `.`/`..`, Replace eingebettetes `..`).

**Stabilität:**
- STAB-H1: HttpClient-Leak in Feedback-Pipeline — ✅ **behoben** (static `_feedbackHttpClient` in PlayerWindow.Feedback.cs + TrainingCenterWindow.xaml.cs).
- STAB-H2: AppendAllTextAsync ohne Lock auf Feedback-JSONL — ✅ **behoben** (`_positiveFeedbackLock` + `_negativeFeedbackLock` SemaphoreSlim). Zusaetzlich: OllamaClient `qwen_raw_responses.log` mit Lock geschuetzt.
- STAB-H3: AppendBatchHistoryAsync Race im Parallel-Batch — ✅ **behoben** (`_batchHistoryLock` SemaphoreSlim).
- STAB-H4: PythonSidecarService Hard-Kill ohne Graceful-Shutdown — ✅ **behoben** (Sidecar `/shutdown`-Endpoint via SIGINT, C# POSTet vor Kill mit 3s-Wait, faellt auf Hard-Kill zurueck).
- STAB-H5: async-void-Timer in PlayerWindow → Window-Close-Race — ✅ **behoben** (`_isWindowClosed`-Guard zu Beginn jedes async-void-Tick-Handlers).
- STAB-H6: Exception-Swallowing in `KbQualityService.FindStaleCandidates()` — ✅ **behoben** (Debug-Log statt `catch{}`, Diagnose-Information bleibt erhalten).

### 4.3 Code-TODO-Marker

Nur **6 TODO/FIXME** im gesamten Code:
- 1× EnhancedVisionAnalysisService: Few-Shot-Format ueberarbeiten
- 5× HoldingFolderDistributor + IbakInspectionProfileExtractor: Pattern-Kommentare (kein Bug)

→ Sehr sauber für 65 400 LOC. Das Audit ist die wichtigere Quelle.

### 4.4 Test-Coverage

- 654 Tests (140 Infrastructure + 514 Pipeline)
- LOC-Ratio Tests:Source = **12 265 : 65 409 ≈ 19 %**
- 2 Skip-Tests
- **Keine** Tests für: PlayerWindow, MultiModelAnalysisService, Sidecar-Routes (Python), VLC-Steuerung, SAM-Renderer

### 4.5 Verbesserungen

1. **PlayerWindow auf partial class splitten** (CRITICAL ARCH-C3): partials + ViewModel-Extraktion. Risiko: niedrig, Aufwand: 2-3 Sessions.
2. **Command-Injection-Fixes** (SEC-H1-H3): einheitlich auf `psi.ArgumentList.Add()` umstellen. ~2 h Aufwand für 8 Stellen.
3. **HttpClient-Leak fixen:** Static Shared HttpClient oder IHttpClientFactory. ~1 h.
4. **SemaphoreSlim für JSONL-Writes:** STAB-H2 + H3. Pro Datei einer. ~30 min.
5. **PythonSidecarService Graceful-Shutdown:** CTRL_BREAK_EVENT senden, dann 3 s warten, dann Kill. ~1 h.
6. **async-void-Timer-Guards:** `if (_player is null || _disposed) return;` zu Beginn. ~15 min.
7. **Test-Coverage erhöhen:** Sidecar-Routes per pytest, MultiModelAnalysisService mit Mock-Sidecar. Ziel: 30 %.

---

## 5. Konsistenz

### 5.1 Code-Konsistenz-Stärken

✅ Pure POCO-Records mit `record`/`init` durchgehend in Application/Domain
✅ Provider-Pattern konsistent (6 Provider nach gleichem Schema)
✅ Async/Await mit `ConfigureAwait(false)` durchgehend
✅ `SemaphoreSlim` für KB-Writer-Lock konsistent
✅ Polly Retry + Circuit Breaker für OllamaClient
✅ JSON-Strict-Schema für Qwen-Outputs (kein freier Text)
✅ Deutsche Kommentare durchgehend (CLAUDE.md-Konvention)
✅ EN 13508-2 / VSA-KEK 2023 als Single-Source-of-Truth (VsaCodeTree)

### 5.2 Konsistenz-Schwächen

⚠️ **Mischung POCO/MVVM in Domain:** HaltungRecord/SchachtRecord/Project haben `INotifyPropertyChanged` (ARCH-H1)
⚠️ **6 verschiedene Trainings-Orchestratoren** ohne gemeinsame Interface-Hierarchie (ARCH-H2)
⚠️ **Pipeline-Modus-Wahl** in `ShouldUseMultiModelAsync` jeweils neu — keine zentrale Pipeline-Strategy
⚠️ **AppSettings vs. AiPlatformConfig vs. AiRuntimeConfig** — drei Configs für verwandte Werte
⚠️ **Manche Stores nutzen `KnowledgeRoot.GetTrainingSamplesPath()`, andere `Path.Combine(KnowledgeRoot.GetRoot(), "...json")`** — inkonsistent

### 5.3 Verbesserungen Konsistenz

1. **`ITrainingPipeline`-Interface** für 6 Orchestratoren — gemeinsame Strategy
2. **Config-Konsolidierung:** AppSettings raw → AiPlatformConfig parsed → AiRuntimeConfig ist klar. Aber doppelte Pfade machen Wartung schwer.
3. **Path-Konstanten zentral:** `KnowledgeRootPaths.TrainingSamples`, `KnowledgeRootPaths.FewShotImages` statt verstreute `Path.Combine`-Calls.
4. **Domain → POCO + UI → Wrapper** durchziehen (ARCH-H1)

---

## 6. Robustheit

### 6.1 Robustheit-Stärken

✅ KB-Writer mit `SemaphoreSlim` + `ExecuteInTransaction` — atomar
✅ JSONL-Write mit Disk-Full-Guard (1 GB-Limit) in KnowledgeBaseManager
✅ Lazy-Load in ReviewQueueService (Datei-IO erst bei erstem Zugriff)
✅ Polly Retry für Ollama (300 ms Backoff, 3x)
✅ Circuit Breaker bei Ollama-Konsekutiv-Fehlern
✅ Path-Provider mit Test-Fallback (%TEMP%-Dir falls kein Resolver)
✅ Config-Loader mit InvalidOperationException → Fail-Fast bei Missregistration

### 6.2 Robustheit-Schwächen

⚠️ **Sidecar-Hard-Kill** ohne Graceful-Shutdown (STAB-H4) → zombie-Python möglich
⚠️ **VLC-async-void Timer** ohne Disposed-Check (STAB-H5)
⚠️ **JSONL-Append ohne Lock** (STAB-H2 + H3) → korrupte Zeilen möglich
⚠️ **HttpClient-Leak** in Feedback-Loop (STAB-H1) → 500 Klicks = ~1 GB Socket-Pool
⚠️ **OperationCanceledException** wird in mehreren Catches geschluckt — Cancellation propagiert nicht durch
⚠️ **KB-Restore aus E:\Brain Mirror** ist ein Best-Effort — kein Health-Check ob Mirror konsistent ist
⚠️ **frames/-Ordner** wächst unbegrenzt (67 GB jetzt) — kein Cleanup-Job
⚠️ **Versions-Tabelle** wächst unbegrenzt (1 478 in 30 Tagen)

### 6.3 Verbesserungen Robustheit

1. **Sidecar Graceful-Shutdown:** `GenerateConsoleCtrlEvent` vor `Kill(entireProcessTree: true)`
2. **JSONL-Lock:** `static SemaphoreSlim` pro Datei (Feedback, BatchHistory)
3. **HttpClient-Singleton:** `services.AddHttpClient<TService>()` nutzen statt `new HttpClient()`
4. **KB-Mirror Health-Check:** Alle 24h `sha256` von DB vergleichen, bei Drift loggen
5. **frames/-Cleanup:** Background-Service der jede Nacht orphans löscht (Frame ohne zugehörigen Sample)
6. **Versions-Pruning:** Wöchentlich Versions älter als 7 Tage löschen + VACUUM
7. **OOM-Watchdog:** VRAM-Monitor, bei <2 GB free → 32B-Modell entladen, Warnung loggen

---

## 7. Qualität (Programm + KI-Wissen)

### 7.1 Programm-Qualität

**Positiv:**
- Sehr saubere Architektur nach heutiger Migration
- 654 Tests grün durchgehend
- Minimal TODO/FIXME-Marker
- Deutsche Kommentare konsistent
- Build dauert <10 s

**Negativ:**
- 5 370-Zeilen-PlayerWindow ist unwartbar (ARCH-C3)
- 4 691-Zeilen HoldingFolderDistributor — auch eine God-Class
- Test-Coverage ~19 % LOC-Ratio
- Sidecar (Python) hat nur 1 Test-File mit 64 LOC

### 7.2 KI-Wissen-Qualität

**Positiv:**
- 21 794 Samples mit Embeddings — gute Quantität
- 207 unique Codes — Diversität gegeben
- Versions-System funktioniert (Rollback möglich)
- nomic-embed-text-Modell ist Stand der Technik

**Negativ:**
- **89 % Yellow/Red-Samples** sind in der KB — die "schlechten" dominieren
- **52 % Accuracy** im ValidationLog (290 Stichproben)
- **97 % unkorrigiert** — fast keine menschliche Validierung
- Long-Tail nicht adressiert (192 von 207 Codes haben <450 Samples)
- TrainingRuns + CategoryWeights + SanierungDecisionLog leer (ungenutzt)

### 7.3 Verbesserungen Qualitaet

1. **PlayerWindow zerschlagen** (CRITICAL)
2. **HoldingFolderDistributor refactor** — 4 691 LOC ist nicht wartbar
3. **Sidecar-Tests:** pytest-Tests für jede Route, Mock-Modelle
4. **KB-Cleanup:** Yellow/Red-Anteil reduzieren via Active-Learning + Long-Tail-Augmentation
5. **TrainingRuns aktiv schreiben** — Jeder train.py-Lauf, jeder Qwen-LoRA-Run
6. **SanierungDecisionLog aktivieren** — User-Entscheidungen in der Sanierungsoptimierung loggen
7. **Mehr Validierungs-Stichproben:** Aktuell nur 290 — Ziel 1 000+ für statistisch belastbare Accuracy

---

## 8. Programm-Stand zusammengefasst

### 8.1 Wo stehen wir?

| Bereich | Note (1=top) | Begründung |
|---------|---------------|------------|
| Architektur | **2** | Schicht-Trennung jetzt sauber, aber God-Classes & MVVM-Mix bleiben |
| KI-Pipeline | **2-** | Stabil, aber Pipeline-Telemetry nicht persistiert, SAM-Rollback dokumentationsschwach |
| KB | **3** | Ausreichend Samples, aber Quality-Verteilung schief, Long-Tail unadressiert |
| Brain (Verzeichnis) | **3-** | 113 GB unbereinigt, kein Cleanup-Job, Versions-Inflation |
| Code-Konsistenz | **2** | POCO durchgezogen, aber 3 Configs + 6 Orchestratoren sind viel |
| Robustheit | **3** | Polly + KB-Locks gut, aber Sidecar-Kill + JSONL-Race + HttpClient-Leak offen |
| Qualität (Programm) | **2-** | Sehr sauber wo refactored, aber 2 God-Classes + niedrige Sidecar-Coverage |
| Qualität (KI-Wissen) | **3+** | Ausreichende Datenmenge, aber Validation/Korrektur-Pipeline ungenutzt |

**Gesamt: 2.5** — solides Stand, klare Roadmap für nächste Sessions.

### 8.2 Top-10 Verbesserungen (priorisiert)

1. **PlayerWindow.xaml.cs zerschlagen** (CRITICAL ARCH-C3) — 2-3 Sessions
2. **Command-Injection-Fixes** (SEC-H1-H3) — 2 h
3. **JSONL-Lock + HttpClient-Singleton + Graceful-Shutdown** (STAB-H1-H4) — 3 h
4. **TrainingCase POCO/Wrapper-Split** — ✅ **abgearbeitet** 2026-05-07 (2 Files migriert: Store + ImportService; 2 Files (Generator + SelfTraining) verbleiben bis WPF-Imaging-Adapter-Task)
5. **WPF-Imaging-Adapter** — entsperrt 6 weitere Migrationen — 1-2 Sessions
6. **Active-Learning aktivieren** — Yellow/Red-Anteil von 89 % auf 70 % bringen — laufender Prozess
7. **frames/-Cleanup-Job** — spart 30-40 GB pro Nacht — 1 Session
8. **Versions-Pruning** — KB-DB schlanker, schnellere Backups — 30 min
9. **Pipeline-Telemetry-Persistierung** — SQLite-Tabelle PipelineRuns — 2 h
10. **Sidecar-Tests** — pytest mit Mock-Modellen — 1-2 Sessions

### 8.3 Was funktioniert gut und sollte nicht angefasst werden

- **Provider-Pattern** ist clean, beibehalten
- **DI-Container** mit `AddSewerStudioInfrastructure/CoreServices/AiServices` ist sauber
- **VsaCodeTree** als Single-Source-of-Truth für VSA-Codes
- **TensorRT-YOLO** Performance ist optimal
- **Qwen-Hybrid (8B + 32B)** VRAM-Lösung ist elegant

---

## 9. Was passierte heute (Session-Zusammenfassung)

50 Commits, 76 Files migriert, 6 Provider-Bridges eingeführt:

```
Vorher → Jetzt:
UI/Ai            107 → 30   (-77, 71% raus)
Application/Ai    12 → 93   (+81, 7.7×)
Infrastructure/Ai  3 → 48   (+45, 16×)
Domain/Ai          0 →  4   (+4)

Build green durchgehend, 654 Tests grün nach jedem Commit.
```

**Push erfolgreich auf GitHub.** Branch `feature/pdf-import-beobachtungen` 282 Commits ahead von `main`.

---

*Audit erstellt 2026-05-06, Build green, Tests grün, Push erfolgreich.*
