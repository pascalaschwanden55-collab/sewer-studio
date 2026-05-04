# Session-Zusammenfassung 2026-05-04 — Phase 0 komplett

**Branch:** `feature/pdf-import-beobachtungen`
**Ziel:** Audit-Phase 0 (Hard-Blocker + Quick-Wins) komplett abarbeiten
**Resultat:** 11 Commits, alle Phase-0-Aufgaben erledigt, Build und Tests gruen

---

## Endstand auf einen Blick

| Metrik | Vorher | Nachher |
|---|---|---|
| `dotnet build AuswertungPro.sln` (parallel) | scheiterte mit ~140 Fehlern | **0 Fehler, 2 Warnings, ~8 s** |
| `dotnet test Pipeline.Tests` (Default) | konnte ~100 Min laufen | **452 Tests, 0 Fehler, 13 s** |
| `dotnet test Infrastructure.Tests` | 135 bestanden, 1 uebersprungen | unveraendert (135/1) |
| Git-Index-Belastung | 285 Files / ~6.8 GB Sidecar-Daten | aus Index entfernt |
| Phase-0-Aufgaben | 4 offen + 1 unbekannter Folgepunkt | **alle 6 erledigt** |

---

## Was wurde geaendert (Commits)

| # | Hash | Titel | Hauptfile(s) |
|---|---|---|---|
| 1 | `ef91ad88` | Phase 0: Pipeline.Tests-csproj — File-Lock-Fix + TFM aligned | `tests/.../Pipeline.Tests.csproj` |
| 2 | `0660cca2` | Phase 1.1/1.3: Repo-Entlastung — 285 Files raus aus Git-Index (~6.8 GB) | `.gitignore` + 285 `D` |
| 3 | `2db3aafc` | Audits v4.3: Konsens-Sammlung + Phase-0-Update | `docs/audits/v4.3/*.md` (5 neu) |
| 4 | `cf631232` | Phase 0.1b: Pipeline-Tests-Default-Filter — GPU/Langzeit standard aus | `.runsettings` + `.runsettings.gpu` + csproj |
| 5 | `d837a792` | Audits v4.3: AUDIT_SUMMARY — Phase 0.1b als erledigt markiert | `AUDIT_SUMMARY.md` |
| 6 | `f15cf920` | Phase 0.2: kbHttp-Leak fixen — ServiceProvider als IDisposable | `ServiceProvider.cs` |
| 7 | `0165a07f` | Audits v4.3: AUDIT_SUMMARY — Phase 0.2 als erledigt markiert | `AUDIT_SUMMARY.md` |
| 8 | `53596001` | Phase 0.3: SafeFireAndForget — 6 Stellen in PlayerWindow migriert | `PlayerWindow.xaml.cs` |
| 9 | `1d97351f` | Audits v4.3: AUDIT_SUMMARY — Phase 0.3 als erledigt markiert | `AUDIT_SUMMARY.md` |
| 10 | `50364240` | Phase 0.4: DamageClassesPromptFull aktivieren — Batch/Video-Pipelines | `EnhancedVisionAnalysisService.cs` + 2 |
| 11 | `37310d8a` | Phase 0-Sweep: Restliche SafeFireAndForget + ServiceProvider.Dispose-Hook | `OverviewPage` + `KnowledgeMirror` + `App.xaml.cs` |

---

## Detailbericht je Aufgabe

### 1. Phase 0 — Build reparieren (ef91ad88)

**Audit-Diagnose:** "140 XAML-Fehler in DataPage / ClockPicker / Schaechte" (Konsens 2/3 Claude+Codex)
**Tatsaechliche Ursache:** keine XAML-Fehler. `error MC1000` File-Lock auf `*.g.cs` durch ein `AdditionalProperties`-Override im `Pipeline.Tests.csproj`, das MSBuild zwang das UI-Projekt zweimal parallel zu bauen. Beide WPF-Markup-Compiler-Targets schrieben gleichzeitig in dasselbe `obj/`-Verzeichnis — die ~140 `CS2001`-Fehler waren reine Folgeschaeden.

**Fix:** 1 Zeile im csproj entfernt — `AdditionalProperties` weg, MSBuild dedupliziert zu **einem** UI-Build.

**Aufwand:** 1 Zeile (Audit-Schaetzung war 1 Tag)

**Lehre:** Vor Audit-Annahmen die **erste** Fehlerzeile pruefen, nicht die haeufigste. Build-Fehler kaskadieren.

---

### 2. Phase 1.1/1.3 — Repo-Entlastung (0660cca2)

**285 Files / ~6.8 GB aus Git-Index entfernt** (Filesystem unangetastet — Sidecar braucht die Daten zur Laufzeit):
- `sidecar/florence2_shadow_log/` (281 Frames + 1 jsonl, 6.6 GB)
- `sidecar/yolo11m.pt/.engine/.onnx` (3 Files, 157 MB)
- `sidecar/models/yolo26m/yolo26m.engine` (1 File, ~40 MB)

**`.gitignore` ergaenzt** um:
- `sidecar/florence2_shadow_log/`
- `sidecar/*.pt`, `*.engine`, `*.onnx`, `*.safetensors`, `*.pth`, `*.bin`
- `sidecar/models/**/*.engine`
- `*.csproj.lscache`
- `**/__pycache__/` und `**/*.pyc` global (vorher nur unter `sidecar/`)

**Wichtig:** echte Repo-Groessenreduktion in der **History** wuerde `git filter-repo` benoetigen (destruktiv, History-Rewrite) — bewusst NICHT gemacht. Future-Branches und Clones haben den Ballast nicht mehr, alte Commits behalten ihn.

---

### 3. Audit-Konsolidierung (2db3aafc, 5 weitere AUDIT_SUMMARY-Updates)

**5 Audit-Dateien** in den Hauptbaum kopiert nach `docs/audits/v4.3/`:
- `audit_gemini.md` — konzeptionell, Stakeholder-Sicht
- `audit_claude.md` — metrisch, STAB/SEC/ARCH-Tracking
- `audit_codex.md` — build-/test-/repo-praktisch
- `audit_konsolidiert_2026-05-04.md` — deutsche Codex-Re-Formulierung + 6-Phasen-Plan (kein 4. Stimmgeber)
- `AUDIT_SUMMARY.md` — Konsens-Auswertung mit Vergleichstabelle, Top-10, Phasen-Roadmap, Status-Tracking

**`AUDIT_SUMMARY.md` aktualisiert** mit:
- Phase-0-Erkenntnis-Block ueber der Vergleichstabelle (Build-Fehldiagnose dokumentiert)
- B1 als erledigt markiert mit Wurzelanalyse
- Top-10 und Phase-0-Tabelle mit Status-Spalte (offen / ✅)
- Neuer Abschnitt **D2** mit 3 Zusatz-Befunden aus konsolidiertem Audit (WindowStateManager.Save synchron, RotateGenCts validiert, ProcessRunner uneinheitlich)

---

### 4. Phase 0.1b — Pipeline-Tests-Default-Filter (cf631232)

**Problem:** GpuEval-Tests in `QwenModelComparison`/`SdfProfileExtraction` liefen bei normalem `dotnet test` mit, obwohl mit `[Trait("Category", "GpuEval")]` markiert. Einer hatte `Skip=...`, der andere war `[Fact]` ohne Skip. Folge: Testlauf konnte ~100 Min dauern.

**Fix:** Zwei neue Files plus Konfig-Eintrag:
- `tests/.../.runsettings` — Default-`TestCaseFilter` `Category!=GpuEval&Category!=LongRunning`
- `tests/.../.runsettings.gpu` — manueller Override (leerer Filter) fuer GPU-Lauf
- csproj-Eintrag `<RunSettingsFilePath>$(MSBuildThisFileDirectory).runsettings</RunSettingsFilePath>`

**Resultat:** 452 Tests, 14 s (statt potenziell 100 min). Manueller GPU-Lauf via:
```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/ -settings tests/AuswertungPro.Next.Pipeline.Tests/.runsettings.gpu -filter "Category=GpuEval"
```

---

### 5. Phase 0.2 — kbHttp-Leak fixen (f15cf920)

**Problem (Audit B9, Konsens 2/3, Claude-CRITICAL):** `ServiceProvider.cs:230` hatte `var kbHttp = new HttpClient { ... }` als lokale Variable im try-Block. Bei Init-Fehler im `EmbeddingService`/`RetrievalService`-Konstruktor ging die Reference verloren — HttpClient + SocketsHttpHandler leakten Sockets bis GC.

**Fix:**
- Neues Field `_kbHttp` an `ServiceProvider`
- `ServiceProvider` implementiert nun `IDisposable`
- catch-Block disposed `_kbHttp` explizit bei Init-Fehler
- `Dispose()`-Methode am Klassenende fuer App-Shutdown-Cleanup
- `App.OnExit` ruft `Dispose()` auf (Commit 11)

`EmbeddingService` blieb unveraendert — kein Disposable-Refactoring noetig.

---

### 6. Phase 0.3 — SafeFireAndForget in PlayerWindow (53596001)

**6 Stellen** in `PlayerWindow.xaml.cs` von `_ = Task.Run(...)` auf `SafeFireAndForget(<Tag>)` migriert:
- Z.4308 `OverlayHintAutoAi`
- Z.6429 `PositiveFeedbackEntry`
- Z.6510 `NegativeFeedbackEntry`
- Z.7700 `NegativeFeedbackMask`
- Z.7765 `PositiveFeedbackMask`
- Z.7927 `CodingPauseCooldown`

**Eine Stelle bewusst nicht migriert** (Z.6892, Few-Shot-Loader) — hat bereits eigenes try/catch + Logger.

---

### 7. Phase 0.4 — DamageClassesPromptFull aktivieren (50364240)

**Problem:** `DamageClassesPromptFull` (~1500 Worte mit Aufnahmetechnik-Erkennung axial/nahaufnahme/schwenk/schacht) war deklariert aber nirgends verwendet — `#pragma warning disable CS0414` versteckte nur die "ungenutzt"-Warning.

**Fix:**
- `EnhancedVisionAnalysisService` Konstruktor um `useFullDamagePrompt = false` erweitert (abwaerts-kompatibel)
- Helper-Property `ActiveDamageClassesPrompt` waehlt zwischen kurz/voll
- `BuildPrompt()` und `BuildPdfPhotoPrompt()` referenzieren die Property statt der Konstante
- 3 Batch/Video-Aufrufer auf `useFullDamagePrompt: true` umgestellt:
  - `VideoFullAnalysisService.Create()`
  - `VideoAnalysisPipelineService` (sidecar-Pfad)
  - `VideoAnalysisPipelineService` (ollama-only-Pfad)
- 3 Codier-Modus-Aufrufer (`PlayerWindow`, `CodingModeWindow`, `TrainingCenterViewModel`) **bleiben unveraendert** → kurzer Prompt
- `#pragma warning disable/restore CS0414` entfernt — keine neue Warning

**Folge:** Batch-/Video-Pipelines bekommen ab jetzt die Aufnahmetechnik-Differenzierung und sollten weniger Fehl-Codierungen bei Kamerabewegung produzieren.

---

### 8. Phase 0-Sweep — SafeFireAndForget ausserhalb PlayerWindow + Caller-Side Dispose (37310d8a)

**Drei Folgeaktionen:**

**a) SafeFireAndForget-Sweep (9 Stellen):**
- `OverviewPageViewModel`: 8× `_ = LoadAllProjectsAsync()` → `OverviewLoadProjects`/`OverviewRefresh`
- `KnowledgeMirrorService:141`: `_ = SyncNowAsync()` → `KnowledgeMirrorDebounceSync`

**Bewusst nicht migriert** (haben bereits eigenes try/catch oder ContinueWith-Schluck):
- `App.xaml.cs:82` (Sidecar-Auto-Start)
- `ServiceProvider.cs:142` (Warmup-Task)
- `ServiceProvider.cs:279` (Brain-Mirror-Initial-Sync)
- `FeedbackIngestionService:91` (Self-Improving-Pfad)
- `ReviewQueueService:81` (Persistenz)
- `VideoFrameStream:74` (ffmpeg-Stderr-Reader, ContinueWith OnlyOnFaulted)

**Bewusst nicht migriert** (UI-Klick-Handler — Exception fuehrt nur zu verschwundenem UI-Item, nicht App-Stabilitaetsrisiko):
- `CodingModeWindow.xaml.cs` (3 Stellen)
- `ImageAnnotationWindow.xaml.cs` (4 Stellen)
- `ProtocolEntryEditorDialog`, `ObservationCatalogWindow`, `PhotoMeasurementWindow`, `VideoTrainingReviewWindow`, `PlayerWindow.TrainingMode`

**b) Caller-Side `ServiceProvider.Dispose()` in `App.OnExit`:** Die in Phase 0.2 hinzugefuegte IDisposable-Implementierung wird jetzt tatsaechlich aufgerufen.

---

## Was bewusst NICHT gemacht wurde (Scope-Disziplin)

1. **Trait-Sweep der ~30 untraitierten Pipeline-Tests** — der Default-Filter wirkt schon, weiterer Sweep ist groesserer Refactor
2. **Sanierungs-YAMLs (`Knowledge/sanierung/products_and_manufacturers.yaml` etc.) stagen** — User-Entscheid (Audit B5 will sie als YAML-only-Quelle, aber Domain-Daten gehoeren explizit gestaged)
3. **Worktree `.claude/worktrees/kind-tharp-c96320/` loeschen** — User-Entscheid
4. **`git push` und PR** — laut Sicherheitsregel nur auf explizite Anweisung
5. **`git filter-repo`** fuer echte History-Repo-Verkleinerung — destruktiv, nur auf Anweisung
6. **Andere `_ = Task.Run/Async`-Stellen mit eigenem try/catch** — keine echte Verbesserung
7. **UI-Klick-Handler** in `_ = ...Async()` Pattern — kein Stabilitaetsrisiko
8. **Empty catch{} Sweep** (Phase 1.2) — separater Audit-Punkt, nicht Phase 0
9. **kein neuer NuGet** — Konvention "keine NuGet-Pakete ohne Rueckfrage"

---

## Commit-Hygiene-Hinweis

Die Commits 6, 8, 10 und 11 enthalten **zusaetzlich pre-existing Branch-Arbeit** der laufenden `pdf-import-beobachtungen`-Session. Diese Aenderungen waren bereits vor der heutigen Session im Working-Tree. Saubere Trennung war nicht moeglich, weil andere Files die pre-existing Aenderungen referenzieren — ein Reset auf HEAD-Stand wuerde mehrere weitere Files brechen (Beispiel `SanierungOptimizationViewModel.cs` ruft `ServiceProvider.RehabRulesEngine` auf, das pre-existing ist). Jede Commit-Body-Message dokumentiert das transparent.

Nicht angetastet:
- `CLAUDE.md` (M-Status seit Anfang)
- ~93 weitere modifizierte Files der Branch-Arbeit
- ~57 untracked Files der Branch-Arbeit (z.B. `tools/kb_repair/`, `tools/offertenvergleich/`, `src/AuswertungPro.Next.Domain/Sanierung/`)

---

## Pruefkriterien fuer den Reviewer

1. **Build:** `dotnet build AuswertungPro.sln` → 0 Fehler, 2 pre-existing Warnings
2. **Tests:** `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/` → 452 bestanden in ~14 s
3. **Tests Infrastructure:** `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/` → 135 bestanden, 1 uebersprungen
4. **GPU-Tests laufen weiterhin manuell:** `dotnet test ... -settings .runsettings.gpu -filter "Category=GpuEval"`
5. **Repo-Status:** `git log --oneline -12` zeigt die 11 Phase-0-Commits + Branch-Vorgeschichte
6. **Sidecar-Daten verfuegbar:** `ls sidecar/florence2_shadow_log/ | wc -l` → 281+ Files (im FS, nicht in Git)
7. **DamageClassesPromptFull-Aktivierung:** `grep -c "useFullDamagePrompt" src/AuswertungPro.Next.UI/Ai/*.cs` → mind. 5 Treffer (Konstruktor + 3 Aufrufer + Property)
8. **kbHttp-Lifecycle:** `grep -E "_kbHttp|IDisposable" src/AuswertungPro.Next.UI/ServiceProvider.cs` zeigt Field, Interface, Dispose()
9. **App.OnExit Hook:** `grep -A 3 "ServiceProvider).Dispose" src/AuswertungPro.Next.UI/App.xaml.cs` → Hook drin
10. **SafeFireAndForget-Verbreitung:** `grep -rc "SafeFireAndForget" src/AuswertungPro.Next.UI/` zeigt mind. 4 Files mit > 6 Treffern

---

## Naechste empfohlene Schritte (nicht in dieser Session erledigt)

| # | Aufgabe | Aufwand | Quelle |
|---|---|---|---|
| 1 | Empty-catch-Sweep — 6+ Stellen mind. `Debug.WriteLine($"...")` | 30 min | Phase 1.2 (Konsens 3/3) |
| 2 | Hydraulik / Eigendevis aus Hauptcode entfernen oder optional machen | 1 Tag | Phase 1.4 (Konsens 3/3) |
| 3 | KnowledgeBase: FK Embeddings→Samples + Embedding.ModelVersion | 4 h | Phase 2.1 (Konsens 2/3) |
| 4 | Channel\<T>-Pipeline fuer Training (Pre→KI→Gate→KB-Writer→UI) | 2 Tage | Phase 2.3 (Konsens 2/3) |
| 5 | RehabilitationRulesEngine: Hardcode raus, nur YAML | 1 Tag | Phase 2.5 (Konsens 3/3) |
| 6 | UI Spacing/Typography-Tokens + zentraler DataGrid-Style + PageHeader | 16 h | Phase 3.2/3.3 (Konsens 3/3) |
| 7 | DI-Container (Microsoft.Extensions.DependencyInjection) einfuehren | 2 Tage | Phase 5.1 (Konsens 2/3) |
| 8 | PlayerWindow.xaml.cs (9856 Zeilen) in Partials zerlegen | 1 Woche | Phase 6.1 (Konsens 3/3) |

Phase 0 ist abgeschlossen. Phasen 1-6 sind im `AUDIT_SUMMARY.md` priorisiert.

---

## Branch-Lage (Stand Phase 0 Ende)

- Branch `feature/pdf-import-beobachtungen` ist **224 Commits ahead** von `origin/feature/pdf-import-beobachtungen`
- **Nicht gepusht** (laut Sicherheitsregel)
- 91 modifizierte und 56 untracked Files entstammen der laufenden Branch-Arbeit, nicht dieser Session

---

## Update 2026-05-04 — Phasen 1.2 / 1.4 / 2.1 / 2.2 / 2.5 ergaenzt

Nach Phase 0 wurden in derselben Session weitere Phasen abgearbeitet:

### Erledigte Phasen (chronologisch)

| Phase | Konsens | Hash | Inhalt |
|---|:---:|---|---|
| **1.2** Empty-catch-Sweep | 3/3 | `c444689f` | 5 echt stille Service-Catches mit `Debug.WriteLine` |
| **1.4** Inventar | – | `55f28dae` | Analyse-Bericht Hydraulik / Eigendevis |
| **1.4** Toggle-Infrastruktur | 3/3 | `a5fed618` | `ShowExpertenmodusFeatures` (default true) |
| **1.4** Followup | – | `ca356d7d` | `HydraulikPrintDialog` ersetzt + geloescht |
| **2.1** KB-Schema | 2/3 | `77557bd1` | FK + ModelVersion + defensive Migration + 8 Tests |
| **2.2** KB-Writer | 2/3 | `250fdabc` | `KnowledgeBaseWriter` + PRAGMAs + 10 Tests |
| **2.5** Sanierungs-Engine | 3/3 | `8a9d7b0f` | JSON-Spiegel aus YAML + Hardcode-Fallback + 11 Tests |

### Endstand der Session (kombiniert mit Phase 0)

- **24 Commits** seit `ef91ad88` (15 Kern-/Phasen-Commits + 9 Audit-/Summary-Commits)
- **Build:** 0 Fehler bei sequenziellem Build (`dotnet build AuswertungPro.sln -m:1` oder Test-getrennt). Paralleler Build mit gleichzeitig laufendem `dotnet test` triggert noch den bekannten WPF-`.g.cs`-Race aus Phase 0 — **das ist erwartetes Verhalten**, sequenziell oder via runsettings sauber.
- **Tests gesamt:** **616 bestanden + 1 uebersprungen**
  - Pipeline-Tests: 481 bestanden, 0 uebersprungen, ~13 s
  - Infrastructure-Tests: 135 bestanden, 1 uebersprungen, ~13 s
- **+29 neue Tests in dieser Session**: 8 (KB-Schema) + 10 (KB-Writer) + 11 (RehabRulesEngine)

### Wichtige Praezisierung zu Phase 2.5

"YAML-only" ist sprachlich nicht praezise. Korrekt:
- **YAML** (`Knowledge/sanierung/rehabilitation_methods.yaml`) bleibt **menschliche Pflege-Quelle** (Kommentare, Quellen-Hinweise, lesbare Struktur)
- **JSON** (`src/AuswertungPro.Next.UI/Config/rehabilitation_methods.json`) ist die **maschinen-lesbare Laufzeit-Quelle** — wird vom Code direkt geladen
- Der **Hardcode in `RehabilitationRulesEngine.cs`** ist nur noch defensiver Fallback bei kaputtem/fehlendem JSON

Direktes YAML-Lesen wuerde `YamlDotNet` als NuGet erfordern — laut CLAUDE.md "keine NuGet-Pakete ohne Rueckfrage". Daher die JSON-Spiegel-Loesung.

### Code-Review-Bestaetigung 2026-05-04 nach Phase 2.5

- Commit `8a9d7b0f` ist atomar (Engine + JSON + Tests zusammen)
- `RehabilitationRulesEngine` liest primaer `rehabilitation_methods.json`
- Hardcode bleibt als defensiver Fallback
- `ServiceProvider` uebergibt korrekt `Config/rehabilitation_methods.json`
- 11 neue Tests bestanden, 0 Fehler
- Sequenzieller Build: 0 Fehler, 0 Warnungen
- Pipeline-Tests: 481 bestanden, 12 s
- Infrastructure-Tests: 135 bestanden, 1 uebersprungen, 13 s

Hinweis: ein paralleler Build/Test-Lauf triggert den WPF-`.g.cs`-Race (siehe Phase 0 Befund). Sequenziell oder via runsettings sauber.

---

## Update 2026-05-04 (zweiter Session-Block) — Phase 3.1, 5.5, 4.2, 3.5, 3.1+3.3-UserControls, 3.3-PageMigration, 3.4-Folge, 4.1, 5.1

Nach dem ersten Update (Phasen 1.2 / 1.4 / 2.1 / 2.2 / 2.5) wurden in derselben Session weitere Phasen abgearbeitet:

### Erledigte Phasen (chronologisch)

| Phase | Konsens | Hash | Inhalt |
|---|:---:|---|---|
| **3.1** app.manifest + DPI | C-Claude | `7d1394f08` | PerMonitorV2 + longPathAware + supportedOS Win 7..11 |
| **5.5** Sanierungs-Decision-Log | A7 (3/3) | `3ecd0228f` | Tabelle + Service + 9 Tests |
| **4.2** ffmpeg-Konsolidierung | C-Claude | `0cc6f50ec` | Inventar + Empfehlung (gestuffelt C1/C2/C3) |
| **3.5** Anleitungstexte raus | C-Gemini | `f41fafd6f` | Anleitungs-Block in Expander, default eingeklappt |
| **Druckcenter-Fix** | — | `5f4b65dbd` | Druck-Buttons im PageHeader sichtbar |
| **Phase 3.3-Erweiterung** UserControls | A6 (3/3) | `9a32b23df` (Tokens), `6872f4663` (4 Pages), `20a0a71e7` (5 letzte Pages) | PageHeader + StatusBadge UserControls, 12/12 Pages migriert |
| **3.4 Folge** FontSize | A6 (3/3) | `86d74a293` | 615/752 Inlines auf Tokens (82%) — TypeMicro/TypeIcon ergaenzt |
| **4.1** IDialogService | C-Claude | `325944656` | ShowDialog/Show/ShowMessage; 18 VM-Stellen migriert |
| **5.1** DI-Container | B2 (2/3) | `63e1f0b2c` | ⏸️ Inventar (NuGet-Freigabe noetig) |
| **4.1 Folge** MessageBox | — | `9c987c237` | ⏸️ Inventar (Migration nach 5.1) |

### Endstand (kombiniert mit Phase 0 + erstem Update)

- **~58 Commits** seit `ef91ad88`
- **23 Audit-Phasen** beruehrt (komplett, teilweise oder Inventar)
- **Build:** sequenziell 0 Fehler, 0 oder 2 Warnings (pre-existing)
- **Tests gesamt:** **645 bestanden + 1 uebersprungen** (510 Pipeline + 135 Infrastructure)
- **+45 neue Tests in dieser Session**: 8 (KB-Schema) + 10 (KB-Writer) + 11 (RehabRulesEngine) + 12 (TrainingRuns) + 9 (DecisionLog) + 8 (KbIngestionPipeline) — minus 13 die nicht direkt zugeordnet sind

### Wichtigste sichtbare Aenderungen fuer den User

- **12 Pages** mit konsistentem `PageHeader`-Stil (Title in TypeH2 / Subtitle in TypeBody / optional Action-Buttons rechts)
- **Druckcenter** zeigt Druck-Buttons jetzt im Header (auch bei kleinem Fenster sichtbar)
- **Einstellungen** zeigt Anleitung nur noch via Expander (default eingeklappt)
- **DPI-Schaerfe** auf Multi-Monitor-Setups (PerMonitorV2)
- **Tokens zentral** — wenn das Theme Type/Spacing-Werte aendert, propagiert das durch 615 FontSize-Stellen

### Was bewusst aufgeschoben wurde

- **Phase 5.1 DI-Container** wartet auf NuGet-Freigabe (Microsoft.Extensions.DependencyInjection 10.0.x). Bericht: `PHASE_5.1_DI_CONTAINER_INVENTAR.md` mit 4-Etappen-Plan.
- **Phase 4.1 Folge MessageBox-Migration** wartet auf Phase 5.1 (dann Constructor-Injection moeglich). Bericht: `PHASE_4.1_FOLGE_MESSAGEBOX_INVENTAR.md`.
- **Phase 4.2 Folge ffmpeg-Konsolidierung** in 3 gestuffelten Sub-Phasen (C1/C2/C3). Bericht: `PHASE_4.2_FFMPEG_INVENTAR.md`.
- **Phase 5.2/5.3/6.x** sind Mehr-Wochen-Eingriffe — eigene Sessions noetig.

### Offene Tranche-Pushes

GitHub HTTPS-Push limitiert auf ~10-20 Commits pro Push (HTTP 500 bei groesseren). Branch wird in 10er/20er-Etappen gepusht. Stand zum Zeitpunkt der Doku: ~48-58 Commits noch lokal.
