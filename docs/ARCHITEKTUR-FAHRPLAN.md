# Architektur-Schulden-Fahrplan (SewerStudio)

**Stand:** 2026-06-28 · Grundlage: Architektur-Urteil + 3-Agenten-Analyse (Architektur / Testlücken / Risiko) über die größten Dateien.

---

## Kurz-Befund (Architektur-Urteil)

**Gesamtnote ~B.** Solides, ehrlich geschichtetes Fundament mit kontainierter, abbaubarer Wartungs-Schuld.

- **Stark:** echte 4-Schichten-Trennung (Domain ← Application ← Infrastructure ← UI), Domain abhängigkeitsfrei; konsequentes „Thin-AI" (C# = Logik, LLM nur Text); überdurchschnittliche Robustheit (atomare Writes, Quarantäne, Restore-Points, Process-Safety, Thread-Sync); etabliertes Extraktions-Muster (PlayerWindow → 95 Partials zerlegt).
- **Schuld (alles Wartbarkeit, kein High-Risk-Strukturfehler):** eine Handvoll 1500–2000-Zeilen-God-Classes; `ServiceProvider` als manueller Service-Locator (erschwert VM-Testbarkeit); pragmatisches Layering (UI referenziert Infrastructure direkt, keine Dependency-Inversion am Rand); punktuelle Duplikate.
- **Bewusst NICHT zu tun:** kein DI-Framework, kein Docker/K8s, kein Schichtwechsel. Für eine Solo-Desktop-App Gold-Plating. **Gezielt schneiden, nicht umbauen.**

---

## Aufteilung Claude ↔ Codex (parallel, kollisionsfrei)

Disjunkte Datei-Mengen, je eigener Worktree, beide von der Mainline (`feature/gis-karte`) abgezweigt, unabhängig mergebar. **Kein Agent fasst eine Datei des anderen an.**

**EINZIGE Koordination (Vertrag):** Die *öffentliche* API von `AppSettings` und `ServiceProvider` bleibt **stabil** — Claude restrukturiert nur deren Innenleben. So brechen Codex' VM-Zugriffe (lesen Settings-Properties, rufen ServiceProvider-Accessor) nicht.

### CLAUDE — Backend / Infrastructure / Output · Branch `refactor/backend`
- `ProtocolPdfExporter` (Phase 1) ← **Start**
- `HoldingFolderDistributor*` (Phase 1)
- `SystemMonitorService` (Phase 1)
- `AppSettings` — interner Split, öffentliche Properties stabil (Phase 4)
- `ServiceProvider` — interne Gruppierung, öffentliche Accessor stabil (Phase 4)

### CODEX — UI-Schicht · eigener Branch
- Phase 0 UI-Dedups: `DataGridColumnFactory` (DataPage+Schaechte = **EIN** Auftrag), `HorizontalAlignmentToTextAlignmentConverter`, `FileNameSanitizer` (CostCalc+Builder)
- SanierungsMatrix: Klassen-Split + `SavePolicy`
- VMs: `TrainingCenterViewModel`, `CostCalculatorViewModel`, `BuilderPageViewModel` (Phase 2)
- Page-Code-Behinds: `DataPageViewModel`, `SchaechtePage.xaml.cs`, `DataPage.xaml.cs` (Phase 3, zuletzt)

**Grenze:** Claude bleibt aus `UI/ViewModels` + `UI/Views`. Codex bleibt aus `Infrastructure` + `Application/Output` und lässt `AppSettings`/`ServiceProvider` in Ruhe (nur lesen). PlayerWindow unberührt.

---

## Methode (für JEDEN Schnitt verbindlich)

1. **Charakterisierungs-/Guard-Test ZUERST** (pure Logik → echter Unit-Test; UI/Invariante → Datei-Inhalt-Guard-Test, Vorbilder: `UiArchitectureGuardTests`, `ShellNavigationPolicyTests`).
2. **GENAU EINE** fokussierte Einheit extrahieren (pure Logik → Application-Klasse; exklusiver Zustand → Controller/Service; geteilte Helfer im Owner lassen, als Delegate injizieren).
3. **VERHALTENSNEUTRAL.** `dotnet build AuswertungPro.sln` 0/0 **und** `dotnet test AuswertungPro.sln` grün.
4. **EIN Commit pro Einheit.** Kommentare Deutsch, keine neuen NuGet-Pakete ohne Rückfrage.
5. **Isoliert:** ein Agent pro klar getrenntem Bereich, eigener Worktree/Branch. PlayerWindow nicht anfassen.
6. **Latente Bugs nicht heimlich mitfixen** — separat melden/committen.

Legende: Aufwand **S/M/L** · Wirkung ★–★★★ · Risiko 🟢/🟡/🔴 · „∥" = parallelisierbar (eigener Worktree, disjunkte Dateien).

---

## Phase 0 — Quick Wins (zuerst, fast risikofrei) · S · ★★ · 🟢

- **`DataGridColumnFactory`** aus `DataPage.xaml.cs` **+** `SchaechtePage.xaml.cs` ziehen → ~200 doppelte Zeilen weg. ⚠️ **EIN** Auftrag (Cross-File), NICHT auf zwei Agenten splitten.
- **`FileNameSanitizer`** (Duplikat in CostCalculatorViewModel + BuilderPageViewModel) → eine Klasse. ∥
- **`HorizontalAlignmentToTextAlignmentConverter`** (Duplikat DataPage/Schaechte) → eigene Datei in `Converters/`. ∥
- **SanierungsMatrix: ~5–6 Sub-Klassen** je in eigene Datei (reiner Move, keine Logik-Änderung). 1280 → ~330 Z. VM. ∥

## Phase 1 — Sichere God-Class-Schnitte (Infrastructure/Output) · M · ★★★ · 🟢

Kein WPF-Timing, gut testbar, echt parallelisierbar in je eigenem Worktree.

- **`ProtocolPdfExporter.cs` (~1673)** → PDF-Modell-Aufbau / Rendering / Datenaufbereitung trennen (Builder + Renderer). **Bester Erst-Kandidat.** ∥
- **`HoldingFolderDistributor*.cs`** → IO/Pfad-/Namenslogik in pure, getestete Helfer; Orchestrator dünn halten. ∥
- **`SystemMonitorService.cs` (~1350)** → `HvciDetector`, `LhmSensorSelector`, `CpuDeltaCalculator`, `NvidiaSmiPoller`. Disposed-/Volatile-Guard NICHT anfassen, kein UI-blockierendes `lock` einbauen. ∥

## Phase 2 — VM-Schnitte (Test ZUERST) · M–L · ★★★ · 🟡

- **`TrainingCenterViewModel.cs` (~1816–2050)** → pure: `KbReadinessCalculator`, `SelfTrainingStepProcessor`; State: `ReviewQueueController`, `KbStatusController`. Vorab-Guards: IsBusy-Symmetrie (try/finally), Cancel (`ct.IsCancellationRequested`). 🟡 ∥
- **`CostCalculatorViewModel.cs` (~1690)** → `CostOwnerLookup`, `DeriveGroupFromKey`/`GetCatalogGroupOrder`, `CostPdfContextBuilder`. `MeasureBlockVm`-Handler beim Entfernen abmelden. 🟡 ∥
- **`SanierungsMatrixPageViewModel.cs`** (nach Phase 0): `SanierungsMatrixSavePolicy` (touched/cleared-Merge). `_suppressSelectionGuard` (TwoWay-Timing, „Audit W1") unangetastet. 🟡
- **`BuilderPageViewModel.cs` (~1490)** → reine Helfer zuerst (`ParseDecimal`/`NormalizeYear`/`TryResolveSpecialCategory`/Filter) → `BuilderPageHelpers`. `Attach/DetachProjectData` + Dispose vorsichtig (Record-Subscription-Leak). 🟡 ∥

## Phase 3 — Riskante Page-Code-Behinds (ZULETZT) · L · ★★ · 🔴

Erst Charakterisierungstests, dann latente Bugs fixen, dann schneiden. NICHT parallel zur Phase-0-Spalten-Factory.

- **`DataPageViewModel.cs` (~1522–1760)** → Dropdown-Commands (5×5) → `DropdownOptionsController`; `DnParsingPolicy`, `AutoSaveScheduler`. ⚠️ `LiveControlRetryBridge.Register` auf `IDisposable`-Deregistrierung (sonst Doppel-Registrierung). `_isSyncingSelectedProtocol` zuletzt. 🔴
- **`SchaechtePage.xaml.cs` (~1710)** → ⚠️ **null Testabdeckung** → Guards zuerst; dann `SchaechteGridColumnBuilder`; Record-Subscriptions zuletzt. 🔴
- **`DataPage.xaml.cs` (~1552–1780)** → **allerletzte Datei.** Zuerst fixen: Reflection auf `vm.UpdateNr` → `internal`/Event; `_isUndocking` ohne try/finally. Dann `FloatingGridController`. Jeden Teilschritt einzeln committen + manuell prüfen (Unloaded/Undocking-Pfad besonders heikel — hier gab es schon Abstürze). 🔴

## Phase 4 — Infrastruktur-Hygiene (opportunistisch) · M · ★★ · 🟢

- **`AppSettings.cs` (364)** → Settings-Modell vom Rest trennen: `SettingsStore` (Persistence/atomic write), `SettingsMigrator`, `SettingsQuarantine`, Debounce. Modell wird schlank. 🟢 ∥
- **`ServiceProvider.cs` (258)** → nach Domänen gruppieren (Import-/KI-/Export-Bündel). Kein DI-Framework — nur ordnen. 🟢
- **`ShellViewModel.cs` (~547)** → niedrige Priorität; ggf. Save/Open- und KI-Status-Teil in eigene Helfer (breit, aber überschaubar). 🟢

## Querschnitt — Disziplin (dauerhaft, kein Einmal-Task)

- **Größen-Budget als Guard-Test:** z. B. „keine UI-Datei > ~800 Z." → verhindert das Nachwachsen. Langfristig wichtiger als jeder Einzelschnitt.
- **Regel:** neue Logik kommt in eine fokussierte Einheit, NICHT in die VM.

---

## Wenn du nur wenig machst

**Phase 0 + Phase 1** holen ~70 % des Lesbarkeits-Gewinns bei minimalem Risiko und laufen **parallel** (4–5 eigene Worktrees, disjunkt). Phase 3 nur mit Tests und Geduld — oder bewusst liegen lassen, bis sie wehtut.

## Parallelisierung konkret (sicher)

Gleichzeitig in eigenen Worktrees möglich, weil disjunkt:
**ProtocolPdfExporter · HoldingFolderDistributor · SystemMonitorService · TrainingCenterViewModel · CostCalculatorViewModel.**
Die DataGrid-Spalten-Factory (DataPage + Schaechte) ist **ein** serieller Auftrag. Page-Code-Behinds (Phase 3) **nie** parallel zur Spalten-Factory.
