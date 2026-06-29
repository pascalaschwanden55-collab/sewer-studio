# Architektur-Fahrplan V2 — UI-Schicht (Claude ↔ Codex)

**Stand:** 2026-06-29 · Grundlage: 5-Agenten-Analyse der 23 größten UI-Dateien (feature/gis-karte @ 1a655bb0).

Backend (Application/Infrastructure/Domain/tools) ist erschöpfend refaktoriert (siehe `ARCHITEKTUR-FAHRPLAN.md` + Kampagne). **V2 nimmt sich die UI vor** — Ziel: **Thin-VM** (Geschäftslogik raus aus VMs/Code-Behinds nach Application/Domain, testbar; VM nur noch Orchestrierung).

Umfang: 23 God-Dateien, ~21.700 Zeilen. **12 → Claude** (Logik-Extrakt + Tests), **11 → Codex** (WPF/MVVM/View-Lifecycle/Dedup).

---

## Vertrag — Whole-File-Ownership (kollisionsfrei)

**Jede Datei gehört EXAKT einem Agenten.** Der Owner macht ALLES an ihr (Logik raus + VM/Code-Behind ausdünnen + eigener Wiring-Edit). Kein Cross-Agent-Edit an derselben Datei.

- **Neue Ziel-Dateien:** Claude besitzt alles neue unter `Application/*`, `Domain/*`, `Infrastructure/Costs/*` + neue Tests in `tests/…Infrastructure.Tests`. Codex besitzt neue Controller/Renderer/Behaviors/Converter unter `UI/*` + UI-nahe Tests in `tests/…UI.Tests`.
- **Geteilte Test-Projekte** (Infrastructure.Tests, UI.Tests): nur **additiv** neue Dateien, **nie fremde Tests editieren**; Dateinamen mit Owner-Bezug → weniger Merge-Konflikte.
- **Geteilte Utilities EINMALIG:** `StringNormalizer` (Umlaut/Mojibake, 3× dupliziert), `VideoTimeFormatter` (3×), `ParseClockHour`/Clock-Mathe → **Claude legt sie einmal in `Application.Common` an, Codex referenziert nur (schreibt sie nicht).**
- **Worktree-Disziplin:** ein eigener Worktree pro Agent (Lehre Rogue-Cleanup-Hazard). Je Schnitt **ein** verhaltensneutraler Commit + Build/Test grün, bevor der nächste beginnt.
- **WPF-Smoke Pflicht** für Codex' heikle View-Dateien (DataPage/SchaechtePage/PhotoMeasurement/VsaCodeExplorer).

### ⚠️ Kosten-Cluster — Pflicht-Sequenz (einziges echtes Risiko)
`MeasureBlockVm`/`CostLineVm` leben in `CostCalculatorViewModel.cs` und werden von HoldingMeasureFactory, CostConsistencyCheckService und SanierungsMatrix benutzt. **Komplett Claude, strikt sequenziell:** C1 (Pricing-Engine auf VM-freies Read-Model) → C2 (HoldingMeasureFactory) → C3 (CostConsistencyCheck) → C4 (SanierungsMatrix-Logik). Codex-Owner, die eine von Claude verschobene Klasse referenzieren, **warten auf Claudes Commit**.

---

## CLAUDE-Lane (Logik → Application/Domain + Tests)

| # | Datei | Arbeit (Ziel-Klassen) | Risiko |
|---|---|---|---|
| C1 | `CostCalculatorViewModel.cs` | **Fundament zuerst.** Preis-/Mengen-Engine aus MeasureBlockVm → `MeasurePricingEngine` (Infrastructure.Costs, auf CostLine/MeasureCost statt CostLineVm) + `MeasureRuleService` (Installations-/EndManschette-Regel, DN200) + `CatalogItemGrouping` + `MeasureImportDefaultsResolver`. Reihenfolge Preis-vor-Menge + `_suppress`/`_enforcing`-Re-Entrancy exakt. | 🔴 |
| C2 | `HoldingMeasureFactory.cs` | NACH C1. `Build()` headless über Engine (kein `new MeasureBlockVm`); Datei → Infrastructure.Costs. | 🔴 |
| C3 | `CostConsistencyCheckService.cs` | NACH C1. Regelwerk KK01–KK14 → Application.Cost; Eingabe hinter `IMeasureBlockView`-Read-Model. Schwellen 50 %/Cross-10 % exakt. Nur Warnungen. | 🟡 |
| C4 | `SanierungsMatrixPageViewModel.cs` | NACH C1–C3. Quick Wins (SummaryFormatter, NavigationTarget) → dann `CatalogPriceApplier` (KEIN DN-Fallback), `MatrixMeasureOptionBuilder`, `RowStoreProjection`, Domain-Mapper. Audit W6/W8/K1/K3 → Tests zahlen hier am meisten. Dirty-Guards/Dialog bleiben in der VM. | 🔴 |
| C5 | `ProtocolEntryEditorDialog.xaml.cs` | ~600 Z. Parse/Normalisier/Validier (teils doppelt) → `ProtocolEntryInputNormalizer` + **ein** `ProtocolEntryValidator` (statt doppelter Validate-Methoden) + `DefaultDescriptionBuilder` + `ProtocolAiInputFactory`. OK-Pfad-Reihenfolge exakt. | 🟡 |
| C6 | `ObservationCatalogViewModel.cs` | `VsaCatalogTreeBuilder`, `ProtocolDescriptionBuilder`, `VsaParameterMerger` (WinCan-Aliase), `ProtocolEntryApplyService`, `DescriptionClockQuantParser`. Fließt in WinCan-Export → verhaltenskritisch. Tests fehlen komplett → erst Charakterisierung. | 🟡 |
| C7 | `VsaCodeExplorerViewModel.cs` | `VsaCodePathResolver`, `VsaCodeValidator`, `ProtocolEntryFromVsaSelectionBuilder`, `VsaTileFactory` (6× dupliziert). Codierungskritisch → Tests zuerst. | 🟡 |
| C8 | `DataPage/DataPageSanierungCostMapper.cs` | `SanierungCostFieldMapper` (Audit W7 max-statt-sum, Liner=1Stk, LEM≠Manschette) + `MeasureClassification` (Domain). Verfälscht sonst Kostenfelder UND Lern-Labels. | 🟡 |
| C9 | `Ai/Pipeline/SamMaskRenderer.cs` | `SamMaskDecoder` (RLE, Overflow-Schutz) + `SamMaskRenderPolicy` (DecideVisualMode) + `MaskLabelTextBuilder`. ⚠️ **Memory:** Policy hat die „Maske-verzerrt"-Regression ausgelöst → immer SubtleFill bei sichtbarer Maske exakt erhalten; Geometrie bleibt in der Datei. | 🟡 |
| C10 | `Services/AiStartupService.cs` | `AiStartupPlanBuilder` + `AiStartupResultSummarizer` + `AiStartupOrchestrator` (gegen `IAiStartupLauncher`). Timeout-Konstanten (80×500 ms Ollama, 240×500 ms Sidecar) 1:1. | 🟡 |
| C11 | `Services/KnowledgeBackupService.cs` | `KnowledgeBackupPathMapper` (../-Traversal-Schutz) + `FramePathRemapper` + Manifest-Policy; `SafePathGuard` → Infrastructure. Import überschreibt KB → strikt neutral. | 🟡 |
| C12 | `DataPage/DataPageProtocolPathResolver.cs` | **Risikoärmster Quick Win, Einstieg/Pattern.** 1:1 → Application.DataPage (`ProtocolPathResolver` + `PdfCandidateSelector`) + Tests. Read-only Dateisuche. | 🟢 |

## CODEX-Lane (WPF/MVVM/View-Lifecycle/Dedup)

| # | Datei | Arbeit | Risiko |
|---|---|---|---|
| X1 | `Views/Pages/DataPage.ColumnLayout.cs` | **Größter Dedup-Hebel zuerst** (entlastet SchaechtePage+DataPage): Alignment-/Layout-Persistenz (~600 doppelte Z.) → geteilter `DataGridColumnLayoutController`/Behavior. | 🟡 |
| X2 | `Views/Pages/SchaechtePage.xaml.cs` | Auf den Controller (X1) umstellen; Column-Factories mit DataPage zusammenführen; DataContext-/Abo-Lifecycle, ApplySearchFilter-Reentrancy. Reine Resolver in geteilte Helfer. WPF-Smoke. | 🟡 |
| X3 | `Views/Pages/DataPage.xaml.cs` | `DataPageColumnFactory`; Abdock/Andock → `GridDockingController`; ~20 Menu_Click → generischer CommandRouter; Drag&Drop → Behavior. ⚠️ Floating-Window-Zustand, Reflection auf `vm.UpdateNr`, Rename-Seiteneffekte. WPF-Smoke. | 🔴 |
| X4 | `ViewModels/Windows/TrainingCenterViewModel.cs` | **Größter Codex-Hebel** (Backend schon ausgelagert): BatchImportAndIndexAsync (~300 Z.) → `BatchImportWorkflowController`; RunSelfTrainingAsync (~210 Z.) → `SelfTrainingSessionController`; Dispatcher → IUiThread; ObservableProperty-Flut → Teil-VMs. ⚠️ KB-Persistenz/Auto-Approve/Eval-Schutz. | 🔴 |
| X5 | `Views/Windows/VideoAnalysisPipelineWindow.xaml.cs` | RenderPipeRadar (~280 Z.) + Overlay → Radar/Overlay-Renderer; Progress-Callback → ProgressMapper. Clock/Zeit-Mathe gegen Application.Common-Utility (nicht neu erfinden). | 🟡 |
| X6 | `ViewModels/Pages/DataPageViewModel.cs` | Konstruktor (~30 Commands) → CommandRegistration; Print (~300 Z.) → `DataPagePrintController`; Auto-Save → Controller; VideoLink-4-fach-Fallback entdoppeln. | 🟡 |
| X7 | `ViewModels/Pages/ImportPageViewModel.cs` | `RunImportAsync<TArg>` → `ImportRunController`; 5 Import*Async entdoppeln (dryRun-Parameter); 3 wortgleiche Store*Files-Tripletten (~210 Z.) → `StoredImportFileRegistry`. | 🟡 |
| X8 | `ViewModels/Pages/SchaechtePageViewModel.cs` | ~25 fast identische Dropdown-Commands → generischer `OptionsCommandSet`; Dialog → Service; Nicht-UI-Reste (ClosedXML-Reader → Infrastructure) in geteilte Helfer. | 🟡 |
| X9 | `Views/Windows/PhotoMeasurementWindow.xaml.cs` | ~70 % Canvas: `PhotoOverlayRenderer` + `PhotoToolController` + InputController; Quant-Mathe → `PhotoMeasurementGeometryService` (Uhr-Sortierung, Deform %, Shoelace, Letterbox) testbar. | 🟡 |
| X10 | `Views/Windows/VsaCodeExplorerWindow.xaml.cs` | View-Glue (Kern liegt in C7): programmatischer Control-Bau → XAML-DataTemplates+TileRenderer; Vm_PropertyChanged-switch → Bindings. ⚠️ toten Minuten-Code mit Test prüfen, nicht still mitnehmen. | 🟡 |
| X11 | `Controls/PipeGraphTimeline.xaml.cs` | **Risikoärmste.** Nur `MarkerColorClassifier` (QualityGate-Schwellen) + `TimelineScaleCalculator` als testbare static-Helfer raus; Rendering/DP bleibt Control. | 🟢 |

## Querschnitt (beide, dauerhaft)

- **Größen-Budget-Guard:** je Datei Zeilen-Obergrenze-Guard-Test → God-Klassen wachsen nach dem Schnitt nicht nach.
- **Test-Projekt-Mapping:** kein Application.Tests-Projekt → neue Logik-Tests nach `Infrastructure.Tests` (Muster `PdfImportSafetyPolicyTests`), UI-Smoke nach `UI.Tests`; nur additiv, Owner-Präfix im Dateinamen.
- **Test-zuerst:** bei jedem 🔴/🟡-Schnitt erst Charakterisierung gegen IST (grün), dann extrahieren. Besonders Kosten-/Mengen-/MwSt-/Quant-Mathe + SAM-Render-Policy (bekannte Audit-Bugs W6/W7/W8/W11/K1/K3 + SAM-Regression → je Regel ein Test).

## Empfohlene Start-Reihenfolge
- **Claude:** C12 (grüner Quick Win, etabliert das Muster) → dann der Kosten-Cluster C1→C2→C3→C4 (höchster, riskantester Hebel).
- **Codex:** X1 (DataGrid-Layout-Dedup, entlastet 3 Dateien) → X4 (TrainingCenter-Monster-Commands).

**Größter Gesamthebel:** Claude = ungetestete, audit-bug-behaftete Kosten-/Mengen-/MwSt-Logik + doppelte Protokoll-Validierung. Codex = der ~600-Z.-Layout-Klon + die 300/210-Z.-Commands im TrainingCenter.
