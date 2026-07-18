# CODEBASE-KARTE — SewerStudio

> **Zweck:** Architektur-Landkarte — welche Schicht ruft welche, welche Klasse macht was, wie Verträge zusammenhängen. Stand: 2026-07-18 (jeder Klassenname per `rg` in `src/`, `sidecar/`, `tools/` bestätigt).
> **Getrennte Quellen, kein Kopieren:** konkrete **Werte** (Pfade, Sidecar-Routen, Modellnamen, `active.json`, Wissensordner-Auflösung, VSA-Katalogdatei) stehen in `docs/SYSTEM-FAKTEN.md`; **Regeln/Prinzipien** (Thin-AI, Sprache, Arbeitsweise) in `CLAUDE.md`. Diese Karte beschreibt nur die Struktur.

---

## 1. Schichten

Vier .NET-Projekte plus Sidecar und Tools. Abhängigkeitsrichtung strikt von oben nach unten: UI → Infrastructure → Application → Domain. Application kennt nur Verträge (Interfaces), Infrastructure liefert die Umsetzung.

| Schicht | Projekt | Verantwortung | Kennt |
|---|---|---|---|
| Domain | `AuswertungPro.Next.Domain` | Fachmodelle, Feldschlüssel; kein Datei-I/O | nichts |
| Application | `AuswertungPro.Next.Application` | Interfaces, Verträge, reine Fachregeln (Policies, Validatoren, JSON-Verträge) | Domain |
| Infrastructure | `AuswertungPro.Next.Infrastructure` | Datei-I/O, Import, SQLite, HTTP-Sidecar, KI-Dienste, Backup | Application, Domain |
| UI | `AuswertungPro.Next.UI` | WPF, ViewModels, Fenster, Renderer, **Zusammensetzung der Dienste** | alle darunter |
| Sidecar | `sidecar/sidecar/` | lokaler FastAPI-Dienst für YOLO/DINO/SAM (siehe SYSTEM-FAKTEN.md, Abschnitt 3–4) | HTTP-Vertrag |
| Tools | `tools/*` | eigenständige CLI-Werkzeuge (u. a. `StageAExporter`, `TrainingDataInventory`) | eigene Referenzen |

Regel aus CLAUDE.md: neue Fachlogik als eigener Service mit Interface in Application/Infrastructure, nie in UI-Code oder Sidecar. UI ruft ViewModel/Service, nie direkt Infrastruktur.

---

## 2. DI / ServiceProvider-Aufbau

Zentrale Zusammensetzung: `src/AuswertungPro.Next.UI/ServiceProvider.cs`. Muster: **erst alle Dienste einmalig bauen, dann registrieren** — kein verstreutes `new` in ViewModels/Fenstern.

- **`ServiceProviderRegistrationMap`** (UI) — reine Zuordnung bereits gebauter Dienste zu ihren Vertragstypen (aktuell 126 Verträge). Erzeugt selbst nichts; `ServiceProvider.cs` ruft die Map erst nach vollständigem Aufbau auf.
- **`ServiceProvider.FullBackup.cs`** / **`ServiceProvider.TrainingYoloExport.cs`** — partielle Ergänzungen, die die Subsystem-Kompositionen anbinden und deren öffentliche Zugriffe unter ihren Interfaces registrieren.
- **`FullBackupComposition`** (Infrastructure) — baut Zielmarker, SQLite-Schnappschuss, Manifestprüfung und `FullBackupService` einmalig. Die UI liefert nur die Quellenfunktion (`IFullBackupSourcesProvider`). `BackupTargetGuard.UseMarkerGuard` darf beim zentralen Aufbau **nicht** verwendet werden; der Marker geht direkt an `FullBackupService`.
- **`TrainingYoloExportComposition`** (UI) — dünne WPF-Hülle um `TrainingYoloExportRuntime`; `TrainingYoloExportDependencies` reicht nur den Coordinator an Fenster/ViewModel.

Gezielte statt globale Injektion: `MediaConflictsPageViewModel` erhält `ISafeShellOpenService` und `IExplorerRevealService` aus dem ServiceProvider — dort kein `Process.Start` und keine statische Shell-Fassade.

Die aktuellen Fabriken arbeiten mit `AiRuntimeSettings` (nicht dem entfernten `AiRuntimeConfig`).

### Wichtige DI-Zugriffe (Interface hinter dem Zugriff)

| Zugriff | Interface / Zweck |
|---|---|
| `Projects` | `IProjectRepository` |
| `PdfImport` … `KinsImport` | fünf Import-Verträge (siehe §4) |
| `StoredImportFiles` / `StoredImportFilePaths` | Schreiben / Auflösen gespeicherter Importquellen |
| `ImportFileStaging` / `ImportMediaDistribution` | `IImportFileStagingService` / `IImportMediaDistributionService` |
| `KnowledgePaths` / `KnowledgeRoot` | einmal aufgelöster KB-Ort (Auflösung: SYSTEM-FAKTEN.md, Abschnitt 2) |
| `Retrieval` | optionale KB-Suche, kann `null` sein |
| `AiSettings` | `IAiPlatformSettingsResolver` |
| `GpuModels` | `IGpuModelSelector` |
| `TrainingSamples` | `ITrainingSampleStore` |
| `TrainingDataInventory` | `ITrainingDataInventoryService` |
| `TrainingExportRegistry` | `ITrainingExportRegistryStore` |
| `TrainingYoloExportCoordinator` | `ITrainingYoloExportCoordinator` |
| `VsaYoloClasses` / `TrainingYoloClasses` | `IVsaYoloClassMapStore` (erweiterbar) / `ITrainingYoloClassMapStore` (strikt lesend) |

---

## 3. Domänenmodell

- **`HaltungRecord`** (Domain) — zentrales Fachmodell einer Haltung. Enthält `Fields`, `FieldMeta`, `VsaFindings`, `ProtocolEntry`, `Protocol`. Besitzt **keine** `DeepClone()`-Methode.
- **`FieldKeys`** (Domain) — kanonische Feldschlüssel. Zugriff immer über `record.GetFieldValue(FieldKeys.X)` / `SetFieldValue(...)`, nie über neue String-Literale.
- **`ImportStats`** — Ergebniszähler eines Imports: `Found`, `Created`, `Updated`, `Errors`, `Uncertain`, `Messages`.

Fachdomäne (Haltung, Schacht, DN, VSA-KEK-Codegruppen BC/BA/BB, Severity 1–5, Uhrlage, Punkt- vs. Streckenschaden): Definitionen in CLAUDE.md; konkrete Code-Zuordnungen in SYSTEM-FAKTEN.md, Abschnitt 7.

---

## 4. Import-Verträge & Workflow

Fünf manuelle Wege — **PDF, XTF, WinCan, IBAK, KINS** — mit einheitlicher Signatur (`Result<ImportStats>` je Weg, optionaler `ImportRunContext`).

**Ablauf-Steuerung (UI-Schicht, dünn):**
- **`ImportManualWorkflowController`** — kapselt die fünf Wege; kennt weder ServiceProvider noch Shell/ViewModel.
- **`ImportRunWorkflowController`** — bindet Projektinstanz, normalisierten Projektpfad und Berichtsordner; prüft nach jedem asynchronen Abschnitt Projektidentität und Abbruch neu. `ImportPageViewModel` verbindet nur Befehle und UI-Zustand.
- **`ImportPostProcessingController`** — fehlertoleranter PDF-Scan; bewusst **getrennt** vom manuellen PDF-Stapellauf (andere Fehlerregeln).

**Transaktionssicheres Staging (Infrastructure + Application-Vertrag):**
- Der Echtlauf legt eine `IImportFileStagingSession` in `ImportRunContext.FileStaging` ab.
- **`StoredImportFileService`** und der `IImportMediaDistributionService` (Umsetzung `MediaDistributionService`) schreiben geprüfte Kopien zuerst neben die Projektdatei unter `.import-staging/<Lauf>`. `Publish` nach den Nacharbeiten, `Accept` erst nach `ReplaceProject`; `Dispose` nimmt nur vom Lauf neu angelegte, unveränderte Dateien zurück. Gleichheit wird am Inhalt geprüft, nie werden vorhandene/wiederverwendete Dateien gelöscht.
- **`ImportFileStagingService`** bereitet projektbezogene Kopien vor und nimmt sie bis zur Projektübernahme zurück.

**Lesen gespeicherter Importlisten:**
- **`StoredImportFilePathResolver`** liest Listen über `StoredImportFileRegistry.Load`, prüft zuerst den echten Projekt-Root, dann alte `Projektdateien\Imports`-Ablagen. Fehlende/unsichere/doppelte Treffer werden verworfen.
- **`VsaPageViewModel`** und **`InspectionProtocolFileLocator`** dürfen diese Pfadlogik **nicht** duplizieren; sie erhalten dieselbe Resolver-Instanz.
- **`ImportFileStoreService`** bleibt eine dateifreie Kompatibilitätsfassade (Leser-Rückfall für Altbestände erhalten).

**Bekannte Grenze:** Ein Prozess-/Stromabsturz während der Sitzung hat noch kein dauerhaftes Journal; `projekt.json`-Stand, additives XTF-Rohdatenarchiv und Ein-Knopf-Import sind noch nicht Teil einer absturzsicheren Gesamttransaktion. Diese Erweiterung gehört in Infrastructure, nicht ins ViewModel.

---

## 5. KI-Pipeline

**Fluss:** C# steuert alles; der Sidecar liefert nur Inferenz.

```text
UI/Service  →  VideoAnalysisPipelineService | SingleFrameMultiModelService | VideoFullAnalysisService
            →  VisionPipelineClient (HTTP an Sidecar, X-Sidecar-Token)
            →  Sidecar: YOLO → DINO → SAM → Quantifizierung → optional Qwen
            →  C#: VSA-Code-Mapping → zeitliche Zusammenführung → QualityGate
```

| Klasse | Schicht | Verantwortung |
|---|---|---|
| `VideoAnalysisPipelineService` | Infrastructure | wählt Multi-Model- oder Ollama-Pfad für Videoanalyse |
| `MultiModelAnalysisService` | Infrastructure | gemeinsame Bildanalyse (YOLO/DINO/SAM/Qwen), framebasierter Dedup |
| `VideoFullAnalysisService` | Infrastructure | Vollanalyse-/Rückfallpfad mit eigener Dedup-Logik |
| `SingleFrameMultiModelService` | Infrastructure | Live-Einzelframe YOLO/DINO/SAM |
| `VisionPipelineClient` | Infrastructure | C#-HTTP-Client zum Sidecar |
| `AiStartupOrchestrator` | Application | lädt konfigurierte Ollama-Modelle vor, ruft danach `/warmup` |
| `GpuModelSelector` | Infrastructure | GPU-Automatik der Modellwahl (`IGpuModelSelector`) |
| `QualityGateService` | Infrastructure | Green/Yellow/Red aus verfügbaren Evidence-Signalen |
| `FullProtocolGenerationService` | Infrastructure | KI-Befunde → Protokolleinträge |
| `LiveFindingSummaryBuilder` | Application | Aufbereitung der Live-Befundzusammenfassung |

Modellstart, VRAM-Budget, Modellnamen und Sidecar-Routen: siehe SYSTEM-FAKTEN.md, Abschnitte 3–5. Kein ByteTrack/OC-SORT, kein echtes Multi-Object-Tracking im HEAD.

---

## 6. Merge- / Dedup-Semantik

**Framebasiert in C#**, kein Tracking.

- **`TemporalFindingDeduplicator`** — führt Videobefunde über Frames zusammen.
- **`TemporalCodeVotingService`** — zeitliche Code-Abstimmung über mehrere Frames.
- **`TrainingSampleGenerator`** — vermeidet Duplikate über die kanonische `Signature`.

Es gibt **keinen** separaten `DetectionAggregator` und keinen produktiven `KbDeduplicationService` (siehe §14). `UpdateActive` ist keine Dedup-Wahrheit.

---

## 7. KnowledgeBase & Laufzeit-Kontextwege

- **`KnowledgeBaseManager`** (Infrastructure) — SQLite-KB: blockiert Eval-kontaminierte Samples, akzeptiert nur indexwürdige, menschlich bestätigte Daten, schreibt per UPSERT nach `SampleId`.
- **`RetrievalService`** — Suche über Kosinus-Ähnlichkeit, prüft das Embedding-Modell; kann als `Retrieval`-Zugriff `null` sein.
- **`KnowledgeBasePathService`** — löst den KnowledgeRoot auf (Reihenfolge: SYSTEM-FAKTEN.md, Abschnitt 2).
- **`TrainingSamplesStore`** (`ITrainingSampleStore`) — JSON-Trainingssamples speichern/mergen.

**Zwei produktive Few-Shot-Kontextwege** (liefern nur Prompt-Beispiele, trainieren keine Modellgewichte):
1. Ähnliche bestätigte Fälle aus `KnowledgeBase.db` über `RetrievalService`.
2. Freigegebene Protokolleinträge getrennt über **`ProtocolTrainingFileStore`** (`protocol_training.json`).

Der frühere bildbasierte Few-Shot-Weg ist entfernt; `fewshot_examples.json`/`fewshot_images` sind nur noch unveränderte Legacy-Daten und werden nie wieder als Prompt-/Trainingsquelle angeschlossen.

---

## 8. TrainingDataInventory (AP 0.1)

Rein lesendes Inventar der Teacher-/Trainingsquellen; verändert nie Annotationen, Bilder oder Pfade. Bewusst getrennt in Verträge (Application), Datei-I/O (Infrastructure) und CLI (Tool).

**Application** (`Ai/Training/Inventory/`):

| Klasse | Verantwortung |
|---|---|
| `ITrainingDataInventoryService` / `TrainingDataInventoryRequest` | Vertrag; `InspectRuntimeSnapshotAsync` liefert in einem Live-Scan Bericht, typisierte Daten und Schutz-Snapshot |
| `TrainingDataInventoryRuntimeSnapshot` | der Live-Snapshot (einzige Sample-Wahrheit für Auswahl/Plan; nicht als zweite Datei gespeichert) |
| `TeacherInventoryPolicy` / `TeacherInventoryTriagePolicy` / `TeacherInventoryReasonPolicy` | verteilte Teacher-Regeln |
| `TrainingInventorySummaryBuilder` | baut die abgeleitete Zusammenfassung |
| `TrainingInventoryReportValidator` | strenger Vertrag (Schema 2.2): Pfad-, Triage-, Quellen-, Zusammenfassungsregeln vor Schreiben / nach Lesen |
| `TrainingDataInventoryJson` | strikter JSON-Vertrag (String-Enums, Pflichtfelder, lehnt unbekannte Felder ab) |
| `TrainingInventoryExitPolicy` | Erfolg nur bei je genau einer aktuellen Teacher- und Sample-Quelle, ohne Error-Issue |

**Infrastructure** (`Ai/Training/Inventory/`):

| Klasse | Verantwortung |
|---|---|
| `TrainingDataInventoryService` | rein lesende Orchestrierung |
| `TrainingInventorySourceReader` | stabile Datei-Schnappschüsse, protokolliert SHA-256, Größe, Änderungszeit |
| `TrainingInventoryPathResolver` | trennt Existenz, Schutz, Reparaturvorschlag, Hash |
| `TrainingInventoryFileEnumerator` | folgt keinen Links/Junctions/Reparse-Points |
| `TrainingInventoryEvalProtectionReader` | verlangt je Eval-Set `frozen=true`, prüft Bild-Hashes und Manifest, je Eval-Bild genau ein Kandidat |
| `TrainingInventoryIssueCollector` | hält die Orchestrierung frei von Meldeformatierung |
| `TrainingInventoryReportOutputPolicy` | prüft das Ziel vor Scan und vor Schreiben; Eval-Root bleibt immer geschützt |

**CLI:** `tools/TrainingDataInventory/` — Bericht + SHA-256 unter `<KnowledgeRoot>\training\reports\`. Eval-Schutz arbeitet fehlersicher: unvollständiger Schutz gibt keine Train/Val-Freigabe.

---

## 9. YOLO-Detect-Klassenkarte v2 (AP 0.2, Freigabe noch offen)

Teacher-Karte und Trainingskarte sind absichtlich getrennt:

| Klasse | Rolle |
|---|---|
| `IVsaYoloClassMapStore` / `VsaYoloClassMapFileStore` | Teacher-Karte; `GetClassId` liest strikt, nur der Live-Teacher darf `GetOrAddClassId` erweitern (Konstruktoroption kann sperren) |
| `VsaYoloClassMapDocumentWriter` | JSON-Karte ist verbindlich, `classes.txt` nur abgeleitet; scheitert JSON-Schreiben, wird `classes.txt` wiederhergestellt/entfernt |
| `ITrainingYoloClassMapStore` / `TrainingYoloClassMapFileStore` | strikt lesender, unveränderlicher Snapshot für den Detect-Export: prüft Version 2, exakt 14 feste Klassen/IDs, echten VSA-Manifest-Hash, vier Quell-Hashfelder, `entry_counts` und Quellenreihenfolge |
| `CodingFindingCodeResolver` + `VsaCodeResolver` → `YoloClassVsaMapper.ToPersistableVsaCode` | Rückmapping der Detect-Klasse `BBD_boden` auf den gültigen `BBDZ`, nie den nackten `BBD` (Integrationstest schützt die Kette) |

Nur der VSA-Hash wird beim Lesen gegen die echte Datei neu berechnet; die drei übrigen Hashes sind Auditwerte der Erzeugung. Versionierte Vorlagen unter `training/class_maps/` werden beim Build nach `Data/Training/` kopiert; die Migrationskandidaten stehen bis zur fachlichen Abnahme bewusst auf `pending`.

---

## 10. Plan-gesteuerter YOLO-Export (AP 0.3, technisch umgesetzt, produktiv gesperrt)

Verbindlicher Datenfluss — genau **ein** unveränderlicher Plan je Exportbefehl; Sidecar und lokaler Ausführer treffen keine eigene Klassen-/Split-/Datei-Entscheidung.

```text
TrainingCenterViewModel → dünner TrainingYoloExportWorkflow → TrainingYoloExportRuntime.CreateHybrid
tools/StageAExporter                                        → TrainingYoloExportRuntime.CreateLocal
beide → ITrainingYoloExportCoordinator (fest gebundene Roots)
      → export_registry_v1.json (approved)         [ITrainingExportRegistryStore / TrainingExportRegistryFileStore]
      → TrainingDataInventoryRuntimeSnapshot (ein Live-Scan)
      → strikt gelesene class_map v2
      → ITrainingExportPlanService                  erzeugt genau einen Plan
      → ITrainingExportExecutionService             Sidecar ODER lokaler Ausführer
      → ITrainingExportCompletionService            markiert nur bestätigte TrainingSamples
```

| Klasse | Schicht | Verantwortung |
|---|---|---|
| `TrainingYoloExportRuntime` | Infrastructure | gemeinsamer Aufbaupunkt; `CreateHybrid` (WPF, Sidecar + lokaler Rückfall), `CreateLocal` (CLI). Roots einmal gebunden |
| `ITrainingYoloExportCoordinator` / `TrainingYoloExportCoordinator` | App / Infra | besitzt Auswahl, Inventar, Klassenkarte, Plan, Ausführung, Abschluss; Root-Pfade nicht austauschbar |
| `ITrainingExportPlanInputBuilder` / `TrainingExportPlanInputBuilder` | App / Infra | baut aus dem Live-Snapshot den einzigen Planner-Input; akzeptiert Teacher-Daten nur über AP-0.1-`Disposition` |
| `TrainingExportPlanService` | Application | legt Klassen-IDs, Haltungssplit, Ausschlüsse, Dateinamen (`img_<sha256>.<endung>`), Labels und SHA-Zusammenführung fest; pfadfrei |
| `ITrainingExportPlanLocalExecutor` / `TrainingExportPlanLocalExecutor` | App / Infra | atomarer lokaler Ausführer: `.staging` → atomar nach `<KnowledgeRoot>\training\datasets\<plan_id>` |
| `ITrainingExportSidecarRequestBuilder` / `TrainingExportSidecarRequestBuilder` | App / Infra | verpackt den Plan für den strikten Sidecar-v2-Vertrag; `plan_sha256 == plan_id` |
| `ITrainingExportExecutionService` / `TrainingExportExecutionService` | App / Infra | Healthcheck, Anmeldung, Transport-Rückfall, Zielpfadprüfung; HTTP 4xx führt nicht zum lokalen Bypass |
| `ITrainingExportCompletionService` / `TrainingExportCompletionService` | App | markiert nur `TrainingSample`-Quellen, deren Bild-SHA der passende Plan bestätigt hat |
| `TrainingYoloExportWorkflow` | UI | nur Busy-, Fortschritts-, Fehlermeldungen — keine Datei-/Sidecar-Logik |

`PlanOnly` (bzw. StageA `--dry-run`/`--plan-only`) durchläuft Register, Inventar, Klassenkarte und Planer, schreibt aber nichts und mutiert keine UI-Liste. Eligibility/`ExportedUtc` werden erst nach bestätigter Ausführung einmal gemeinsam gespiegelt.

**Freigabestatus:** technischer Anschluss vorhanden, produktiv gesperrt, bis die class_map-v2-Migration freigegeben ist **und** ein menschlich freigegebenes `export_registry_v1.json` existiert. `tools/StageAExporter` ist reine Kompatibilitäts-CLI vor derselben Runtime; `--val-ratio` und `--allow-dummy-bbox` sind harte Fehler.

---

## 11. Ereignisbasierte Eval-Messung (AP 0.4a, technische Grundlage)

Die frühere Sammeldatei `EvalSetBenchmark.cs` ist aufgeteilt; öffentliche Klassennamen/Signaturen bleiben gleich (Dateien unter `Application/Ai/Evaluation/`).

| Klasse | Verantwortung |
|---|---|
| `EvalSetBenchmarkCase` (in `EvalSetBenchmarkModels.cs`) | trägt additiv `HoldingKey`, `ExpectedSeverity`, `EventId`, optional `MeterStart`/`MeterEnd` |
| `EvalSetBenchmarkDataset` | toleranter Loader für Altbestand-Sets |
| `EvalSetReleaseDatasetValidator` | `LoadAndValidate` für Release-Abnahme: fehlende Bilder/Haltungen, ungültige/widersprüchliche Werte stoppen; `EventId` nur bei Schäden Pflicht |
| `EvalSetV2Builder` | übernimmt die neuen Felder, verlangt Severity + Ereignis-ID bei Schäden |
| `EvalSetEventScorer` | zählt ein Ereignis über mehrere Frames einmal (Schlüssel Haltung+`EventId`), trennt Detect-Treffer vom Gate; Severity 4/5 ≥ 20 unabhängige Ereignisse, Wilson-/exakte 95%-Grenzen |
| `EvalSetManifestHasher` | Manifest-Hashing des Eval-Sets |

Das reale 120er-Set ist noch nicht mit Severity/EventId nachgepflegt — AP 0.4 ist nicht abgeschlossen; keine Modellfreigabe allein aus der Messlogik.

---

## 12. Zustandslose UI-Helfer & Renderer

Diese Klassen sind reine UI-Helfer und werden **nicht** im ServiceProvider registriert; die Fenster liefern nur Daten, Modus und Größe.

| Klasse | Verantwortung |
|---|---|
| `PhotoMeasurementGeometryService` | stabile öffentliche Fassade für reine Fotomessungs-Geometrie |
| `PhotoMeasurementAnglePlanBuilder` | zustandslose Winkel-, Abzweig-, Kreis-, Bogenplanung (nicht in WPF zurückschieben) |
| `PipelinePipeRadarRenderer` | zustandslose Zeichnung des Rohr-Radars der Videoanalyse |
| `LiveFrameRingOverlayRenderer` | gemeinsame Ring-Zeichnung mit drei Stilen (`Compact`/`Detail`/`Interactive`) |
| `PipelineLiveFrameOverlayRenderer` | Leer-/Größenregeln des eingebetteten Live-Rings |
| `LiveDetectionGeometryMapper` | gemeinsamer Uhrparser, Uhrwinkel, Fassade auf die zentrale Ringgeometrie |
| `RingSectorGeometry` | geometrische Ringform |
| `StatusColors` | zentrale Statusfarben (u. a. `Current.SeverityOverlay`) |
| `PipelineProgressMapper` | laufbezogene Fortschritts-/ETA-/Live-Frame-Abbildung (eine Instanz je Lauf) |
| `PipelineResultPresenter` | zustandslose Abschlussabbildung; baut ≤ 250 sichtbare `DetectionItem`-Zeilen |

**SAM-Review im Training Center:** `TrainingReviewSamWorkflow` prüft Kandidat/Box/Frame, startet den bedarfsgesteuerten `ITrainingReviewSamSegmentationService` (Umsetzung `TrainingReviewSamSegmentationService`) und bereitet Maske/Status auf. Der Durchmesser kommt aus `TrainingCenterWindowDependencyFactory`; nur `null` wird zu 300 mm. Datei-/Masken-/Einstellungslogik nicht ins Fenster zurückschieben.

---

## 13. Test-Guards

- **Golden-Test** unter `tests/Fixtures/TrainingExport/` — führt denselben Fall (Train, Dev-Val, Multi-Label-Bild) durch **beide** Export-Ausführer; relative Pfade, SHA-256 und Bytes aller Ausgabedateien müssen identisch sein.
- **Eval-Kontaminationsschutz** — `EvalContaminationGuard` (Application) plus Blockierung in `KnowledgeBaseManager`; `TrainingInventoryEvalProtectionReader` sichert Freeze-/Hash-Integrität je Eval-Set.
- **Inventar-Vertrag** — `TrainingInventoryReportValidator` prüft Schema 2.2 vor Schreiben und nach Lesen; fokussierte Tests unter `tests/AuswertungPro.Next.Infrastructure.Tests/Ai/Training/Inventory/`.
- **BBD-Rückmapping** — Integrationstest schützt die Kette `BBD_boden → BBDZ`.
- **Eval-CSV/JSON-Ausgaben** — Verhaltenstests sichern alle sieben Ausgaben samt Kopfzeilen und Escaping.
- Weitere Testprojekte: siehe SYSTEM-FAKTEN.md, Abschnitt 1.

---

## 14. Entfernte / nicht existente Klassen (per `rg` bestätigt, 0 Treffer)

Diese Namen dürfen **nicht** als Ist-Zustand auftauchen (Erwähnung nur als „entfernt"/„existiert nicht"):

`FewShotExampleStore` · `DetectionAggregator` · `YoloDatasetExportService` · `InferenceOrchestratorService` · `KbDeduplicationService` · `BenchmarkMetricsStore` · `BenchmarkRunner` · `EvalSetGenerator` · `PdfProtocolTableParser` · `CreateFewShotStore` · `EvalSetBenchmark` (Sammeldatei, aufgeteilt) · `AiRuntimeConfig` (durch `AiRuntimeSettings` ersetzt).

Ebenfalls nicht im HEAD: ByteTrack/OC-SORT/echtes Multi-Object-Tracking, automatische 8B→32B-Laufzeit-Eskalation, `UpdateActive` als Dedup-Wahrheit. Vollständige Negativliste mit Pfaden/Modellen/Routen: SYSTEM-FAKTEN.md, Abschnitt 8.
