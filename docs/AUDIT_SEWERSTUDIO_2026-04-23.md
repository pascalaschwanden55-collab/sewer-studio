# Gesamt-Audit SewerStudio — 2026-04-23

**Fokus:** Sicherheit · Stabilitaet · Architektur
**Methode:** Drei parallele Audit-Agents, konsolidiert und priorisiert.
**Ziel-Projekt:** `c:\Sewer-Studio_KI_4.2` · Branch `feature/pdf-import-beobachtungen`

> **Update 2026-04-25 — Erweiterung durch externen Zweitaudit:** Vier weitere
> Befunde aufgenommen (SEC-C1 UI-Pfad-Containment, SEC-H5 Sidecar-Auth,
> erweiterte H1-Liste mit Pdf*-Extractors + SdfToSqliteConverter-Drain,
> STAB-M9 Tests-auf-absolute-Pfade). Roadmap um die Sofort-PRs 1-4 ergaenzt.
> Siehe Anhang am Ende des Dokuments.

---

## Executive Summary

Das Projekt ist **funktional stabil, aber strukturell angespannt**. Die Sicherheitslage ist fuer eine Single-User-Desktop-App unproblematisch — keine Remote-Exploits moeglich, aber fuenf Stellen mit Command-Injection-Risiko ueber manipulierte Dateinamen. Die Stabilitaets-Befunde sind die dringendsten: vier konkrete Bugs, die im Betrieb bereits Schaden anrichten (Knick-Erkennung ist tot, Feedback-Schreibzugriffe race-anfaellig, Batch-Deadlock bei Pause, ffmpeg-Pipe-Deadlock). Architektonisch ist das formale Schichten-Modell sauber auf csproj-Ebene — aber **inhaltlich** lebt die gesamte KI-Logik im UI-Projekt, entgegen CLAUDE.md. Dazu 10 Dateien mit mehr als 1800 Zeilen, wobei `PlayerWindow.xaml.cs` mit 9'494 Zeilen heraussticht.

**Dringlichkeit:**
- **~1 Arbeitstag Quick-Wins** beheben 80 % der akuten Probleme (Command-Injection, File-Append-Races, Knick-HttpClient, ffmpeg-Drain, CLAUDE.md-Sync).
- **Mittelfristige Umbauten** (Service-Locator → DI, AI-Schicht verschieben, PlayerWindow aufsplitten) sind teuer, aber loesen die strukturellen Blocker fuer Testbarkeit und Weiterentwicklung.

---

## Top-10 Dateien nach Zeilenlaenge

| # | Datei | Zeilen | Kern-Anmerkung |
| - | ----- | -----: | -------------- |
| 1 | `UI/Views/Windows/PlayerWindow.xaml.cs` | 9'494 | VLC + AI + Measure + Protocol + Hotkeys + Overlay in einer Datei |
| 2 | `Infrastructure/HoldingFolderDistributor.cs` | 4'616 | `static` mit globalem Cache-State |
| 3 | `UI/ViewModels/Pages/DataPageViewModel.cs` | 3'241 | Import + Export + AI + Navigation; haengt an `App.Services` |
| 4 | `UI/Views/Windows/CodingModeWindow.xaml.cs` | 3'044 | Coding-Ablauf + UI + Hotkeys |
| 5 | `UI/ViewModels/Windows/TrainingCenterViewModel.cs` | 2'885 | gehoert in Application-Schicht |
| 6 | `Application/Reports/ProtocolPdfExporter.cs` | 2'856 | QuestPDF-Monolith |
| 7 | `UI/Views/Pages/DataPage.xaml.cs` | 2'442 | Code-Behind mit 9x `App.Services`-Locator |
| 8 | `UI/Ai/Pipeline/MultiModelAnalysisService.cs` | 2'162 | GPU-State-Automat (eigentlich "InferenceOrchestrator") |
| 9 | `UI/ViewModels/Windows/CostCalculatorViewModel.cs` | 1'854 | Cost-Engine mit ViewModel vermengt |
| 10 | `UI/Views/Windows/PhotoMeasurementWindow.xaml.cs` | 1'835 | Measure-Tools + UI + Calibration |

---

## CRITICAL-Befunde (sofort fixen)

Die folgenden 7 Punkte sind entweder aktive Bugs oder strukturelle Blocker.

### [STAB-C1] KnickDetection greift auf disposed HttpClient zu
- **Datei:** `src/AuswertungPro.Next.UI/Ai/VideoAnalysisPipelineService.cs:130-138`
- **Problem:** `using var knickHttp = new HttpClient { ... };` endet nach Zeile 133. Danach (Zeile 137) ruft `AnalyzeAsync` auf jedem Frame `KnickDetection.ProcessFrameAsync` auf, das den bereits disposed Client verwendet. Die Exception wird vom umgebenden `catch { }` geschluckt.
- **Folge:** **Knick-Erkennung ist im Ollama-Only-Pfad dauerhaft kaputt** — ohne Log-Hinweis.
- **Fix:** `knickHttp` ausserhalb des `if`-Blocks instanziieren und dem Video-Service als Feld uebergeben, oder `using` bis nach `AnalyzeAsync` verlaengern.

### [STAB-C2] BatchSelfTrainingOrchestrator: Pause/Cancel-Deadlock
- **Datei:** `src/AuswertungPro.Next.UI/Ai/Training/BatchSelfTrainingOrchestrator.cs:1088-1095` (analog `VideoSelfTrainingOrchestrator.cs:368-375`)
- **Problem:** `Pause()` ruft `_pauseGate.Wait()` auf `SemaphoreSlim(1,1)`. Zweiter Klick vom UI-Thread blockiert UI unendlich. `Cancel()` gibt `_pauseGate` nicht frei — Worker haengt bei CheckPauseAsync.
- **Folge:** UI-Freeze bei Doppelklick, Batch haengt wenn Nutzer waehrend Pause Abbrechen drueckt.
- **Fix:** `Cancel()` um `_pauseGate.Release()` erweitern (nur wenn paused), `Pause()` idempotent machen.

### [STAB-C3] ServiceProvider-Warmup: kbHttp + kbCtx leaken bei Startup-Exception
- **Datei:** `src/AuswertungPro.Next.UI/ServiceProvider.cs:226-245`
- **Problem:** `kbHttp`, `kbCtx`, `embedder` werden erzeugt; wirft `CheckModelConsistency()` eine Exception, uebernehmen die Objekte niemand, bleiben bis Prozess-Ende offen. Socket + SQLite-Connection.
- **Folge:** Ressourcen-Leak beim Startup-Fehler; seltener WAL-Hang bei naechstem Start mit derselben DB.
- **Fix:** Erst alle Objekte in lokale Variablen mit `using`, dann am Schluss an Felder uebergeben (Owner-Pattern).

### [STAB-C4] InspectionFrameExtractor: ffmpeg-Pipe-Deadlock
- **Datei:** `src/AuswertungPro.Next.Infrastructure/Import/WinCan/InspectionFrameExtractor.cs:229-249`
- **Problem:** Nach `Process.Start` werden stdout/stderr via `_ = ReadToEndAsync()` fire-and-forget gestartet, aber nie awaited. Fuellt ffmpeg-stderr den 64 KB-OS-Pipe-Buffer, blockiert ffmpeg → Timeout → Kill.
- **Folge:** Sporadische Timeouts bei 3000-Video-Batch, **1–3 % Frame-Verlust**, Log-Zumuellung durch UnobservedTaskException.
- **Fix:** `await Task.WhenAll(outT, errT, p.WaitForExitAsync(ct))` — wie in `QuickScanService` / `VideoFullAnalysisService` vorbildlich geloest.

### [ARCH-C1] KI-Schicht komplett im UI-Projekt (Thin-AI-Prinzip gebrochen)
- **Betrifft:** `src/AuswertungPro.Next.UI/Ai/**` (~60 Services), gesamte KI-Pipeline
- **Problem:** CLAUDE.md sagt "Thin-AI: C# fuer alle Geschaeftslogik". Tatsaechlich lebt *die gesamte* KI-Geschaeftslogik im UI-Projekt (EnhancedVisionAnalysisService, MultiModelAnalysisService, QualityGateService, DetectionAggregator, VideoAnalysisPipelineService, alle Orchestrators, alle KB-Services). `Application/Ai` hat ~8 Interfaces, `Infrastructure/Ai` genau **eine** Datei.
- **Folge:** Jeder AI-Service zieht WPF-Abhaengigkeiten. Keine Unit-Tests ohne WPF-Runtime. Kein CLI-/Headless-Host moeglich. Clean-Architecture existiert nur formal.
- **Empfehlung:** Inkrementell verschieben, beginnend mit stateless Services (DetectionAggregator, QualityGateService, VsaCodeResolver).

### [ARCH-C2] Service-Locator-Anti-Pattern: `App.Services` als globale Registry
- **Betrifft:** 16 Dateien, 42+ Treffer. Worst Offenders: `DataPage.xaml.cs` (9x), `SchaechtePage.xaml.cs` (5x), `DataPageViewModel.cs` (Feld-Initialisierer).
- **Problem:** DI-Container wird umgangen. ViewModels haengen statisch an `App.Services`. `ShellViewModel` + `DataPageViewModel` lesen im Feld-Initialisierer — **wenn Ctor vor App.OnStartup laeuft, NRE**.
- **Folge:** ViewModels sind nicht isoliert testbar. Refactorings brechen stillschweigend. Start-Race-Condition latent.
- **Empfehlung:** Ctor-Parameter-Injection. Beginnend mit den 5 ViewModels, die Feld-Initialisierer nutzen.

### [ARCH-C3] God-Class `PlayerWindow.xaml.cs` — 9'494 Zeilen
- **Betrifft:** `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml.cs`
- **Problem:** VLC-Steuerung + AI-Overlay + OSD + Protocol-Editor + Hotkeys + Measure-Tools + Frame-Annotation + Report-Rendering in einer Datei. Keine Regions, keine partial-Splits.
- **Folge:** Ein VLC-Fix zwingt, 9500 Zeilen zu scannen. Die juengsten Commits (`4ff9ab61`, `d15f64a4`, `4480a1fe` zu VLC-Cleanup) belegen empirisch die Fragilitaet.
- **Empfehlung:** Nicht Big-Bang. Stufenweise via partial-class: `PlayerWindow.Vlc.cs`, `PlayerWindow.Ai.cs`, `PlayerWindow.Measure.cs`, `PlayerWindow.Protocol.cs`. Danach ViewModel-Extraktion.

---

## HIGH-Befunde (in den naechsten 1–2 Wochen fixen)

### Sicherheit

#### [SEC-H1–H3, M4] Command-Injection via Dateinamen in fuenf Process.Start-Stellen
Identisches Muster: `$"\"{path}\""` statt `ArgumentList.Add(path)`.

- `Infrastructure/Import/WinCan/SdfToSqliteConverter.cs:97` (PowerShell)
- `Infrastructure/Import/WinCan/SdfToSqliteConverter.cs:127` (Python)
- `Infrastructure/Import/WinCan/InspectionFrameExtractor.cs:217` (ffmpeg)
- `UI/Ai/VideoFrameExtractor.cs:30` (ffmpeg)
- `UI/Ai/Pipeline/BatchPipelineService.cs:470` (ffmpeg)
- `UI/Ai/Training/VideoSelfTrainingOrchestrator.cs:479` (ffmpeg)
- `UI/Ai/Training/Services/PdfProtocolExtractor.cs:717` (Python)
- `UI/Ai/Training/Services/PdfProtocolTableParser.cs:1062` (Python)

**Problem:** Ein Dateiname mit `"` (z.B. aus UNC-Pfad oder manipulierter WinCan-DB) laesst beliebige Kommandozeilen-Parameter an ffmpeg/Python/PowerShell durchschlagen. Windows verbietet `"` in Dateinamen zwar, aber der Pfad wird aus externen Quellen zusammengebaut (WinCan-DB, XML-Projekte).
**Folge:** Mit manipulierter Projekt-Datei kann ffmpeg auf willkuerliche Pfade schreiben (`-y`), PowerShell beliebigen Code ausfuehren.
**Fix:** Auf das in diesem Repo bereits vorbildlich verwendete `psi.ArgumentList.Add(...)` umstellen (Referenz: `VideoProbeService.cs:64`, `M150MdbImportHelper.cs` mit `Arguments`-Build-Service).
**Aufwand:** ~2 h fuer alle 8 Stellen.

#### [SEC-H4] Path-Traversal via Haltungs-ID
- **Datei:** `Application/Common/ProjectPathResolver.cs:101` (`SanitizePathSegment`), angewandt in `HoldingFolderDistributor.cs:342,847,1713,1763,3831`
- **Problem:** Sanitisiert nur `Path.GetInvalidFileNameChars()` — `.` und `..` bleiben erhalten. Ein `OBJ_Key = ".."` in der WinCan-DB landet als Directory-Name, `Path.Combine(basis, "..")` eine Ebene ueber dem Zielordner.
- **Fix:** Nach Sanitisierung pruefen `if (cleaned is "." or ".." || cleaned.Contains("..")) cleaned = "UNKNOWN";` — oder `Path.GetFullPath(target).StartsWith(destRoot)` erzwingen.

### Stabilitaet

#### [STAB-H1] HttpClient-Leak in Feedback-Pipeline
- **Dateien:** `Views/Windows/PlayerWindow.xaml.cs:7614-7632`, `Views/Windows/TrainingCenterWindow.xaml.cs:219-237`
- **Problem:** `CreateFeedbackService()` erzeugt bei **jedem** User-Approve/Reject einen neuen HttpClient. Nie disposed. In langen Codier-Laeufen sind das hunderte.
- **Folge:** 500 Interaktionen → 1–2 GB Socket-Pool; TIME_WAIT-Port-Erschoepfung auf localhost.
- **Fix:** Statischen Shared-HttpClient oder IDisposable-FeedbackService.

#### [STAB-H2] `File.AppendAllTextAsync` ohne Lock auf Feedback-JSONL
- **Datei:** `Views/Windows/PlayerWindow.xaml.cs:7709, 7742` (mit parallelen Callsites in 6111, 6192, 7380, 7444)
- **Problem:** Mehrere `_ = Task.Run(SavePositiveFeedbackAsync...)` schreiben parallel auf dieselbe JSONL-Datei. Halb-geschriebene Zeilen moeglich.
- **Folge:** **Korrupte Feedback-Dateien**, spaeterer KB-Reingest verliert Eintraege.
- **Fix:** `static SemaphoreSlim` pro Datei.

#### [STAB-H3] `AppendBatchHistoryAsync` Race im Parallel-Batch
- **Datei:** `Ai/Training/BatchSelfTrainingOrchestrator.cs:1299-1304, 379`
- **Problem:** Bei `MaxParallelHaltungen > 1` parallele Schreib-Calls auf Resume-History-Datei ohne Lock.
- **Folge:** Korrupte Zeilen koennen dazu fuehren, dass Haltungen doppelt verarbeitet oder faelschlich als erledigt markiert werden.
- **Fix:** SemaphoreSlim oder Batched-Write.

#### [STAB-H4] PythonSidecarService: Harter Kill ohne Graceful-Shutdown
- **Datei:** `App.xaml.cs:217` → `Ai/PythonSidecarService.cs:190-208`
- **Problem:** Direkt `Kill(entireProcessTree: true)` ohne uvicorn-Shutdown-Signal.
- **Folge:** FastAPI-Lifespan-Hooks laufen nicht, VRAM/GPU-State selten inkonsistent, zombie-Python-Prozesse in Admin-Contexten.
- **Fix:** Vor Kill `CTRL_BREAK_EVENT` via `GenerateConsoleCtrlEvent` senden, 3 s warten, dann Kill.

#### [STAB-H5] `async void`-Timer-Ticks in PlayerWindow koennen Window-Close-Race haben
- **Datei:** `Views/Windows/PlayerWindow.xaml.cs:1546, 8603`
- **Problem:** Exception vor dem inneren `try` crasht App (async void-Exception im SyncContext).
- **Fix:** Zu Beginn `if (_player is null || _disposed) return;`.

#### [STAB-H6] Exception-Swallowing in Eval-Gesundheitspruefung
- **Datei:** `ViewModels/Windows/TrainingCenterViewModel.cs:533`
- **Problem:** `catch { }` um `KbQualityService.FindStaleCandidates()`. Versteckt SQLite-Lock-/Schema-Fehler im KB-Audit.
- **Fix:** `catch (Exception ex) { _logger?.LogWarning(ex, "Stale-Count fehlgeschlagen"); }`.

### Architektur

#### [ARCH-H1] Domain-Models mit `INotifyPropertyChanged` + `ObservableCollection`
- **Betrifft:** `Domain/Models/HaltungRecord.cs:3`, `SchachtRecord.cs:3`, `Project.cs:22-23`
- **Problem:** Domain kennt WPF/MVVM-Typen. Blockiert Headless-Use (Sidecar/CLI), Event-Handler feuern beim Laden.
- **Fix:** POCO-Records in Domain, Wrapper-ViewModels im UI. Migration feature-flag-weise.

#### [ARCH-H2] Sechs Trainings-Orchestrators mit unklarer Zustaendigkeitstrennung
- **Betrifft:** `SelfTrainingOrchestrator`, `VideoSelfTrainingOrchestrator`, `BatchSelfTrainingOrchestrator`, `InitialTrainingOrchestrator`, `YoloRetrainOrchestrator`, `QwenLoraOrchestrator`
- **Problem:** `SelfTrainingOrchestrator` (PDF-Foto) vs. `VideoSelfTrainingOrchestrator` (Video) sind bewusst getrennt. Aber `InitialTrainingOrchestrator` und `BatchSelfTrainingOrchestrator` ueberlappen in Scope. Keine einheitliche Interface-Hierarchie.
- **Folge:** Neue Features wie Protokoll-First-Modus nur in einem Pfad implementiert; Bug-Fixes laufen aus dem Synchron.
- **Fix:** Gemeinsames `ITrainingPipeline` + Strategie-Pattern.

#### [ARCH-H3] Inkonsistente Interface-Abstraktion bei KI-Kern-Services
- **Betrifft:** `Ai/EnhancedVisionAnalysisService.cs`, `Ai/Pipeline/MultiModelAnalysisService.cs`, `Ai/Pipeline/QualityGateService.cs`, `Ai/Pipeline/DetectionAggregator.cs`, `Ai/Pipeline/BatchPipelineService.cs`, `Ai/OllamaVisionFindingsService.cs`
- **Problem:** Genau die Pipeline-Kernservices, die in Tests am dringendsten austauschbar sein muessten, haben **kein** Interface.
- **Fix:** Interfaces einziehen. Dann Unit-Tests moeglich (TDD mit QualityGate, DetectionAggregator).

#### [ARCH-H4] CLAUDE.md referenziert Klassen, die nicht existieren
- `InferenceOrchestratorService` existiert nicht (real: `MultiModelAnalysisService`)
- `ClassificationService` existiert nicht
- `MeasurementService` existiert nicht
- `ReportGenerator` existiert nicht (real: `ProtocolPdfExporter`, `HaltungsDossierPdfBuilder`)
- VRAM-Zahlen weichen von Realitaet ab (8B ist 11.7 GB nicht 10 GB, 32B laeuft hybrid)
- DINO-Permanenz: CLAUDE.md sagt permanent, Code ist lazy (Phase 3.4)
**Fix:** CLAUDE.md-Sektion "Wichtige Klassen" + "AI-Pipeline" mit Audit 2026-04-19 synchronisieren.

#### [ARCH-H5] God-Classes ueber das Top-10 hinaus

Siehe Tabelle oben. Auswahl fuer schnellen Impact: `MultiModelAnalysisService.cs` (2'162) ist der GPU-State-Automat — eigentlich *der* InferenceOrchestrator aus CLAUDE.md.

---

## MEDIUM-Befunde (nice to have, kein Ausreisser)

### Sicherheit
- **[SEC-M1]** `trust_remote_code=True` bei HF-Modell-Loads in `sidecar/sidecar/models/dino_wrapper.py`, `nemotron_parse_wrapper.py`, `scripts/calibrate_florence2.py`. Wenn `sidecar/models/` auf ein Netzlaufwerk ausgelagert wird, ist RCE moeglich.
- **[SEC-M2]** LoRA-Deploy-Route in `sidecar/sidecar/routes/lora_training.py:352-400` akzeptiert ungepruefte `ollama_base_url` (SSRF-Vektor) und `model_name` (kein Regex-Filter).
- **[SEC-M3]** Dynamisches `ALTER TABLE` via String-Interpolation in `Ai/KnowledgeBase/KnowledgeBaseContext.cs:144`. Heute mit Literalen gefuettert, aber Einladung fuer spaeteren Missbrauch.

### Stabilitaet
- **[STAB-M1]** **Escalation-Swap nicht wie in CLAUDE.md:** `EnhancedVisionAnalysisService.cs:929-940` entlaedt 8B nicht vor 32B-Load. Code-Kommentar legt das als bewussten Pfad aus (RAM-Hybrid), aber CLAUDE.md weicht ab. **Entscheidung noetig:** Doku anpassen oder Swap implementieren.
- **[STAB-M2]** `ObservableCollection.Add`-Flood in Batch-Loop (`TrainingCenterViewModel.cs:1843`). Bei 3000 Haltungen × 5-20 Samples: 15'000-60'000 Dispatcher-Invokes. UI wird laggy nach Stunden. **Fix:** Zirkulaerer Buffer (max 1000 Items).
- **[STAB-M3]** `ReviewQueue.Clear() + foreach Add` statt AddRange → UI-Freeze 1-2 s bei >100 Items.
- **[STAB-M4]** `KnowledgeMirrorService` wird per `_ = new KnowledgeMirrorService(...)` erzeugt und haelt sich via `static Current`-Property am Leben. Nicht disposed → Timer leakt bis Prozess-Ende.
- **[STAB-M5]** `TakeSnapshotSafe` nicht thread-safe. Concurrent Calls (Coding-Timer + Training-Mode) koennen schwarze Snapshots erzeugen. Fix: SemaphoreSlim.
- **[STAB-M6]** CancellationToken-Inkonsistenz in `BatchSelfTrainingOrchestrator.cs:325-332`: `_isCancelled` wird nur in `Cancel()` gesetzt, nicht automatisch bei `ct.Cancel()`.
- **[STAB-M7]** `KnickDetectionService` nicht thread-safe dokumentiert.
- **[STAB-M8]** `VisionPipelineClient` kein `IDisposable` trotz eigener HttpClient-Erzeugung.

### Architektur
- **[ARCH-M1]** State-behaftete static Classes (`HoldingFolderDistributor` mit Cache, `FrameStore`, `TrainingSamplesStore`, `SelfTrainingHistoryStore`). Global State ohne Test-Reset-Moeglichkeit.
- **[ARCH-M2]** `ServiceProvider.cs`-Konstruktor tut zu viel: Filesystem-Lookups, Fire-and-Forget-Task.Run, ad-hoc HttpClient. Kein `IAsyncDisposable`. Empfehlung: `ServiceProvider.InitializeAsync()`.
- **[ARCH-M3]** `new HttpClient` an 10+ Stellen statt zentralem Factory. Anti-Pattern, aktuell nicht akut weil localhost.
- **[ARCH-M4]** Service-Naming inkonsistent: `InspectionProfileExtractor` (static) vs. `PdfProtocolExtractor` (instance); `FrameStore` (static) vs. `FewShotExampleStore` (instance).
- **[ARCH-M5]** `KnowledgeMirrorService.Current` — Singleton-Registry ausserhalb des DI.
- **[ARCH-M6]** CLAUDE.md-VRAM-Zahlen veraltet (siehe ARCH-H4).

---

## LOW / INFO

Gesammelt, weniger dringend:

- **[L1]** 4 pro-Projekt-Solution-Dateien neben Haupt-`AuswertungPro.sln` — entfernbar.
- **[L2]** `MessageBox.Show($"... {ex.Message}")` in ~20 Stellen gibt interne Pfade preis. Solo-App: OK. Bei Weitergabe: Produktivmodus-Flag mit generischer Meldung.
- **[L3]** `-ExecutionPolicy Bypass` mehrfach (kein Problem nach SEC-H1-Fix).
- **[L4]** XML-Parser ohne explizit deaktivierte DTD (`XDocument.Load`); .NET default-safe, aber nicht explizit.
- **[L5]** Einige `catch { }` in Cleanup-Pfaden ohne Logging — erschwert Debugging.
- **[L6]** `_detectionTimer.Tick` wird nicht via `-=` entfernt (GC sollte's kriegen, aber Best-Practice).
- **[L7]** Event-Subscriptions auf statische Stores nicht abgemeldet.
- **[L8]** Temp-Files mit `Guid.NewGuid()` — sicher genug.

---

## Positive Befunde (was gut ist)

- **Keine Hardcoded-Secrets, keine externen API-Keys.** appsettings/.env existieren nicht.
- **SQL durchweg parametrisiert** (`command.Parameters.AddWithValue`) oder mit Literalen.
- **Keine TypeNameHandling, kein BinaryFormatter, kein pickle.load, kein eval/exec** im Hauptcode.
- **Kein WebView2/Browser-Control** → kein klassischer XSS-Vektor.
- **Sidecar defaultet auf 127.0.0.1** — Remote-Zugriff von aussen blockiert.
- **LoRA-Deploy schuetzt `adapter_path` aktiv gegen Path-Traversal** (S1-Fix dokumentiert).
- **Global Exception Handler in App.xaml.cs** deckt Dispatcher, AppDomain, TaskScheduler sauber ab. `Interlocked`-Rekursions-Guard. Vorbildlich.
- **OllamaClient** nutzt Polly mit Retry + Circuit Breaker, `_ownsHttp`-Flag fuer Dispose.
- **VLC-Cleanup im PlayerWindow** ist nach juengsten Commits defensiv — Detach auf UI-Thread, Dispose auf ApplicationIdle.
- **BatchPipelineService** serialisiert Qwen mit SemaphoreSlim(3), CancellationLinked-Tokens pro Frame.
- **FileLoggerProvider** mit `lock (_lock)` — einziger thread-safe Log-Pfad, aber korrekt.
- **Schichten auf csproj-Ebene sauber** (`Application → Domain`, `Infrastructure → Application+Domain`, `UI → alle`), keine zyklischen References.
- **AiPlatformConfig-Prioritaet dokumentiert** (`AppSettings > Env-Var > Default`). Vorbildlich.
- **SemaphoreSlim-Locking-Hygiene** bei den Stores (TrainingSamplesStore, FewShotExampleStore, BenchmarkMetricsStore).

---

## Priorisierter Fahrplan

### Sprint 1: Quick-Wins (1 Arbeitstag, ~8 h)
Das sind Fixes mit hoher Wirkung und kleinem Aufwand. Reihenfolge nach Risiko-Reduktion:

1. **SEC-H1-H3 / M4: `ArgumentList.Add` fuer 8 Process.Start-Stellen** (~2 h) — eliminiert gesamte Command-Injection-Kaskade.
2. **STAB-C4: ffmpeg-Drain in InspectionFrameExtractor fixen** (~20 min) — beseitigt 1-3 % Frame-Verlust im Nachtbatch.
3. **STAB-C1: KnickHttp-Lifetime fixen** (~10 min) — Knick-Detection wieder aktivieren.
4. **STAB-C2: Pause/Cancel-Semantik** (~30 min) — kein UI-Freeze, kein Batch-Haenger.
5. **STAB-H2/H3: File-Append-SemaphoreSlim** (~1 h) — keine korrupten Feedback/History-Dateien.
6. **STAB-H1: Feedback-HttpClient-Sharing** (~30 min) — kein Socket-Leak bei langen Sessions.
7. **SEC-H4: Path-Traversal-Check in `SanitizePathSegment`** (~15 min).
8. **ARCH-H4: CLAUDE.md synchronisieren** (~45 min) — jede zukuenftige Claude-Sitzung profitiert sofort.
9. **ARCH-L1: Doppelte `.sln` entfernen** (~5 min).

Nach Sprint 1: alle aktiven Bugs beseitigt, Security auf sauberem Stand.

### Sprint 2: Strukturelle Fixes (1 Woche)
10. **STAB-C3: ServiceProvider Resource-Owner-Pattern** (~2 h)
11. **ARCH-C2: `App.Services`-Feld-Initialisierer entfernen** (~3 h) — fuer 5 ViewModels. Eliminiert Start-Race-Condition.
12. **ARCH-H3: Interfaces fuer QualityGateService, DetectionAggregator, MultiModelAnalysisService** (~4 h) — schaltet TDD fuer die Pipeline-Kerne frei.
13. **ARCH-M3: HttpClient-Zentralisierung** (~2 h) — ein Client pro Host im DI.
14. **STAB-M1: Escalation-Swap-Entscheidung** (Doku anpassen oder Swap implementieren, 2-4 h).
15. **STAB-M2/M3: UI-Performance-Fixes** (zirkularer Buffer + AddRange, ~2 h).

Nach Sprint 2: Pipeline-Kern testbar, Start stabil, UI schnell bei langen Laeufen.

### Sprint 3: Groessere Umbauten (2-4 Wochen, optional)
16. **ARCH-C1: AI-Schicht nach Infrastructure/Application verschieben** — in Wellen, stateless Services zuerst.
17. **ARCH-C3: PlayerWindow aufsplitten** — erst partial-class, dann ViewModel-Extraktion.
18. **ARCH-H2: Trainings-Orchestrator-Konsolidierung** — gemeinsames Interface + Strategie-Pattern.
19. **ARCH-H1: Domain-POCO-Migration** — INotifyPropertyChanged raus aus Domain.

Diese Umbauten sind keine Quick-Wins, aber sie loesen die strukturellen Blocker fuer langfristige Wartbarkeit.

---

## Anhang: Audit-Methodik

Drei parallele Agent-Instanzen mit scharfen Scopes:

- **Security-Agent:** OWASP-Top-10 an Desktop-Realitaet angepasst, Fokus auf Command-Injection (Process.Start), Path-Traversal, Deserialization, Hardcoded-Secrets, SQL-Injection, Temp-File-Races, API-Aufrufe, XAML-Injection.
- **Stabilitaets-Agent:** Exception-Swallowing, IDisposable, Async/Await-Pitfalls, Thread-Safety, Event-Leaks, Resource-Cleanup, Cancellation, WPF-Dispatcher, Crash-Recovery.
- **Architektur-Agent:** Schichten-Verletzungen (csproj + usings), DI-Struktur, God-Classes (wc -l), Interfaces, zyklische Dependencies, Naming-Konsistenz, Static-vs-Instance, UI/Domain-Entkopplung, Config-Management, Duplizierung, Sidecar-Integration, CLAUDE.md-Sync.

Alle drei Agents: read-only, keine Code-Aenderungen, strukturierte Output mit Severity + Datei:Zeile + Fix-Empfehlung.

Konsolidierung durch Deduplikation (z.B. PlayerWindow-Groesse taucht in Stability + Architecture auf), Priorisierung nach tatsaechlichem Betriebsrisiko (nicht Schweregrad-Dogmatik), Fahrplan-Erstellung nach Aufwand/Nutzen-Verhaeltnis.

---

## Anhang: Erweiterung durch Zweitaudit 2026-04-25

Externer Zweitaudit hat vier Befunde aufgedeckt, die im Erst-Audit fehlten oder
zu schwach gewichtet waren. Hier konsolidiert mit den im Sprint-1 bereits
gefixten Punkten.

### Neu aufgenommen

#### [SEC-C1] UI-Pfade loesen Projektdatei-Pfade ohne Containment-Check auf
- **Dateien:**
  - `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:1435-1438`
  - `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:2321-2328` (`ResolveDossierPhotoPath`)
  - `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:2348-2375` (`AddResolvedPdf`)
  - `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs:2443-2460` (`ResolveExistingPath`)
- **Problem:** `Path.GetFullPath(Path.Combine(projectDir, path))` ohne Pruefung
  ob der finale Pfad noch im Projekt-Root liegt. Absolute Pfade werden direkt
  akzeptiert. Eine manipulierte Projektdatei (z.B. via Mail-Anhang oder
  Filesharing geoeffnet) kann beliebige lokale Dateien als
  Dossier-Foto/PDF/Frame referenzieren.
- **Folge:** Lokale Dateien (z.B. `C:\Users\...\Dokumente\...`) landen in PDF-Exporten.
  Bei zukuenftigem File-Sharing-Feature: Datenleck.
- **Fix:** Eine zentrale `ProjectFilePathPolicy` einfuehren, die `ProjectPathResolver`
  als Backend nutzt. Alle UI-Aufloesungen darauf umstellen. Absolute Pfade
  ablehnen, ausser explizit als `ExternalFileReference` modelliert.

#### [SEC-H5] Sidecar-Endpunkte ohne Authentisierung — eskaliert von SEC-M2
- **Dateien:**
  - `sidecar/sidecar/main.py:166-190` (App-Aufbau, keine Auth)
  - `sidecar/sidecar/routes/training.py:359-431` (`/training/export-yolo`)
  - `sidecar/sidecar/models/yolo_wrapper.py:76-84` und `:378-385` (`reload_model`)
  - `sidecar/sidecar/routes/lora_training.py:352-399` (LoRA-Deploy)
- **Problem:** Lokales Listening auf 127.0.0.1 ist by-design akzeptiert, **aber:**
  - Browser-CSRF/SSRF-Angriffe auf `localhost:8100` sind moeglich (Browser
    sendet automatisch Cookies/Origin-Requests von beliebigen Tabs).
  - `/admin/reload_model` mit beliebigem `.pt`-Pfad = **PyTorch-RCE-Vektor** (das
    `.pt`-Format ist Pickle-basiert, kann beliebigen Code ausfuehren).
  - Andere lokale Prozesse (Malware) erreichen den Sidecar trivial.
- **Folge:** Wer auch immer lokal Code ausfuehren kann, kann den Sidecar dazu
  bringen, beliebige `.pt`-Dateien zu laden = beliebigen Python-Code ausfuehren.
- **Fix:** Lokales Bearer-Token, beim ServiceProvider-Start generiert (z.B.
  GUID), als `OLLAMA_SIDECAR_TOKEN` env-var an Sidecar uebergeben. Alle
  Endpunkte ausser `/health` pruefen Header `X-Sidecar-Token`.
- **Bonus:** `model_path` nur aus Whitelist-Verzeichnissen (`models/`, `runs/train/`).

#### [STAB-H1-erweitert] Pipe-Drain-Bugs in weiteren Process.Start-Stellen
Sprint 1 hat 4 Stellen behoben (Commit 764533e3). Der Zweitaudit zeigt **fuenf
weitere** Stellen mit demselben Muster:

- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfOcrExtractor.cs:181-190`
- `src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfTextExtractor.cs:85-104`
- `src/AuswertungPro.Next.UI/Ai/Training/Services/PdfProtocolExtractor.cs:322-340` (pdftotext)
- `src/AuswertungPro.Next.UI/Ai/Training/Services/PdfProtocolExtractor.cs:730-740` (PyMuPDF — Kommentar luegt, Code drained synchron)
- `src/AuswertungPro.Next.Infrastructure/Import/WinCan/SdfToSqliteConverter.cs:92-151` (PowerShell + Python — `ReadToEnd()` synchron vor `WaitForExit()`)
- **Fix:** Zentraler `ProcessRunner` (siehe Roadmap PR3), der ArgumentList +
  asynchroner stdout/stderr-Drain + harter Timeout + Tree-Kill kapselt. Alle
  obigen Stellen darauf umstellen.

#### [STAB-H7] YOLO-Export-Base64-Bombe (entdeckt in Session 23.04, gefixt 23.04-spaet)
- **Datei:** `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs:1284-1394`
- **Problem:** `ExportYoloAsync` laedt **alle** Samples gleichzeitig als
  Base64-Strings in eine `List<TrainingExportSample>`, serialisiert sie als
  **ein** riesiges JSON, sendet das in einem HTTP-Request. Bei 10'000+ Samples
  → 10+ GB Peak-Memory → OOM.
- **Folge:** `Insufficient memory to continue the execution` bei Datasets > ein paar tausend Samples.
- **Fix (bereits angewandt, uncommitted in TrainingCenterViewModel.cs):**
  Sidecar-Pfad deaktiviert, immer lokaler `File.Copy`-Pfad. Bestaetigt durch
  erfolgreichen Lauf mit 12'667 Samples bei stabilem RAM (27 % statt 77 %).
- **Nachholen:** Code-Aenderung als Commit nachholen. Langfristig: Sidecar-Pfad
  korrekt batched implementieren oder ganz entfernen.

#### [STAB-M9] Pipeline-Tests schreiben auf absolute Maschinenpfade
- **Dateien:**
  - `tests/AuswertungPro.Next.Pipeline.Tests/QwenModelComparisonTest.cs:36, 133, 240`
  - `tests/AuswertungPro.Next.Pipeline.Tests/SdfProfileExtractionTest.cs:26-32`
- **Problem:** Tests schreiben nach `C:\KI_BRAIN\...` — `dotnet test` schlaegt
  auf Maschinen ohne dieses Verzeichnis fehl (3 Tests rot in CI).
- **Fix:** GPU-/Eval-Tests mit `[Trait("Category", "GpuEval")]` markieren.
  Andere Tests auf `Path.GetTempPath()` oder `Environment.GetEnvironmentVariable("KI_BRAIN_ROOT")`
  umstellen mit Skip wenn nicht gesetzt.

### Eskalation bestehender Befunde

- **SEC-M2 → SEC-H5:** Sidecar-Auth war zu niedrig gewichtet. Eskaliert auf HIGH wegen PyTorch-RCE-Vektor.
- **STAB-H4 (Sidecar-Kill) bestaetigt:** Audit 25.04 nennt zusaetzlich den `Arguments`-String-Concat in `PythonSidecarService.cs:72-80`. Wird gemeinsam in PR3 mit ProcessRunner gefixt.

### Aktualisierte Roadmap

**Sprint 2 = die vier PRs aus Audit 25.04:**

| PR | Inhalt | Dauer | Befunde |
|---|---|---|---|
| **PR1** | Zentrale Projektpfad-Policy + UI-Aufloesungen haerten | 2-3 h | SEC-C1 |
| **PR2** | Sidecar Bearer-Token-Auth + Path-Root-Hardening | 3-4 h | SEC-H5, SEC-L2 |
| **PR3** | Zentraler `ProcessRunner` + alle Pdf*/SdfConverter/SidecarStart umstellen | 4-5 h | STAB-H1-erweitert, STAB-H4 |
| **PR4** | Tests auf relative/Temp-Pfade + GpuEval-Trait | 1-2 h | STAB-M9 |

Sprint 2 = ~12 h Arbeit, beseitigt alle vom Zweitaudit aufgedeckten Risiken.

### Was vom Zweitaudit ueberinterpretiert ist

- **L1 Mojibake:** Die Schreibweise `ae/oe/ue` in C#-Kommentaren ist **bewusste
  Konvention** aus CLAUDE.md (Cross-Encoding-Lesbarkeit fuer git-bash, Editor
  ohne UTF-8). Kein Bug, keine Aktion. Echte Mojibake (`ã±`, `Ã¼` etc.) waere
  zu fixen — solche existieren im Repo nicht.
