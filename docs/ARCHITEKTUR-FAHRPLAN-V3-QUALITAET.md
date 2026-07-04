# Fahrplan V3 — Qualitäts-Optimierung SewerStudio

> **Grundlage:** Architektur-/Code-Review vom 2026-06-30 (`docs/REVIEW-Architektur-Code-2026-06-30.md`), Stand `feature/gis-karte @ cda3fc06`.
> **Ziel:** Jede Review-Dimension auf **mindestens B+**, Aspiration **A- bis A+**. Alle Änderungen **verhaltensneutral** (Charakterisierungs-Tests), keine fachliche Logik ohne Rückfrage.

---

## 1. Ausgangslage & Zielnoten

| Dimension | Ist | Pflicht (Welle 1) | Aspiration (Welle 2) | Hebel |
|---|---|---|---|---|
| Schichtenarchitektur | B+ | B+ (gehalten) | **A** | Reine Logik-Controller UI→Application (DEC-1) |
| Dekompositions-Qualität | B | **B+** | **A-** | Trivial-Klassen inline, Sink-Objekte (DEC-2), CostCalculator (DEC-3) |
| Kohärenz / 3-Linien-Merge | B | **A-** | A | Tote Konkurrenz-Duplikate killen (COH-1..4) |
| Robustheit / Fehler | A- | A- (gehalten) | **A** | Politur ROB-1..3 |
| Test-Qualität | B- | **B+** | **A-** | Brittle String-Guards raus (TQ-1..3) |
| Domänen-Korrektheit | A- | A- (gehalten) | **A** | Streckenschaden Single-Source (DOM-1) + DOM-2..4 |
| UI / MVVM | B | **B+** | **A-** | Disposal-Leaks, Threading (UI-1..5) |

**Nach Welle 1:** alle ≥ B+. **Nach Welle 2:** alle A-/A.

---

## 2. Prinzipien & Koordinations-Protokoll (NICHT brechen)

1. **Whole-File-Ownership — kollisionsfrei:**
   - **CLAUDE-Lane** = `src/AuswertungPro.Next.Domain/`, `…Application/`, `…Infrastructure/` + deren Test-Projekte.
   - **CODEX-Lane** = `src/AuswertungPro.Next.UI/**` + `tests/AuswertungPro.Next.UI.Tests/`.
   - Keine Datei in beiden Lanes. Jede Lane in **eigenem Worktree** (Lehre aus rogue-cleanup-hazard: ein Worktree pro Agent).
   - Branches off `feature/gis-karte @ cda3fc06`: `refactor/quality-backend` (Claude), `refactor/quality-ui` (Codex).
2. **Verhaltensneutral + test-first:** Vor jeder Extraktion/Konsolidierung Charakterisierungs-Test, der das Ist-Verhalten festnagelt. Keine fachliche Änderung (Severity-Schwellen, Code-Mapping) ohne explizite Rücksprache.
3. **Geteilte APIs über Handoff, nicht über geteilte Dateien:** Bei Konsolidierungen legt **Claude zuerst** die kanonische Application/Domain-Klasse (+ Interface + Tests) an; **danach** verdrahtet **Codex** die UI darauf um und löscht die UI-Duplikate. Reihenfolge ist bindend (siehe §5).
4. **Gate je Cluster:** voller `dotnet build` 0 Fehler + betroffene Tests grün. **Gate je Welle:** volle Suite grün; bei UI-Änderungen **manueller WPF-Smoke** der betroffenen Fenster.
5. **Nicht gepusht** ohne explizites OK. Merge der Lanes nach `feature/gis-karte` erst nach Welle-Gate + Smoke.

---

## 3. WELLE 1 — Boden: alle Dimensionen auf ≥ B+

### Q1 — Backend-Konsolidierung der Merge-Narben · **CLAUDE** · [Kohärenz, Schichten]
**Befunde:** COH-1, COH-2, COH-3, COH-4.
**Dateien:**
- `src/AuswertungPro.Next.Infrastructure/Import/Xlsx/SchaechteTemplateColumnReader.cs` → **löschen** (null Referenzen, verifiziert).
- `src/AuswertungPro.Next.Application/DataPage/DnValueParser.cs` → kanonische `TryParseMillimeters` sicherstellen, Test bleibt.
- `src/AuswertungPro.Next.Application/DataPage/SchaechteFieldLogic.cs` → kanonische Such-/Nr-Spalten-Logik sicherstellen, Test bleibt.
**Schritte:** (1) COH-3-Datei löschen, Build grün. (2) Application-Versionen als Single Source of Truth prüfen/ergänzen, sodass sie exakt das Verhalten der produktiven UI-Varianten (`ParseDnMm`, `SchaechteSearchMatcher`, `ResolveNrColumnName`) abdecken. (3) Charakterisierungs-Tests, die genau diese Verhaltensgleichheit absichern.
**Abnahme:** Application trägt die einzige Wahrheit + Tests; tote Infrastructure-Datei weg; Build/Tests grün. **Handoff an Q2.**

### Q2 — UI-Umverdrahtung der Merge-Narben · **CODEX** (nach Q1) · [Kohärenz → A-]
**Befunde:** COH-1, COH-2, COH-4.
**Dateien:** `src/AuswertungPro.Next.UI/DataPage/DataPageHydraulikReportCalculator.cs`, `…/SchaechteSearchMatcher` (bzw. Service), `src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs:375`.
**Schritte:** UI auf die Application-Klassen (`DnValueParser`, `SchaechteFieldLogic`) umstellen; UI-Duplikat-Logik (`ParseDnMm`, inline `ResolveNrColumnName`, redundanter Matcher) löschen; UI-Tests auf die genutzte Implementierung umbiegen.
**Abnahme:** keine doppelten Logik-Pfade mehr; UI delegiert nach Application; Suite grün.

### Q3 — UI-Lebenszyklus & Leaks · **CODEX** · [UI/MVVM → B+]
**Befunde:** UI-1, UI-2.
**Dateien:** `…/ViewModels/Pages/DataPageViewModel.cs:152,169`, `OverviewPageViewModel.cs:55`, `ProjectPageViewModel.cs:87`. Referenz-Muster: `BuilderPageViewModel` (macht es korrekt).
**Schritte:** Die drei Page-VMs auf `IDisposable` umstellen, `_shell.PropertyChanged` mit benanntem Handler abonnieren + in `Dispose()` abmelden; `DataPageViewModel.Dispose()` ruft `LiveControlRetryBridge.Reset()` (nur wenn eigener Handler registriert). `DisposeIfReplaced` räumt dann automatisch auf.
**Abnahme:** kein Page-VM bleibt nach Seitenwechsel am Shell-Singleton hängen; Disposal-Test.

### Q4 — UI-Threading & View-Politur · **CODEX** · [UI/MVVM → A-, stützt Robustheit]
**Befunde:** UI-3, UI-4, UI-5.
**Dateien:** `…/Ai/CodingFrameExtractionService.cs:49`, `…/Views/Pages/DataPage.xaml.cs:147`, `…/Views/Windows/VsaCodeExplorerWindow.xaml.cs:249`.
**Schritte:** ffmpeg-Frame-Extraktion async durchziehen oder per `Task.Run` aus dem Codier-Hotpath nehmen (Muster: `SystemMonitorService`); Inline-Style-Aufbau in eine `DataGridWrappingTextColumnFactory` auslagern; blockierendes `Dispatcher.Invoke` → `BeginInvoke`/`IUiThread`.
**Abnahme:** kein UI-Thread-Block im Foto-/Grenzereignis-Pfad; Factory-Muster konsistent.

### Q5 — Test-Hygiene · **CODEX** · [Test-Qualität → B+/A-]
**Befunde:** TQ-1, TQ-2, TQ-3.
**Dateien:** `tests/…UI.Tests/TrainingCenterBatchImportArchitectureTests.cs`, `…/UiArchitectureGuardTests.cs` (9411 Z.), `…/DataPageCommandArchitectureTests.cs:45`.
**Schritte:** ~264 Quelltext-String-Guards aussondern (die ausgelagerten Controller haben bereits echte Verhaltenstests); die echten Architektur-Fitness-Tests (Service-Locator-Verbot, Layer-Checks) in eine kleine `ArchitectureFitnessTests.cs` extrahieren und behalten; doppelte `ExtractMethodBody`-Helfer entfernen. Wo Verdrahtung getestet werden soll: VM mit Fakes aufrufen + beobachtbare Wirkung prüfen.
**Abnahme:** Brittle Substring-Pins weg; refactor-robuste Fitness-Tests bleiben; Suite grün (Testzahl sinkt bewusst).

### Q6 — Dekompositions-Boden · **CODEX** · [Dekomposition → B+]
**Befunde:** DEC-2, DEC-4 + Trivial-Klassen aus DEC-1.
**Dateien:** `…/ViewModels/Windows/TrainingCenterViewModel.cs:872,1444`, triviale `…/Ai/Training/*Controller.cs` (<1 KB, reine Indirektion).
**Schritte:** Getyptes Sink-/Kontext-Objekt (`record TrainingBatchUiSink { Log; SetStatus; SetProgress; … }`) einführen, 15-Parameter-Delegate-Listen auf 2-3 reduzieren; ~22 triviale Drei-Zeiler-Controller inline zurücknehmen; doppelten `<summary>`-Block konsolidieren.
**Abnahme:** keine 15-Delegate-Signaturen mehr; spürbar weniger Mikro-Indirektion; verhaltensneutral.

### Q7 — Robustheits-Politur · **CLAUDE** · [Robustheit → A]
**Befunde:** ROB-1, ROB-2, ROB-3.
**Dateien:** `…/Infrastructure/…/FeedbackIngestionService.cs:106`, `…/CodingSessionService.cs:194`, `…/Import/…/KiasFdbTopologyReader.cs:144`.
**Schritte:** stillen Weight-Learning-`catch` mit `Debug.WriteLine` angleichen; Session-Abschluss async durchziehen oder dokumentiert entkoppeln; FDB-Reader auf `CultureInfo.InvariantCulture` + feldweises `TryConvert` (eine Zeile kippt nicht den ganzen Stammdaten-Block).
**Abnahme:** keine vollständig stillen Catches; FDB-Import robust gegen Typ-/Kultur-Ausreißer.

### Q8 — Domänen-Härtung · **CLAUDE** · [Domäne → A]
**Befunde:** DOM-1 (Kern), DOM-2, DOM-3, DOM-4.
**Dateien:** `…/Domain/VsaCatalog/StreckenschadenCodeClassifier.cs:15`, `…/Infrastructure/Ai/VsaCodeResolver.cs:397`, `…/TemporalCodeVotingService.cs:83`, `…/QuantificationSeverityPolicy.cs:11`.
**Schritte:** DOM-1 = Domain-Classifier kanonisch, `VsaCodeResolver.IsStreckenschadenCode` delegiert nach der `RequiresRange`-Prüfung dorthin + **Konsistenz-Test über alle Katalog-Codes**. DOM-2 = `_lastConfirmedMeter = 0` in `Reset()`. DOM-4 = BBD-Präfix kommentieren („nur Präfix-Anker, kein gültiger Basiscode"). **DOM-3 (Severity-Deckel bei 4) NUR nach Rückfrage** — fachliche Entscheidung, evtl. nur kommentieren.
**Abnahme:** eine Streckenschaden-Quelle + Konsistenz-Test; latente Reset-Lücke zu; keine fachliche Änderung ohne Freigabe.

**→ Welle-1-Gate:** beide Lanes nach `feature/gis-karte` mergen (Q1 vor Q2 etc.), volle Suite grün, WPF-Smoke (DataPage, Schächte, TrainingCenter, VsaCodeExplorer, Coding-Foto-Pfad). **Erwartetes Ergebnis: alle Dimensionen ≥ B+.**

---

## 4. WELLE 2 — Aspiration: A- bis A+

### Q9 — Logik-Controller UI → Application · **CLAUDE führt, CODEX folgt** · [Schichten → A, Dekomposition → A-]
**Befunde:** DEC-1, LAYER-1, COH-4-Rest.
**Umfang:** ~69 WPF-freie Controller unter `src/AuswertungPro.Next.UI/Ai/Training/` (reine Entscheidungsregeln/Zustandsmaschinen) nach `src/AuswertungPro.Next.Application/Ai/Training/` verschieben + in Unter-Namespaces gliedern (`SelfTraining/`, `BatchImport/`, `ReviewQueue/`, `Presentation/`).
**Koordinierter Ablauf (kein Parallelzugriff auf dieselbe Datei):**
1. Codex pausiert Arbeit an `TrainingCenterViewModel.cs`.
2. Claude verschiebt die Dateien physisch nach Application, setzt Namespaces, baut Application grün (die Tests ziehen mit).
3. Codex aktualisiert `using`-Direktiven/Referenzen in den UI-Konsumenten (v. a. `TrainingCenterViewModel`), Build grün.
**Faustregel als Konvention:** Datei ohne `System.Windows`-Bezug = Application-Kandidat. **Risiko:** breiter mechanischer Move → strikt verhaltensneutral, Tests als Netz.
**Abnahme:** Training-Logik liegt in Application, ist ohne UI-Assembly testbar; UI/Ai/Training enthält nur noch WPF-nahe Adapter.

### Q10 — CostCalculator entzerren · **CLAUDE (Logik) + CODEX (VM)** · [Dekomposition → A-/A]
**Befunde:** DEC-3.
**Dateien:** `…/ViewModels/Windows/CostCalculatorViewModel.cs:22` (1595 Z., 3 VM-Klassen, Domänenlogik im VM).
**Ablauf:** Claude extrahiert `CostConsistencyChecker` + `MeasureQuantityDeriver` (Set-Dn/Length/Connections, Katalog-Preise) als getestete Application-Services — gleiche Muster wie Training-Bereich. Danach Codex dünnt das VM aus (orchestriert nur, ruft Services).
**Abnahme:** Kostenlogik testbar in Application; VM < ~600 Z., reine Orchestrierung; Stil konsistent mit Training.

### Q11 — Rest-Geometrie & Schicht-Politur · **CODEX + CLAUDE** · [Dekomposition/Schichten Politur]
**Befunde:** DEC-5, LAYER-2, LAYER-3, LAYER-4.
**Schritte:** Codex: `PhotoMeasurementWindow.xaml.cs` Rest-Mathematik (Hit-Testing/Kalibrierung) in `GeometryService` ziehen + testen; Bilddekodierung (WPF-Imaging) von reiner Bewertung trennen (LAYER-4). Claude: Transport-Wissen aus `Application/Ai/EnhancedVisionModels.cs:41` in neutralen Fehlertyp (`SidecarUnavailableException`) im `VisionPipelineClient` mappen (LAYER-2); wo VM-Orchestrierung getestet werden soll, Interface via Default-Parameter injizierbar machen (LAYER-3, konsistent mit „kein MS-DI").
**Abnahme:** keine untestbare Rest-Mathematik in Code-Behind; Application kennt keine HTTP-/Socket-Typen mehr.

**→ Welle-2-Gate:** volle Suite grün, WPF-Smoke (TrainingCenter, CostCalculator, PhotoMeasurement). **Erwartetes Ergebnis: alle Dimensionen A-/A.**

---

## 5. Lane-Split & Sequenzierung

| Cluster | Owner | Welle | Dimension(en) | Abhängigkeit |
|---|---|---|---|---|
| Q1 Backend-Konsolidierung | Claude | 1 | Kohärenz, Schichten | — |
| Q2 UI-Umverdrahtung | Codex | 1 | Kohärenz | **nach Q1** |
| Q3 UI-Leaks/Lifecycle | Codex | 1 | UI | — |
| Q4 UI-Threading/Politur | Codex | 1 | UI, Robustheit | — |
| Q5 Test-Hygiene | Codex | 1 | Test-Qualität | — |
| Q6 Dekompositions-Boden | Codex | 1 | Dekomposition | — |
| Q7 Robustheits-Politur | Claude | 1 | Robustheit | — |
| Q8 Domänen-Härtung | Claude | 1 | Domäne | — |
| Q9 Controller UI→Application | Claude+Codex | 2 | Schichten, Dekomposition | nach Welle-1-Merge |
| Q10 CostCalculator | Claude+Codex | 2 | Dekomposition | Claude vor Codex |
| Q11 Geometrie/Schicht-Politur | Codex+Claude | 2 | Dekomposition, Schichten | — |

**Kritische Reihenfolge:** Q1→Q2 (Backend-Quelle vor UI-Umverdrahtung). Q9/Q10 sind die einzigen echten Cross-Lane-Handoffs — dort koordiniert (Owner-Wechsel auf derselben logischen Einheit, nie gleichzeitig dieselbe Datei). Innerhalb einer Lane sind Q3–Q6 (Codex) bzw. Q7–Q8 (Claude) unabhängig und frei sequenzierbar.

---

## 6. Definition of Done je Dimension

- **Kohärenz → A-:** keine toten Konkurrenz-Klassen mehr (COH-1..4 erledigt); je Konzept eine Implementierung in Application.
- **UI/MVVM → A-:** keine leakenden Page-VMs (alle `IDisposable`); kein UI-Thread-Block im Hotpath; statischer Bridge-Root entschärft.
- **Test-Qualität → A-:** brittle Substring-Guards weg; refactor-robuste Fitness-Tests in eigener Datei; Suite grün.
- **Dekomposition → A-:** keine 15-Delegate-Signaturen; CostCalculator entzerrt; Logik-Controller in Application; Mikro-Klassen reduziert.
- **Schichten → A:** keine reine Logik mehr in der UI-Schicht; Application-platziert + testbar.
- **Robustheit → A:** keine stillen Catches; FDB-Import kultur-/typ-robust.
- **Domäne → A:** Streckenschaden Single-Source + Konsistenz-Test; Reset vollständig.

**Gesamtziel:** von B (gut) auf **A- (sehr gut, konsistent poliert)**.

---

## 7. Verifikation pro Schritt

```bash
dotnet build AuswertungPro.sln          # 0 Fehler
dotnet test  AuswertungPro.sln           # alle grün (Testzahl in Q5 bewusst niedriger)
```
Plus manueller WPF-Smoke der je Welle betroffenen Fenster (Test-Suite deckt WPF-Verhalten nicht ab).
