# Phase 6.2 — TrainingCenterViewModel zerlegen (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "TrainingCenterViewModel zerlegen → BatchOrchestrator + Repo + Writer" — Audit A2 (Konsens 3/3, ~1 Woche).
**Resultat:** Inventar + Modul-Vorschlag. KEIN Code-Eingriff.

---

## A. Bestand

| File | Zeilen |
|---|---:|
| `TrainingCenterViewModel.cs` | **2.964** |
| `TrainingCenterWindow.xaml` | 1.061 (laut Phase-0-Audit) |
| `TrainingCenterWindow.xaml.cs` | ~? (CodeBehind-Stellen migriert) |

**Metriken:**
- ~59 Methoden im VM
- Gewachsen seit Audit-Erstauflage (2.628 Zeilen)

---

## B. Verantwortlichkeiten

Aus Methoden- und Field-Namen ableitbar:

### B1. UI-State / Property-Bindings
- ObservableCollection<TrainingCase>, SelectedCase, IsRunning, ProgressText, LogText
- Tab-Selection (Cases / FewShot / YoloRetrain / Teacher)
- ~15% des Files

### B2. Datei-/Ordner-Import
- Import-Source-Verzeichnis waehlen
- TrainingCenterImportService (Phase 1.2 schon migriert)
- ~10% des Files

### B3. Batch-Training-Orchestrierung
- StartBatchAsync, ProcessCaseAsync, ParallelForEachAsync
- KI-Aufrufe (Ollama/Sidecar)
- Quality-Gate-Auswertung
- Result-Merging
- ~30% des Files

### B4. KnowledgeBase-Writes
- KnowledgeBaseManager.IndexSampleAsync (Phase 2.2 KbWriter-Lock)
- Sample-Persistierung
- Embedding-Generation
- ~15% des Files

### B5. Fortschrittsanzeige
- ProgressReporter, IProgress<T>-Implementation
- LogText-Append (mit MessageBox-Wrapper Phase 4.1)
- ~5% des Files

### B6. Few-Shot / Teacher / Yolo-Retrain Tabs
- FewShotExampleStore-Integration
- TeacherAnnotationStore-Integration
- YoloRetrainOrchestrator-Aufruf
- ~15% des Files

### B7. Persistenz-State
- TrainingCenterStore (Sessions wiederherstellen)
- TrainingCenterSettings (UI-Einstellungen pro Run)
- ~5% des Files

### B8. Cross-Cutting (Logging, Cancellation, Cleanup)
- CancellationTokenSource, RotateGenCts (Phase 0.2-validiert)
- Cleanup beim Abort
- ~5% des Files

---

## C. Vorgeschlagene Aufteilung

### Module-Vorschlag (Audit-konform: BatchOrchestrator + Repo + Writer)

| Modul | Soll-Inhalt | Zielgroesse |
|---|---|---:|
| `TrainingCenterViewModel.cs` (Shell) | UI-State, Property-Bindings, Command-Wiring | 600-800 Zeilen |
| `TrainingBatchOrchestrator.cs` (NEU) | B3 — Batch-Logik, ParallelForEachAsync, KI-Aufrufe | 800-1.000 Zeilen |
| `TrainingRunRepository.cs` (NEU) | B7 — Persistenz, TrainingCenterStore | 200 Zeilen |
| `KnowledgeBaseWriter` (existiert, Phase 2.2) | B4 — KB-Writes | (vorhanden) |
| `TrainingProgressModel.cs` (NEU) | B5 — IProgress<T>-DTO + Aggregation | 150 Zeilen |
| `TrainingCenterTabController.cs` (NEU) | B6 — Few-Shot / Teacher / Yolo-Retrain Logik | 400 Zeilen |
| `TrainingCenterImportService` (existiert, Phase 1.2) | B2 — Datei-Import | (vorhanden) |

**Total nach Migration:** ~2.150-2.550 Zeilen verteilt auf 7 Klassen — pro Klasse 150-1.000 Zeilen, wartbar.

---

## D. Audit-Vorschlag konkret

Audit-Empfehlung Codex (Befund 1.2 explizit fuer TrainingCenterViewModel):
- `BatchTrainingOrchestrator` — KI-Calls + Parallelisierung
- `TrainingRunRepository` — Persistenz
- `KnowledgeBaseWriter` — DB-Writes (Phase 2.2 schon erledigt)
- `TrainingProgressModel` — Fortschrittsanzeige

Mein Vorschlag deckt diese 4 + ergaenzt um Tab-Controller fuer Few-Shot/Teacher/Yolo-Retrain.

---

## E. Risiken

| Risiko | Wirkung | Gegenmittel |
|---|---|---|
| Cancellation-Logik (RotateGenCts) ueber Module verteilt | Cancel-Bug | CTS bleibt im Shell-VM, Sub-Module bekommen Token-Parameter |
| Konstruktor-Argumente zwischen VM und Orchestrator | Aufwand bei DI | Phase 5.1 (DI-Container) kommt vorher oder gleichzeitig |
| Test-Coverage fuer TrainingCenter | Nicht vorhanden | Sub-Phase G: Smoke-Test fuer Orchestrator + Repository |
| Branch-Konflikt mit anderen Eingriffen | Massen-Merge | In eigener Session, nicht parallel |
| Phase 5.3 Wechselwirkung | Orchestrator ruft KI direkt — wird einfacher nach 5.3 | Reihenfolge: 5.3 -> 6.2 |

---

## F. Empfohlener gestaffelter Pfad (~5-7 Tage)

### Sub-Phase 6.2.A: Vorbereitung (~3 h)
- Klassen-Skelette anlegen (`TrainingBatchOrchestrator`, `TrainingRunRepository`, `TrainingProgressModel`).
- Cluster-Methoden im VM identifizieren.

### Sub-Phase 6.2.B: TrainingProgressModel (~3 h)
- Klar abgegrenzte DTO-Klasse, leicht herauszuziehen.

### Sub-Phase 6.2.C: TrainingRunRepository (~4 h)
- B7 Persistenz, Store-Wrapper.

### Sub-Phase 6.2.D: TrainingBatchOrchestrator (~2 Tage)
- Groesster Cluster (B3). KI-Aufrufe + Parallelisierung.
- Cancellation-Token-Verkabelung.

### Sub-Phase 6.2.E: Tab-Controller (~1 Tag)
- B6 Few-Shot / Teacher / Yolo-Retrain.

### Sub-Phase 6.2.F: VM-Slim (~1 Tag)
- VM ruft nur Orchestrator/Repo/Writer/Tab-Controller — UI-State bleibt.

### Sub-Phase 6.2.G: Tests + Live-Test (~1 Tag)
- TrainingBatchOrchestrator Unit-Test (mit Mock-Embedder).
- Live-App-Test: Batch starten, Cancel, Resume.

**Total: ~5-7 Tage.**

---

## G. Reihenfolge mit anderen Phasen

Empfohlene Reihenfolge:
1. **5.3** KI-Schicht aus UI (Interfaces verfuegbar)
2. **5.2** ServiceProvider zerlegen
3. **5.1** DI-Container
4. **6.1** PlayerWindow Partials
5. **6.2** TrainingCenterVM zerlegen ← hier

Alternative (parallel zu 6.1):
- 6.1 und 6.2 koennen gleichzeitig in zwei Branches.

---

## H. Akzeptanz

- TrainingCenterViewModel.cs verifiziert: 2.964 Zeilen, ~59 Methoden.
- 8 Verantwortlichkeits-Cluster identifiziert.
- 7-Module-Vorschlag (4 neue + 3 vorhandene/erledigte).
- Audit-Konsens-konform (BatchOrchestrator + Repo + Writer + ProgressModel).
- ⏸️ Migration in **eigener Mehr-Tages-Session** mit User-Freigabe.
- KEIN Code-Eingriff in dieser Iteration.
