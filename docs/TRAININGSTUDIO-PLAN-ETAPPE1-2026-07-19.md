# Training Studio — Implementierungsplan Etappe 1: Pruefplatz (2026-07-19)

> **Fuer die Umsetzungs-Sitzung (Opus):** Diesen Plan Aufgabe fuer Aufgabe abarbeiten,
> Checkboxen (`- [ ]`) pflegen. Test zuerst, dann Implementierung, dann Commit.
> Bei Abweichung zwischen Plan und echtem Code gilt der echte Code — Abweichung im
> Commit-Text dokumentieren. *(Plan-Ausfuehrungs-Skills optional, nicht erforderlich.)*

**Ziel:** Der Pruefplatz aus dem Design [TRAININGCENTER-REDESIGN-2026-07-19.md](TRAININGCENTER-REDESIGN-2026-07-19.md):
Foto/Frame anzeigen → Box ziehen → SAM segmentiert → KI-Vorschlag → Code setzen →
geprueftes Sample in KB + Teacher-Pool. Neues Fenster `TrainingStudioWindow`, additiv —
das alte TrainingCenterWindow bleibt unveraendert.

**Architektur:** Ein neuer Orchestrierungs-Service `IAnnotationWorkbenchService`
(Vertrag in Application, Implementierung in UI wie beim SAM-Review-Muster) buendelt
Segmentierung, Vorschlag und Speichern. Das Fenster ist duenn (DependencyFactory-Muster),
ViewModel mit CommunityToolkit.Mvvm. Alle Werte via `docs/SYSTEM-FAKTEN.md`.

**Tech:** WPF/.NET 10, CommunityToolkit.Mvvm 8.4.0, bestehender Sidecar (Port 8100).

## Globale Randbedingungen

- Kommentare Deutsch. Keine neuen NuGet-Pakete.
- Bestehende Klassen NICHT umbauen — nur additiv nutzen. Das alte TrainingCenterWindow bleibt unberuehrt.
- Eval-Schutz ist hart: Speichern prueft `EvalContaminationGuard.ClassifyForExport` VOR jedem Schreiben; bei Treffer klare Fehlermeldung, kein Speichern.
- KB-Index-Regeln beachten: `Status=Approved`, `HumanConfirmed=true`, Beschreibung ≥ 10 Zeichen, Code via `VsaCodeResolver.LookupLabel` aufloesbar.
- Das Korrektur-Flag heisst `Corrected` (bool?) — ein Feld `IsKorrigiert` existiert NICHT.
- Statische Fassaden (`TrainingSamplesStore.Current`, `TeacherAnnotationStore.Current`) nicht neu verdrahten — Interfaces injizieren.
- Fenster-Muster: kein DI fuer Fenster; `internal record Dependencies` + `internal static Factory.Create(ServiceProvider? services)` mit Designer-Fallback; `WindowStateManager.Track(this)` im Ctor; Theme nur per `DynamicResource` (Brush-/Style-Keys aus `Theme/ThemeLight.xaml`).
- Nach jeder Aufgabe: `dotnet build AuswertungPro.sln` (0 Fehler/0 Warnungen im geaenderten Bereich) und betroffene Tests gruen.

## Verifizierte Kern-APIs (Kurzreferenz, von den API-Scouts belegt)

| Baustein | Exakte Signatur / Fakt |
|---|---|
| SAM-Service | `ITrainingReviewSamSegmentationService.SegmentFrameFileAsync(string framePath, BoundingBox box, string code, int? pipeDiameterMm = null, CancellationToken ct = default)` → `TrainingReviewSamResult(SamResponse Response, IReadOnlyList<MaskQuantificationService.QuantifiedMask> QuantifiedMasks)` — Box ist die NORMIERTE YOLO-Box (0..1, Mittelpunkt+Groesse); Pixel-Umrechnung macht der Service |
| Box-Typ | `BoundingBox` (readonly record struct, `Application/Ai/Training/BoundingBox.cs`): `TryCreate(double xc, double yc, double w, double h, out BoundingBox box)` validiert; `ApplyTo(TrainingSample sample)` schreibt Bbox-Felder |
| Classify | `IVisionPipelineClient.ClassifyYoloAsync(new YoloClassifyRequest(imageBase64, topK), ct)` → `YoloClassifyResponse.Predictions` (`ClassName`, `Confidence`); Felder `Usable`/`QualityReason` = Frame-Quality-Gate, `IsBend` = Bogen-Veto. KEIN Crop-Parameter — Crop vorher zuschneiden und als Base64 senden |
| Retrieval | `sp.Retrieval` ist `IRetrievalService?` (NULLABLE) — `RetrieveAsync(string queryText, int topK = 5, CancellationToken ct)` → `IReadOnlyList<RetrievalResult>` (`RetrievalResult(SampleRecord Sample, double Score)`) |
| Sample speichern | NEU: `ITrainingSampleStore.MergeAndSaveAsync` (gleiche Signature wird NICHT ueberschrieben) · UPDATE: `MergeOrUpdateAsync` · NIE `SaveAsync` fuer Einzel-Updates |
| Signatur | `TrainingSample.BuildCanonicalSignature(caseId, code, meterCenter, meterEnd)` → `"{caseId}\|{code}\|{m:F1}\|{m:F1}"` |
| Gold-Fund-Felder | `Status=TrainingSampleStatus.Approved`, `HumanConfirmed=true`, `Corrected=false/true`, `ConfirmedByUser`, `ConfirmedAtUtc=DateTime.UtcNow`, `KbIndexState=KbIndexState.Pending` vor Index |
| KB-Index | `IKnowledgeBaseIndexer.IndexAsync(IReadOnlyList<TrainingSample>, ct)` → `KbIndexOutcome`; danach `IsIndexed→Indexed`, `IsSkipped→Skipped` (kein Retry), sonst `Error` |
| Eval-Schutz | `EvalContaminationGuard.LoadEvalImageHashes(root)` + `LoadEvalHaltungKeys(root)` + `ClassifyForExport(hashes, holdings, framePath, caseId)` → `Clean / EvalImageHash / EvalHaltung` |
| Teacher-Schreiber | `TrainingAnnotationExportService.ExportAsync(sourceFramePath, NormalizedBoundingBox bbox, string vsaCode, int classId, string baseName, ct)`; Erzeugung via `TrainingAnnotationExportServiceFactory.Create(ITeacherAnnotationStore)`; ClassId via `IVsaYoloClassMapStore.GetOrAddClassId` (Teacher-Karte darf wachsen) |
| Teacher-Ablage | `<KnowledgeRoot>\teacher_annotations.json`, `teacher_images\`, `teacher_labels\`; `ITeacherAnnotationStore.AppendAsync(params TeacherAnnotation[])` (idempotent per AnnotationId) |
| **Bekannte Luecke** | KEIN bestehender Teacher-Weg setzt `TeacherAnnotation.HaltungName`/`VideoPath` → Disposition `QuarantineOrigin` (288 Faelle). **Der neue Weg setzt beide Felder, wenn bekannt.** |
| Vorbild Approve/Reject | `ReviewApprovalService` (`Infrastructure/Ai/Training/ReviewApprovalService.cs`): Approve-Feldfolge und `_corr`-Mechanik (`SampleId_corr`, neue Signatur, `Corrected=true`) — als Muster fuer den Save-Step lesen |
| Fenster-Vorbilder | `TrainingCenterWindowDependencyFactory` (Dependencies-Record + Create), `HydraulikPanelWindow` (VM per Ctor, `DataContext = vm`, Closed haengt Handler ab) |
| VM-Muster | `public partial class X : ObservableObject` + `[ObservableProperty] private ...` + `[RelayCommand]` |

## Dateistruktur (neu)

```text
src/AuswertungPro.Next.Application/Ai/Workbench/
  AnnotationWorkbenchModels.cs        (DTOs: Item, Suggestion, Decision, SaveResult)
  IAnnotationWorkbenchService.cs      (Vertrag)
src/AuswertungPro.Next.UI/Services/
  AnnotationWorkbenchService.cs       (Implementierung: SAM + Vorschlag + Speichern)
src/AuswertungPro.Next.UI/ViewModels/
  TrainingStudioViewModel.cs          (Zustand + Commands, duenn)
src/AuswertungPro.Next.UI/Views/Windows/
  TrainingStudioWindow.xaml(.cs)      (Layout + Box-Zeichnen + Tasten)
  TrainingStudioWindowDependencyFactory.cs
tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/
  AnnotationWorkbenchServiceTests.cs
  TrainingStudioViewModelTests.cs
```

---

## Aufgabe 1: Vertraege + DTOs (Application)

**Dateien:** Create `src/AuswertungPro.Next.Application/Ai/Workbench/AnnotationWorkbenchModels.cs`, `IAnnotationWorkbenchService.cs`

**Produziert (spaetere Aufgaben verlassen sich exakt hierauf):**

```csharp
namespace AuswertungPro.Next.Application.Ai.Workbench;

/// <summary>Ein zu pruefendes Bild samt Kontext. FramePath ist Pflicht; Haltungsdaten optional (Fotos).</summary>
public sealed record WorkbenchItem(
    string FramePath,
    string CaseId,                    // Haltungskennung oder "foto_<yyyyMMdd>_<lfd>"
    double MeterStart,
    double MeterEnd,
    string? HaltungName,              // wenn bekannt: schliesst die QuarantineOrigin-Luecke
    string? VideoPath,
    int? PipeDiameterMm);

/// <summary>Codevorschlag der KI zu einer gezogenen Box.</summary>
public sealed record WorkbenchSuggestion(
    IReadOnlyList<WorkbenchCodeCandidate> Candidates,   // absteigend nach Confidence
    bool FrameUsable,                 // false = Quality-Gate des Sidecars (unscharf/dunkel)
    string QualityReason,
    bool IsBend);                     // Bogen-Veto-Signal

public sealed record WorkbenchCodeCandidate(string VsaCode, double Confidence, string Quelle); // Quelle: "cls" | "kb"

/// <summary>Entscheidung des Menschen zu einer Box.</summary>
public sealed record WorkbenchDecision(
    string VsaCode,                   // finaler Code (bei Akzeptieren = Vorschlag)
    bool WasCorrected,                // true wenn vom Top-Vorschlag abgewichen
    string Beschreibung,              // >= 10 Zeichen (UI generiert Vorlage, editierbar)
    double? ClockPosition,
    int? Severity,
    string ConfirmedByUser);

public sealed record WorkbenchSaveResult(
    bool Saved,
    string? RefusalReason,            // gesetzt bei Eval-Abweisung oder Validierungsfehler
    string? SampleId,
    string KbIndexState,              // "Indexed" | "Skipped" | "Error" | "-"
    string? TeacherAnnotationId);
```

`IAnnotationWorkbenchService.cs`:

```csharp
namespace AuswertungPro.Next.Application.Ai.Workbench;

using AuswertungPro.Next.Application.Ai.Training;   // BoundingBox

public interface IAnnotationWorkbenchService
{
    /// <summary>Segmentiert die normierte Box per SAM. Liefert Maske(n) + Quantifizierung.</summary>
    Task<WorkbenchSegmentation> SegmentAsync(WorkbenchItem item, BoundingBox box, string codeHint, CancellationToken ct = default);

    /// <summary>Erzeugt den KI-Codevorschlag zur Box (cls-Klassifikator + aehnliche KB-Faelle).</summary>
    Task<WorkbenchSuggestion> SuggestAsync(WorkbenchItem item, BoundingBox box, CancellationToken ct = default);

    /// <summary>Speichert die menschliche Entscheidung: Eval-Schutz, TrainingSample, KB-Index, Teacher-Kandidat.</summary>
    Task<WorkbenchSaveResult> SaveAsync(WorkbenchItem item, BoundingBox box, WorkbenchSegmentation? segmentation, WorkbenchDecision decision, CancellationToken ct = default);
}
```

`WorkbenchSegmentation` (in Models-Datei): Record mit `string? MaskRle, int MaskImageWidth, int MaskImageHeight, double? AreaPercent, string StatusText, bool Degraded` — bewusst UI-frei (kein WPF-Typ in Application).

- [ ] **Schritt 1:** Beide Dateien anlegen (Code oben, XML-Doku Deutsch).
- [ ] **Schritt 2:** `dotnet build AuswertungPro.sln` → 0 Fehler.
- [ ] **Schritt 3:** Commit `feat(workbench): Vertraege fuer den Pruefplatz (Etappe 1)`.

## Aufgabe 2: AnnotationWorkbenchService — Segmentieren + Vorschlagen (TDD)

**Dateien:** Create `src/AuswertungPro.Next.UI/Services/AnnotationWorkbenchService.cs`; Test `tests/AuswertungPro.Next.UI.Tests/Ai/Workbench/AnnotationWorkbenchServiceTests.cs`

**Konstruktor (alle Abhaengigkeiten als Interfaces/Delegates, Vorbild TrainingReviewSamWorkflow):**

```csharp
public sealed class AnnotationWorkbenchService : IAnnotationWorkbenchService
{
    public AnnotationWorkbenchService(
        ITrainingReviewSamSegmentationService samService,       // vorhandener SAM-Weg (UI/Services)
        IVisionPipelineClient pipelineClient,                   // ClassifyYoloAsync
        IRetrievalService? retrieval,                           // nullable! (sp.Retrieval)
        ITrainingSampleStore sampleStore,
        IKnowledgeBaseIndexer kbIndexer,
        ITeacherAnnotationStore teacherStore,
        IVsaYoloClassMapStore teacherClassMap,
        Func<string, byte[]> readFileBytes,                     // testbar statt File.ReadAllBytes
        Func<string?> resolveEvalSetRoot)                       // Default: () => TrainingSamplesStore.EffectiveEvalSetRoot
}
```

**SegmentAsync:** delegiert an `samService.SegmentFrameFileAsync(item.FramePath, box, codeHint, item.PipeDiameterMm, ct)`;
aus dem Ergebnis die ERSTE Maske mit nicht-leerem `MaskRle` nehmen (Muster TrainingReviewSamWorkflow); `Degraded`/`SkippedBoxes>0` der `SamResponse` in `StatusText` ausweisen („Teil-Segmentierung — pruefen").

**SuggestAsync:**
1. Frame-Bytes lesen → Base64 → `ClassifyYoloAsync(new YoloClassifyRequest(b64, 5), ct)` (Whole-Frame, wie produktiv ueblich).
2. Kandidaten = `Predictions` als `WorkbenchCodeCandidate(ClassName, Confidence, "cls")`.
3. Wenn `retrieval != null`: `RetrieveAsync($"{topCode} {WorkbenchItemText}", 3, ct)`; KB-Treffer als Kandidaten `(Sample.VsaCode, Score, "kb")` anhaengen; gleiche Codes zusammenfassen (max Confidence gewinnt).
4. `FrameUsable=resp.Usable`, `QualityReason`, `IsBend` durchreichen (UI zeigt Warnhinweis; bei `IsBend` Hinweis „kein BCE codieren").

- [ ] **Schritt 1 (Test zuerst):** Fakes fuer `ITrainingReviewSamSegmentationService`, `IVisionPipelineClient`, `IRetrievalService`. Tests: (a) `SegmentAsync` liefert erste nicht-leere Maske + Degraded-Hinweis; (b) `SuggestAsync` mischt cls- und kb-Kandidaten, dedupliziert per Code, sortiert absteigend; (c) `retrieval == null` → nur cls-Kandidaten, kein Fehler; (d) `Usable=false` wird durchgereicht.
- [ ] **Schritt 2:** Tests laufen lassen → FAIL (Service existiert nicht).
- [ ] **Schritt 3:** `SegmentAsync` + `SuggestAsync` implementieren (SaveAsync wirft vorerst `NotImplementedException`).
- [ ] **Schritt 4:** Tests gruen.
- [ ] **Schritt 5:** Commit `feat(workbench): Segmentierung + KI-Vorschlag im Workbench-Service`.

## Aufgabe 3: AnnotationWorkbenchService.SaveAsync — Speichern mit Schutznetz (TDD)

**Ablauf (exakt diese Reihenfolge, Vorbild `ReviewApprovalService`):**

```csharp
public async Task<WorkbenchSaveResult> SaveAsync(...)
{
    // 1) Validierung: decision.Beschreibung >= 10 Zeichen; VsaCodeResolver.LookupLabel(decision.VsaCode) != null
    //    sonst: Saved=false, RefusalReason (klare deutsche Meldung)
    // 2) EVAL-SCHUTZ (hart, VOR jedem Schreiben):
    var root = _resolveEvalSetRoot();
    var hashes = EvalContaminationGuard.LoadEvalImageHashes(root);
    var holdings = EvalContaminationGuard.LoadEvalHaltungKeys(root);
    var verdict = EvalContaminationGuard.ClassifyForExport(hashes, holdings, item.FramePath, item.CaseId);
    if (verdict != EvalContaminationGuard.ExportContaminationResult.Clean)
        return new WorkbenchSaveResult(false, $"Eval-Schutz: Bild gehoert zum eingefrorenen Mess-Set ({verdict}). Nicht speicherbar.", null, "-", null);
    // 3) TrainingSample bauen:
    //    SampleId = $"wb_{Guid.NewGuid():N}"[..15]; CaseId/Code/Beschreibung/Meter aus item+decision;
    //    Signature = TrainingSample.BuildCanonicalSignature(item.CaseId, decision.VsaCode, item.MeterStart, item.MeterEnd);
    //    Status=Approved, HumanConfirmed=true, Corrected=decision.WasCorrected,
    //    ConfirmedByUser/ConfirmedAtUtc, QualityGateLevel="Green",
    //    SourceType=SourceTypeNames.ManualCoding, MatchLevel=decision.WasCorrected ? ReviewCorrected : ReviewApproved,
    //    FramePath=item.FramePath, KbIndexState=Pending;
    //    box.ApplyTo(sample); Maskenfelder aus segmentation (SamMaskRle/Width/Height) wenn vorhanden.
    // 4) await _sampleStore.MergeAndSaveAsync(new List<TrainingSample> { sample });
    // 5) var outcome = await _kbIndexer.IndexAsync(new[]{ sample }, ct);
    //    KbIndexState aus outcome (IsIndexed→Indexed, IsSkipped→Skipped, sonst Error); via MergeOrUpdateAsync persistieren.
    // 6) Teacher-Kandidat: classId = _teacherClassMap.GetOrAddClassId(decision.VsaCode);
    //    bbox = new NormalizedBoundingBox(box.XCenter, box.YCenter, box.Width, box.Height);
    //    exportService (via TrainingAnnotationExportServiceFactory.Create(_teacherStore)) .ExportAsync(item.FramePath, bbox, code, classId, $"wb_{annotationId}", ct);
    //    TeacherAnnotation mit VsaCode, Beschreibung, Severity, MeterPosition=item.MeterStart, BoundingBox=bbox,
    //    ClockPosition, FullFramePath/CroppedRegionPath/YoloAnnotationPath aus Exportergebnis
    //    UND HaltungName=item.HaltungName, VideoPath=item.VideoPath   // <-- schliesst die QuarantineOrigin-Luecke
    //    → AppendAsync. Teacher-Fehler duerfen das Sample-Speichern nicht rueckgaengig machen → Fehler im Result-Text.
}
```

- [ ] **Schritt 1 (Tests zuerst, mit Fake-Stores):**
  (a) Golden-Pfad: genau EIN Sample gespeichert (Signature korrekt, Approved/HumanConfirmed/Corrected-Felder exakt), EIN KB-Index-Aufruf, EIN Teacher-Append **mit gesetztem HaltungName/VideoPath**;
  (b) Eval-Bild → `Saved=false`, RefusalReason enthaelt „Eval", KEIN Store-/Index-/Teacher-Aufruf;
  (c) Beschreibung < 10 Zeichen → abgewiesen vor allen Aufrufen;
  (d) `outcome.IsSkipped` → `KbIndexState="Skipped"` via MergeOrUpdateAsync;
  (e) Teacher-Export-Fehler → `Saved=true`, Fehlertext im Result (Sample bleibt).
- [ ] **Schritt 2:** Tests FAIL.
- [ ] **Schritt 3:** Implementieren.
- [ ] **Schritt 4:** Tests gruen.
- [ ] **Schritt 5:** Commit `feat(workbench): Speichern mit Eval-Schutz, KB-Index und Teacher-Kandidat`.

## Aufgabe 4: TrainingStudioViewModel (TDD)

**Dateien:** Create `src/AuswertungPro.Next.UI/ViewModels/TrainingStudioViewModel.cs`; Test `TrainingStudioViewModelTests.cs`

`public partial class TrainingStudioViewModel : ObservableObject` — Konstruktor:
`(IAnnotationWorkbenchService workbench, Func<IReadOnlyList<WorkbenchItem>> loadQueue, string confirmedByUser)`.

**Zustand ([ObservableProperty]):** `_items` (ObservableCollection\<WorkbenchItem\>), `_currentIndex`, `_currentImagePath`, `_currentBox` (BoundingBox?), `_segmentation` (WorkbenchSegmentation?), `_suggestion` (WorkbenchSuggestion?), `_selectedCode`, `_beschreibung`, `_clockPosition` (double?), `_severity` (int?), `_statusText`, `_isBusy`, `_queueDoneCount`.

**Commands ([RelayCommand], je eigenes CancellationTokenSource — KEIN geteilter Abbruch):**
- `BoxDrawnAsync(BoundingBox box)`: setzt `_currentBox`, ruft parallel `SegmentAsync` (codeHint = Top-cls oder "damage") und `SuggestAsync`; Ergebnisse in Zustand; `SelectedCode` = Top-Kandidat; `Beschreibung` = Vorlage `"{Label laut VsaCodeResolver} bei {Uhr}"` (editierbar).
- `AcceptAsync()`: `SaveAsync` mit `WasCorrected=false`; danach `NextItem()`.
- `CorrectAsync()`: wie Accept, aber `WasCorrected = SelectedCode != TopVorschlag`.
- `Discard()`: Box/Maske/Vorschlag verwerfen, Bild bleibt.
- `NextItem()` / `PreviousItem()`.
- Abweisungen (`Saved=false`) landen als deutliche Meldung in `StatusText` — niemals still.

- [ ] **Schritt 1 (Tests zuerst, Fake-Workbench):** (a) BoxDrawn fuellt Vorschlag+Maske und setzt SelectedCode auf Top-Kandidat; (b) Accept ruft SaveAsync mit `WasCorrected=false` und geht zum naechsten Item; (c) Code-Wechsel + Correct → `WasCorrected=true`; (d) Eval-Abweisung → StatusText enthaelt Meldung, Item bleibt; (e) zweiter BoxDrawn waehrend laufendem ersten → erster wird abgebrochen (eigene CTS je Aufruf).
- [ ] **Schritt 2:** FAIL → **Schritt 3:** implementieren → **Schritt 4:** gruen.
- [ ] **Schritt 5:** Commit `feat(workbench): TrainingStudio-ViewModel mit Tastatur-Arbeitsfluss`.

## Aufgabe 5: TrainingStudioWindow (XAML + Box-Zeichnen)

**Dateien:** Create `TrainingStudioWindow.xaml(.cs)`, `TrainingStudioWindowDependencyFactory.cs`; Modify `src/AuswertungPro.Next.UI/MainWindow.xaml(.cs)` (ein Menuepunkt „Training Studio (Vorschau)" neben dem bestehenden Training-Center-Eintrag, Aufruf `new TrainingStudioWindow(GetServiceProvider()) { Owner = this }.Show()`).

**Layout (Design Abschnitt 3, Modus 1):** Grid 2 Spalten (`*` Bild, `360` Panel) + untere Zeile Warteschlange.
Links `Image` + `Canvas`-Overlay fuer Box (Zeichnen via MouseDown/Move/Up im Code-behind ist hier ERLAUBT als reine Geometrie-Erfassung; Pixel→normiert umrechnen und `BoundingBox.TryCreate` — bei false: Statushinweis) und Maskenanzeige (vorhandenen `SamMaskRenderer` wiederverwenden, Grep nach Verwendung im TrainingCenterWindow als Vorbild).
Rechts: Vorschlagsliste (ItemsControl der Kandidaten mit Quelle-Badge cls/kb), Code-ComboBox, Uhr-Eingabe, Severity-Buttons 1–5, Buttons `Akzeptieren (A)` `Korrigieren (K)` `Verwerfen (V)`.
Tasten: `Window.InputBindings` → KeyBindings A/K/V/Right auf die Commands.
Theme: nur DynamicResource-Keys (`BgBrush`, `CardBrush`, `PrimaryButton`, `SuccessButton`, `ToolbarButton`, `Severity1Brush`…`Severity5Brush`).
**Achtung Hit-Test:** fuer Eltern-Suche im Visual Tree ausschliesslich `VisualTreeSafe.GetParentSafe`/vorhandene Behaviors verwenden (Crash-Muster auf Text-`Run`).

**DependencyFactory** (Vorbild TrainingCenterWindowDependencyFactory): baut `IAnnotationWorkbenchService` aus `sp` (`TrainingReviewSamSegmentationService` via vorhandener Fabrik, `IVisionPipelineClient` aus PipelineCfg wie `VisionPipelineTrainingReviewSamClient`, `sp.Retrieval`, Stores via Adapter, `TrainingSamplesStore.EffectiveEvalSetRoot`), plus `loadQueue`-Delegate (Aufgabe 6). `services == null` → Designer-Defaults.

- [ ] **Schritt 1:** Factory + Fenster + Menuepunkt bauen (VM per Ctor, `DataContext = vm`, `WindowStateManager.Track(this)`).
- [ ] **Schritt 2 (XAML-Binding-Pruefung):** jeden `{Binding ...}`-Pfad gegen die VM-Properties pruefen (xaml-binding-checker-Vorgehen) — Ergebnis im Commit-Text notieren.
- [ ] **Schritt 3:** `dotnet build` → 0 Fehler; App starten, Fenster oeffnen, Foto laden, Box ziehen → Maske + Vorschlag erscheinen (Sidecar muss laufen; ohne Sidecar: saubere Fehlermeldung im StatusText, kein Crash).
- [ ] **Schritt 4:** Commit `feat(ui): TrainingStudio-Fenster — Pruefplatz mit Box-Handgriff`.

## Aufgabe 6: Quellen — Fotos + Review-Warteschlange

**Dateien:** Create `src/AuswertungPro.Next.UI/Services/WorkbenchQueueService.cs` (+ Tests); Modify Factory (echtes `loadQueue`).

- **Fotoordner:** Dateidialog (jpg/png, Mehrfachauswahl) → je Datei ein `WorkbenchItem` mit `CaseId = $"foto_{DateTime.Now:yyyyMMdd}_{lfd}"`, Meter 0/0, HaltungName/VideoPath null, optionales DN-Eingabefeld im Fenster (Default leer → Service nutzt 300-mm-Default).
- **Review-Warteschlange:** `ITrainingSampleStore.LoadAsync()` → Samples mit `QualityGateLevel` Yellow/Red und `HumanConfirmed == null` und vorhandener `FramePath`-Datei → als `WorkbenchItem` (CaseId/Meter/FramePath aus Sample; vorhandene Bbox als vorgezogene Box anzeigen). Sortierung: Red vor Yellow, dann neueste zuerst.
- [ ] **Schritt 1 (Test zuerst):** Queue-Filter- und Sortierlogik (Fake-Store): nur Yellow/Red+unbestaetigt+Datei existiert; Red zuerst. → FAIL → implementieren → gruen.
- [ ] **Schritt 2:** Verdrahtung im Fenster (Buttons „Fotos laden…" / „Warteschlange laden").
- [ ] **Schritt 3:** Commit `feat(workbench): Foto- und Review-Quellen fuer den Pruefplatz`.

## Aufgabe 7: Golden-Pfad-Test + Abschluss

- [ ] **Schritt 1:** Integrationstest (Temp-Verzeichnisse, echte `TrainingSampleFileStore`+`TeacherAnnotationFileStore`, Fake-SAM/-Classify): Box → Save → exakt 1 Sample in `training_samples.json` (Signature/Felder exakt), 1 Teacher-Annotation **mit HaltungName**, Bild+Label-Datei existieren; zweiter Save mit gleicher Signatur legt KEIN Duplikat an.
- [ ] **Schritt 2:** `dotnet test AuswertungPro.sln` komplett gruen; `dotnet build` 0 Warnungen im neuen Code.
- [ ] **Schritt 3:** Sichtpruefung durch Pascal: Foto → Box → Maske → Vorschlag → `A` → Statuszeile zeigt „gespeichert + indexiert". 
- [ ] **Schritt 4:** Commit `feat(workbench): Golden-Pfad-Test Pruefplatz Etappe 1 abgeschlossen`.

---

## Ausblick Etappen 2–5 (grob — je ein eigener Detailplan, wenn dran)

2. **Player-Integration:** denselben `IAnnotationWorkbenchService` im PlayerWindow nutzen (Box auf Standbild; `WorkbenchItem` aus laufender Haltung — HaltungName/VideoPath IMMER gesetzt).
3. **Haltungs-Loop:** `IHaltungTrainingLoopService` — Soll-Stand-Store je Haltung, Vergleich via vorhandener ereignisbasierter Logik (`EvalSetEventScorer`-Wiederverwendung), Bestanden-Urteil, Verlaufsanzeige.
4. **Startseite + Messlatte:** `ITrainingPriorityService` (Priorisierung aus KB-/Sample-Bestand), Abdeckungskarte je Code, Eval-Lauf-Anbindung mit getrennter Anzeige geuebt/ungesehen.
5. **Umzug & Stilllegung:** Export-/Bestand-/Selbsttraining-Bereiche als eigene duenne Views ins Training Studio; altes Fenster stilllegen.

## Selbstpruefung (nach Etappe 1)

1. Kein neues Fenster-Code-behind mit Fach-/Dateilogik (nur Geometrie-Erfassung + Delegation)?
2. Eval-Schutz nachweislich VOR jedem Schreiben (Test b in Aufgabe 3)?
3. `HaltungName`/`VideoPath` bei bekannter Herkunft gesetzt (QuarantineOrigin-Luecke geschlossen)?
4. Kein geteilter CancellationTokenSource/IsBusy zwischen unabhaengigen Laeufen?
5. Altes TrainingCenterWindow unveraendert (git diff leer dort)?
