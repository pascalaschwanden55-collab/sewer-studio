# Audit Claude — SewerStudio KI 4.2

**Datum:** 2026-05-04
**Branch:** feature/pdf-import-beobachtungen
**Methode:** 6 parallele Audit-Agents
**Vergleichs-Baseline:** Audit 2026-04-23

---

## ⚠️ Erste wichtige Information vorab

Das Projekt baut aktuell nicht. `dotnet build` liefert 140 Fehler, hauptsaechlich in DataPage.xaml.cs und ClockPickerControl.xaml.cs — XAML-Elemente (Grid, Face, CenterDot, GridHost, UndockButton u. a.) sind im Code-Behind referenziert, fehlen aber im XAML. Das muss zuerst gefixt werden, bevor irgendetwas anderes Sinn ergibt.

## Punkt 1 — Architektur

### Befund

| Bereich | Zahl | Bewertung |
|---|---|---|
| KI-Dateien in UI | 144 | KRITISCH |
| KI-Dateien in Application | 9 (nur DTOs/Interfaces) | KRITISCH |
| KI-Dateien in Infrastructure | 1 | KRITISCH |
| App.Services-Locator-Treffer | 47 in 21 Dateien | HOCH |
| ServiceProvider.cs Zeilen / Properties | 657 / 31 | HOCH |
| PlayerWindow.xaml.cs Zeilen | 9856 (war 9494 am 23.04.) | KRITISCH |
| Microsoft.Extensions.DependencyInjection genutzt | 0× | HOCH |

Die Schicht-Trennung ist auf Projekt-Ebene sauber (Domain → Application → Infrastructure → UI), aber inhaltlich hohl — die ganze KI lebt im UI-Projekt. PlayerWindow.xaml.cs ist seit dem letzten Audit sogar gewachsen (+362 Zeilen).

### Empfehlungen (priorisiert)

1. **DI-Container einfuehren** (Microsoft.Extensions.DependencyInjection + IHostedService) — ersetzt ServiceProvider.cs schrittweise. Aufwand: ~16 h.
2. **PlayerWindow.xaml.cs aufteilen** in partial class-Files: PlayerWindow.Vlc.cs, .Ai.cs, .Measure.cs, .Protocol.cs. Aufwand: ~24 h, kann inkrementell laufen.
3. **KI-Schicht migrieren** beginnend mit zustandslosen Services (DetectionAggregator, QualityGateService, VsaCodeResolver) → Application.Ai. Schwere Orchestratoren spaeter.
4. **ViewModels per Konstruktor-Injection versorgen** — kein App.Services.X mehr in Feld-Initialisierern (Race-Risiko gegen Startup).

## Punkt 2 — Fehler

### Status der CRITICAL-Befunde aus 2026-04-23

| ID | Status |
|---|---|
| STAB-C1 KnickHttp-Disposed | ✅ GEFIXT |
| STAB-C2 Pause-Deadlock | ✅ GEFIXT |
| STAB-C3 Warmup-Leak | 🟡 TEILGEFIXT — kbHttp (ServiceProvider.cs:230) wird nie disposed |
| STAB-C4 ffmpeg-Pipe-Deadlock | ✅ GEFIXT |
| SEC-H1..H3 Command-Injection | ✅ GEFIXT (ArgumentList) |
| SEC-H4 Path-Traversal | ✅ GEFIXT |
| SEC-H5 Sidecar-Auth | ✅ GEFIXT (Bearer-Token) |

Sehr gute Arbeit zwischen den Audits. Aber die Architektur-CRITICALs (ARCH-C1/C2/C3) sind unveraendert offen.

### Neue Bugs (Stand heute)

| Schwere | Stelle | Problem |
|---|---|---|
| KRITISCH | Build / DataPage.xaml + ClockPickerControl.xaml | 140 Compile-Fehler (XAML/Code-Behind Mismatch) |
| KRITISCH | ServiceProvider.cs:230 | kbHttp = new HttpClient(...) ohne Dispose-Kette → Socket-Leak |
| HOCH | PlayerWindow.xaml.cs:6429,6510,7698,7762 | _ = Task.Run(…) ohne SafeFireAndForget (Helper existiert!) |
| HOCH | SystemMonitorService.cs:112,512,990 | Hardware-Init-Exceptions stillschweigend verloren |
| HOCH | PdfProtocolExtractor.cs:740 | stdout sync, stderr async — Pipe-Buffer-Risk wie STAB-C4 |
| HOCH | KnowledgeMirrorService.cs:277, FewShotExampleBuilder.cs:433 | Empty catch { } ohne Log |
| MITTEL | DataPageViewModel.cs:1147 | using var http = new HttpClient in Hot-Path |

**TODO/FIXME-Backlog:** Praktisch leer — nur 1 echtes TODO im ganzen Repo (ausgezeichnet).

### Empfehlungen

1. **Build reparieren (Tag 1):** XAML-Dateien um fehlende x:Name ergaenzen oder Code-Behind anpassen.
2. **SafeFireAndForget-Helper ueberall einsetzen** (existiert in TaskExtensions.cs) — 1 h Suchen/Ersetzen.
3. **kbHttp-Lifecycle** auf Singleton-HttpClient umstellen oder EmbeddingService IDisposable machen.
4. **Empty-catch-Audit:** alle 6+ Stellen mindestens Debug.WriteLine($"{ex.Message}") — 30 Min.

## Punkt 3 — Konsistenz

### Befund

- 2 Legacy-Klassen ohne [Obsolete]: LegacyPdfImportService, LegacyXtfImportService.
- KnowledgeBaseManager bricht die *Store/*Repository-Konvention.
- 15+ ffmpeg-ProcessStartInfo-Stellen rufen ffmpeg jeweils selbst auf — der zentrale ProcessRunner + FfmpegLocator sind nicht durchgaengig genutzt (z. B. BatchPipelineService, VideoFrameExtractor, MultiModelAnalysisService, BoundaryPhotoService).
- 3 *FrameExtractor-Klassen mit ueberlappendem ffmpeg-Code: VideoFrameStream, VideoFrameExtractor, InspectionFrameExtractor.
- 5 *Pdf*-Klassen mit Mix static/sealed und Suffix Builder/Exporter/Renderer ohne klare Regel.
- 11 ViewModels mit using System.Windows → MVVM-Bruch (TrainingCenterVM, ImageAnnotationVM, DataPageVM, ShellVM, BuilderPageVM, ...).
- 3 ViewModels nutzen noch manuell INotifyPropertyChanged statt [ObservableProperty] (492 Treffer Toolkit-Pattern dominant).
- Logging: sehr konsistent (215× ILogger, 4× Console/Debug).
- 9 Config-Klassen mit Mix *Config/*Settings/*Options ohne Konvention.
- 51 new XxxWindow() / ShowDialog direkt in ViewModels — IDialogService existiert, wird aber nicht durchgaengig genutzt.

### Empfehlungen

1. **ffmpeg-Konsolidierung** (1 Tag) — alle 15 Stellen ueber ProcessRunner.RunAsync(FfmpegLocator.Path, [...]).
2. **IDialogService erzwingen** — 51 new …Window()-Stellen in ViewModels durch _dialogs.ShowDialog<TVm>() ersetzen (~10 h).
3. **Legacy-Klassen** in Import/Legacy/-Subfolder + [Obsolete]-Attribut.
4. **Config-Konvention dokumentieren:** *Options → IOptions-Pattern, *Config → Bootstrap, *Settings → User-Preferences.
5. **KnowledgeBaseManager → KnowledgeBaseStore umbenennen.**

## Punkt 4 — Datenbank / KI-Pipeline / Training / Sanierung

### A) KnowledgeBase

| Status | |
|---|---|
| Schema (5 Tabellen, WAL) | GUT |
| FK-Constraints | fehlen (Embeddings ohne FK auf Samples) |
| Embedding-Versionierung | fehlt (Migration-Falle bei Modell-Wechsel) |
| Dedup-Schwellen 0.92/0.85 | GUT |
| new KbContext() ungebunden | 17×, davon 4× ohne using |
| KB-Inspector-Tool | minimal funktional, keine Health-Checks |

### B) KI-Pipeline

| Status | |
|---|---|
| Eskalation 8B→32B Logik | GUT (drei-stufig) |
| 32B-Throttle | inkonsistent zu CLAUDE.md (SemaphoreSlim(2,2) statt (1,1)) |
| QualityGate-Schwellen 0.75/0.45 | OK, aber CLAUDE.md spricht von 95/4/1 % — Doku ungenau |
| FrameQualityFilter | GUT |
| JSON-Schema-Strict | GUT |
| DamageClassesPromptFull | DEAKTIVIERT (#pragma warning disable CS0414) — KI bekommt weniger Domaenenwissen als geplant |
| Sidecar Lifespan + VRAM-Monitor | sehr robust |

### C) Selbsttraining

| Status | |
|---|---|
| BatchSelfTrainingOrchestrator Pause/Resume | GUT |
| Persistenter Crash-Resume-Checkpoint | fehlt (nur Heuristik) |
| PdfProtocolTableParser (3+ Formate) | GUT, KEK-2023 nicht explizit dokumentiert |
| DifferenceAnalyzer (Greedy + Toleranzen) | GUT |
| MeterToFrameResolver (3-stufig) | GUT |
| Benchmark-Historie (3 Laeufe) | wenig |
| YOLO-Retrain (Mutex, Manifest, MaxArchive=3) | GUT |

### D) Sanierungsvorschlaege

| Status | |
|---|---|
| Wissensbasis (Knowledge/sanierung/*.yaml, last_updated 2026-04-28) | GUT, VSA-KEK 2023 zitiert |
| MeasureRecommendationService (regelbasiert + lernend) | GUT |
| AiSanierungOptimizationService (Hybrid Regel→KI→Cost+Validation) | GUT |
| Architektur-Lage | im UI-Projekt statt Infrastructure |
| RehabilitationRulesEngine.Procedures | hartcoded statt aus YAML (Drift-Risiko) |
| DevisGenerator + DevisExcelExporter | komplett, kein Stub |

### Empfehlungen

1. **DamageClassesPromptFull aktivieren** — sofortige Qualitaetssteigerung, ~30 Min.
2. **Embedding-Versions-Tag** in Embeddings-Tabelle (Model TEXT NOT NULL) — sonst geht bei zukuenftigem Wechsel zu bge-m3 die ganze KB kaputt.
3. **FK-Constraint** Embeddings.SampleId REFERENCES Samples ON DELETE CASCADE.
4. **Resume-Checkpoint** im Batch (HaltungId+Status nach JSON, Restart liest und ueberspringt).
5. **AiSanierungOptimizationService** nach Infrastructure.Sanierung verschieben.
6. **RehabilitationRulesEngine** nur aus YAML laden, Hardcode entfernen.
7. **CLAUDE.md** QualityGate-Prozente vs. Composite-Score klarer trennen.

## Punkt 5 — Slim-Down (was kann weg?)

### Sofort loeschbar (Quick-Wins)

| Pfad | Groesse / Anzahl | Risiko |
|---|---|---|
| sidecar/florence2_shadow_log/ | 6.6 GB / 76'586 JPGs — kein Reader im Code | Niedrig |
| _legacy/ (PS1-Skripte) | 13 Dateien, ungenutzt | Niedrig |
| tools/__pycache__/, mdb_*.txt, pdf_audit_result.csv | Audit-Reste | Niedrig |
| tools/QuickPdfAnalyzer, PdfHeaderReader, PdfImageAnalyzer, DiagnosticPdfParser, AiDocPdf | 5 von 7 PDF-CLIs redundant | Niedrig |
| yolo11*.pt, yolov8l-seg.pt, yolo26{n,s,m,x}*.pt im Repo-Root | ~1.1 GB Roh-Weights | Niedrig |
| Views/Windows/CombinedOfferWindow.xaml(.cs) + OfferCalculatorWindow + CostCalculationWindow | kein new …Window(-Aufruf gefunden | Niedrig |
| PriceCatalogEditorWindow.xaml(.cs) | durch CatalogManagerWindow ersetzt | Niedrig |
| LegacyPdfImportService.cs, LegacyXtfImportService.cs | Adapter zeigen auf neue Implementierung | Mittel |

**Geschaetzter Gewinn:** ~7 GB Disk + ~25 obsolete Dateien.

### Konsolidierungs-Kandidaten (mehr Aufwand)

- SelfTrainingOrchestrator (PDF) vs. VideoSelfTrainingOrchestrator vs. BatchSelfTrainingOrchestrator — PDF-Variante hat nur 1 Aufrufer in TrainingCenterViewModel.cs:2668. Pruefen ob noch UI-Knopf existiert; sonst zusammenlegen.
- VideoFullAnalysisService + QuickScanService + FullProtocolGenerationService — drei "Full-Run"-Wrapper neben VideoAnalysisPipelineService.
- OllamaVisionFindingsService — Parallel-Implementation zu EnhancedVisionAnalysisService.
- McDropoutService, WeightLearningService, AccuracyDashboardService, ModelRegistryService, AutoApprovalService, KbQualityService, ChangeDetectionService — Forschungs-Stubs ohne UI-Aufruf.
- 3 Druck-Dialoge (HydraulikPrintDialog, DossierPrintDialog, PrintOptionsDialog) → einer reicht.
- PdfProtocolExtractor + PdfProtocolTableParser + IbakPdfStammdatenExtractor — alle drei aktiv genutzt, aber gemeinsame Pipeline waere besser.

## Punkt 6 — Optik / Layout (Schulnote 4.4)

### Befund

| Punkt | Note |
|---|---|
| Theme-System | 3- |
| Window-Architektur | 4 |
| MVVM-Hygiene | 5 |
| Visual Quality | 4 |
| UI-Standards (kein Fluent/Material) | 5 |
| Branding | 5 |
| Layout-Probleme | 4 |
| Barrierefreiheit / High-DPI | 6 |

**Konkrete Probleme:**
- 162 Hex-Hardcodes in Theme//Controls.xaml + 22 in Views (z. B. PlayerWindow.xaml, StartupSplashWindow.xaml).
- Kein Live-Theme-Toggle — Wechsel braucht App-Neustart.
- Kein App-Icon im csproj, kein Produktlogo (Assets/Brand/ enthaelt nur PLACE_LOGO_HERE.txt + ein Kunden-Logo).
- Keine Icon-Library — Pfeile/Icons sind Unicode (▲▼) oder inline-Path Data="…".
- Kein zentraler DataGrid-Style (AlternatingRowBackground, Header-Sticky).
- 152 FontFamily=-Treffer mit Mix Consolas/Segoe UI/Cascadia Mono.
- Magic-Number-Padding/Margin ueberall (0,0,0,12, 8,0,0,0, ...).
- app.manifest fehlt → kein PerMonitorV2-DPI → unscharf auf 4K Multi-Monitor.
- Touch-Targets zu klein (Height="26" in DataPage.xaml:19).
- 51 new XxxWindow() direkt aus ViewModels (kein Dialog-Service, Modal-Wildwuchs).
- Style-Duplikation: PlayerWindow.xaml definiert lokale PlayerCard/PlayerLabelText statt Theme-Erweiterung.

### Empfehlungen (Top-10, sortiert nach Wirkung pro Stunde)

| # | Vorschlag | Aufwand |
|---|---|---|
| 1 | app.manifest mit PerMonitorV2-DPI + <ApplicationIcon> im csproj | 1 h |
| 2 | Spacing/Typography-Tokens (Sp.S=4, M=8, L=16; FontSizeBody=13) als StaticResources | 4 h |
| 3 | DataGrid-Style zentral (AlternatingRowBackground, Header-Sticky, kompaktes Padding) | 3 h |
| 4 | PageHeader UserControl (Title + Breadcrumb + Action-Slot) — fuer alle 11 Pages wiederverwendbar | 3 h |
| 5 | Hex-Hardcodes durch DynamicResource ersetzen (162 + 22 Stellen) | 6 h |
| 6 | Icon-Library (FontAwesome.Sharp oder MaterialDesignInXaml-PackIcon) | 6 h |
| 7 | Echtes Live-Theme-Toggle (DynamicResource konsequent + Resources.Clear+Add) | 4 h |
| 8 | IDialogService erzwingen (51 ViewModel-Verstoesse) | 10 h |
| 9 | WPF-UI 3 (Microsoft Fluent) integrieren — Mica/Acrylic, moderne Optik | 16 h |
| 10 | PlayerWindow.xaml.cs zerlegen (9856 Zeilen) | 24 h |

## In einfachen Worten zusammengefasst

### Was ist heute gut?

- Sicherheit ist auf gutem Niveau. Die kritischen Bugs vom letzten Audit sind fast alle gefixt (Pipe-Deadlock, Pause-Deadlock, Knick-HttpClient, Command-Injection, Path-Traversal, Sidecar-Token).
- Die KI-Pipeline ist solide gebaut. Eskalation 8B→32B funktioniert, JSON-Schema strikt, VRAM-Monitor robust, Sidecar-Lifespan sauber.
- Die Sanierungs-Wissensbasis ist aktuell (VSA-KEK 2023, last_updated April 2026) und wird produktiv genutzt — kein Stub.
- Logging und Tests sind ueberraschend konsistent (215× ILogger, kaum Console-Reste, 56 Test-Klassen in xUnit).
- Praktisch keine TODO/FIXME-Schulden im Code — sehr diszipliniert.

### Was ist die groesste Baustelle?

- Das Programm baut nicht. XAML und Code-Behind passen in DataPage.xaml.cs und ClockPickerControl.xaml.cs nicht zusammen → 140 Compile-Fehler. Das muss sofort als Erstes weg.
- Die ganze KI lebt im UI-Projekt statt in der Infrastruktur-Schicht (CLAUDE.md sagt "Thin-AI", Realitaet ist 144 zu 1). Folge: Du kannst die KI nicht ohne WPF testen, kein Headless-Lauf moeglich.
- PlayerWindow.xaml.cs hat 9856 Zeilen — und ist seit dem letzten Audit sogar gewachsen. Jeder VLC-Fix zwingt dich, ein 10'000-Zeilen-File zu durchsuchen.
- Es gibt keinen DI-Container. Alles haengt am statischen App.Services (47-mal aus 21 Dateien aufgerufen). Das sind Race-Conditions, die nur darauf warten zu passieren.

### Was ist die wichtigste Wahrheit fuer den Schlankheits-Wunsch?

- Du kannst sofort 7 GB Disk und ~25 Dateien loeswerden, ohne irgendein Risiko: Florence2-Shadow-Log (6.6 GB), _legacy/-Skripte, 5 ungenutzte WPF-Windows (CombinedOffer, OfferCalculator, CostCalculation, PriceCatalogEditor, ObservationCatalog), 5 redundante PDF-CLIs, 7 ungenutzte YOLO-Roh-Weights.
- Mittelfristig konsolidieren: VideoFullAnalysisService + QuickScanService + FullProtocolGenerationService ueberlappen mit VideoAnalysisPipelineService. Drei Self-Training-Orchestratoren koennen wahrscheinlich zwei werden. Sieben Forschungs-Services (McDropout, WeightLearning, AccuracyDashboard, ModelRegistry, AutoApproval, KbQuality, ChangeDetection) haben keinen UI-Trigger.

### Was ist die Wahrheit zur Optik?

- Schulnote 4.4 — solide Substanz, aber technisch unfertig. Die Software wirkt nach Eigenentwicklung, nicht nach Industrie-Produkt wie WinCan VX. Es fehlt: ein Produktlogo, ein App-Icon, eine Icon-Library, ein zentraler DataGrid-Style, Spacing-Tokens, PerMonitor-DPI und ein echtes Live-Theme-Toggle. Mit 50–70 Stunden gezielter UI-Arbeit waere "professionell" erreichbar.

## Empfohlener Fahrplan (in Reihenfolge)

| Phase | Inhalt | Aufwand | Wirkung |
|---|---|---|---|
| 0 — heute | Build reparieren (XAML-Fehler), kbHttp-Leak, SafeFireAndForget | 1 Tag | App kompiliert wieder, keine offenen STAB-Bugs |
| 1 — diese Woche | Slim-Down Quick-Wins (7 GB + 25 Dateien) | 1 Tag | Repo schlanker, Kopfraum frei |
| 2 — naechste Woche | DamageClassesPromptFull aktivieren, Embedding-Versions-Tag, FK-Constraints | 1 Tag | KI-Qualitaet messbar besser, KB migrationssicher |
| 3 — naechste 2 Wochen | UI-Quick-Wins: app.manifest, Spacing-Tokens, DataGrid-Style, PageHeader, Hex-Hardcodes raus | 16 h | Optisch sichtbarer Sprung |
| 4 — Monat 1 | IDialogService-Pflicht + ffmpeg-Konsolidierung (15 Stellen) | 2 Tage | Konsistenz dauerhaft, ViewModels testbar |
| 5 — Monat 2 | DI-Container einfuehren, ServiceProvider zerlegen | 1 Woche | Architektur tragfaehig fuer KI-Migration |
| 6 — Monat 3+ | PlayerWindow.xaml.cs in Partials zerlegen, KI-Schicht schrittweise nach Application/Infrastructure | langfristig | CLAUDE.md-Vorgaben endlich erfuellt |
