# Modul I-a — ReviewQueue-Engine & Approval-Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Die testbare Backend-Basis für Modul I „Review & Freigabe": Self-Training-NoFindings landen priorisiert in der Review-Queue, ein Kandidat ist stabil per `SampleId` adressierbar (mit Altbestand-Fallback), die Queue lädt/speichert verlustfrei, eine im Review gezogene Box überlebt Updates, und die Freigabe-/Ablehn-Orchestrierung liegt in einem **testbaren `ReviewApprovalService`** (nicht mehr im 2100-Zeilen-ViewModel).

**Architecture:** Reine Logik in der Application-Schicht (`SelfTrainingReviewRouting`, `TrainingSampleMerge`, `BoundingBox`, `ProtocolReviewCandidateFilter`), Persistenz/Orchestrierung in Infrastructure (`ReviewQueueService`, `ReviewApprovalService`). KB-Indizierung bleibt hinter `KnowledgeBaseManager` (Eval-Guard + Eligibility aus Phase 0 unverändert) und wird über die Abstraktion `IKnowledgeBaseIndexer` injiziert — der Service bleibt UI-frei und mockbar.

**Tech Stack:** C#/.NET 10, xUnit. Keine WPF-Abhängigkeit in 2a.

**Grundlage:** Spec `docs/superpowers/specs/2026-06-02-trainingsmodule-redesign-design.md` (§5/§6/§9/§11) + verifizierte Ist-Karte (2026-06-02). Phase 0 „Engine-Sicherheit" ist umgesetzt (NoFindings-Status, Deindex bei Reject, Eval-Guard, BBox-`HasBbox`-Härtung, YOLO-Gate). 2b (UI) setzt auf 2a auf.

**Verbindliche Schärfungen (User, 2026-06-02):** 2a = ReviewQueue + SampleId + ApprovalService + Tests. SampleId ins Queue-Item (alte Dateien weiter lesbar, Fallback CaseId+Code+Meter nur für Altbestand, neuer Code nutzt immer SampleId). Persistenz-Roundtrip-Test ist Pflicht-Schritt. §11-Refactor (`ReviewApprovalService`) jetzt in 2a. Lehrer-Tab nicht anfassen.

---

## File Structure

- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/ReviewQueueService.cs` — `SampleId` im Item + Persistenz + Pfad-injizierbare Factory + zentrale Priorität.
- Create: `src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingReviewRouting.cs` — Routing-Regel + Priorität.
- Create: `src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleMerge.cs` — Feld-Übernahme inkl. BBox.
- Create: `src/AuswertungPro.Next.Application/Ai/Training/BoundingBox.cs` — normierte Box + Validierung.
- Create: `src/AuswertungPro.Next.Application/Ai/Training/ProtocolReviewCandidateFilter.cs` — Protokoll-Startdaten-Filter.
- Create: `src/AuswertungPro.Next.Application/Ai/Training/IKnowledgeBaseIndexer.cs` + `ITrainingSampleStore.cs` + `IReviewApprovalService.cs` — Abstraktionen.
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Training/ReviewApprovalService.cs` + `KnowledgeBaseIndexerAdapter.cs` + `TrainingSamplesStoreAdapter.cs` — Implementierungen.
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingSamplesStore.cs:85-137` — nutzt `TrainingSampleMerge`.
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs:~1979-2003` — Enqueue nutzt `SelfTrainingReviewRouting`; (später) delegiert Approval an den Service.
- Test: `tests/AuswertungPro.Next.UI.Tests/ReviewQueueTests.cs` (erweitern), neue `tests/AuswertungPro.Next.Pipeline.Tests/{SelfTrainingReviewRoutingTests,TrainingSampleMergeTests,BoundingBoxTests,ProtocolReviewCandidateFilterTests}.cs`, `tests/AuswertungPro.Next.Infrastructure.Tests/{ReviewQueuePersistenceTests,ReviewApprovalServiceTests}.cs`.

**Reihenfolge:** Task 1 (SampleId) und Task 4/5 (Merge/Box) sind Voraussetzung für Task 6 (ApprovalService). 2,3,5 sind unabhängig. Empfohlen: 1 → 2 → 3 → 4 → 5 → 6.

---

## Task 1: `SampleId` im ReviewQueueItem + Persistenz (Roundtrip + Altbestand) + Pfad-Factory

**Zweck:** Ein Kandidat muss stabil per `SampleId` auffindbar sein (statt fragilem `CaseId/Code/Meter ± 0.2`). Alte `review_queue.json` ohne `SampleId` müssen weiter laden; Fallback nur für Altbestand.

**Files:** Modify `ReviewQueueService.cs`; Create `tests/AuswertungPro.Next.Infrastructure.Tests/ReviewQueuePersistenceTests.cs`.

- [ ] **Step 1: Pfad-injizierbare Test-Factory bereitstellen**

`ReviewQueueService` hat heute `CreatePersistent()` mit festem Pfad und einen Pfad-Ctor. Eine **öffentliche** Overload ergänzen (für Tests + spätere Flexibilität), ohne den bestehenden Ctor zu brechen:
```csharp
public static ReviewQueueService CreatePersistent(string persistencePath) => new(persistencePath);
```
(Falls der Pfad-Ctor `private` ist: `internal`/`public` so weit öffnen, dass die neue Factory ihn nutzen kann.)

- [ ] **Step 2: Failing test — Roundtrip inkl. SampleId**

`tests/AuswertungPro.Next.Infrastructure.Tests/ReviewQueuePersistenceTests.cs`:
```csharp
using System.IO;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ReviewQueuePersistenceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "rq-" + Guid.NewGuid().ToString("N") + ".json");
    public void Dispose() { try { if (File.Exists(_path)) File.Delete(_path); } catch { } }

    [Fact]
    public void Roundtrip_haelt_SelfTraining_Items_inkl_SampleId()
    {
        var a = ReviewQueueService.CreatePersistent(_path);
        a.EnqueueFromSelfTraining(
            caseId: "06.1-2", vsaCode: "BAB", suggestedCode: "",
            meter: 12.3, framePath: "f.png", matchLevel: "NoFindings",
            reason: "HumanReviewRequired", sampleId: "06.1-2_st_001_120000");

        var b = ReviewQueueService.CreatePersistent(_path); // neu laden aus Datei
        var items = b.GetAll();

        Assert.Single(items);
        Assert.Equal("06.1-2_st_001_120000", items[0].SelfTrainingSampleId);
        Assert.Equal("BAB", items[0].SelfTrainingVsaCode);
    }

    [Fact]
    public void Alte_Datei_ohne_SampleId_laedt_mit_null_SampleId()
    {
        // Minimal-JSON wie ein Altbestand (kein SampleId-Feld):
        File.WriteAllText(_path,
            "[{\"Id\":\"x\",\"Priority\":0.9,\"EnqueuedUtc\":\"2026-01-01T00:00:00Z\"," +
            "\"SelfTrainingCaseId\":\"06.1-2\",\"SelfTrainingVsaCode\":\"BAB\"," +
            "\"SelfTrainingMeter\":12.3,\"SelfTrainingFramePath\":\"f.png\",\"SelfTrainingMatchLevel\":\"Mismatch\"}]");

        var svc = ReviewQueueService.CreatePersistent(_path);
        var items = svc.GetAll();

        Assert.Single(items);
        Assert.Null(items[0].SelfTrainingSampleId);          // fehlt -> null
        Assert.Equal("06.1-2", items[0].SelfTrainingCaseId);  // Altbestand-Fallbackfelder bleiben nutzbar
    }
}
```
*(Hinweis: exakte JSON-Feldnamen aus `PersistedItem` im Code verifizieren und im Test angleichen.)*

- [ ] **Step 3: Test → rot** (`--filter "FullyQualifiedName~ReviewQueuePersistence"`). Erwartet: `EnqueueFromSelfTraining` kennt `sampleId` noch nicht / `SelfTrainingSampleId` fehlt.

- [ ] **Step 4: Implementieren**
  - `ReviewQueueItem`: Feld `string? SelfTrainingSampleId` ergänzen.
  - `EnqueueFromSelfTraining(...)`: optionalen Parameter `string? sampleId = null` ans Ende; im erzeugten Item setzen.
  - `PersistedItem` (JSON-DTO): `SelfTrainingSampleId` ergänzen (nullable). `LoadSelfTrainingItems` mappt es (null wenn fehlt — `System.Text.Json` lässt fehlende Felder auf null). `PersistSelfTrainingItems` schreibt es mit.

- [ ] **Step 5: Test → grün.**

- [ ] **Step 6: Zentrale Priorität (Vorgriff auf Task 2 nutzbar machen)**

In `EnqueueFromSelfTraining` den hartcodierten Prioritäts-Switch durch `SelfTrainingReviewRouting.Priority(level)` ersetzen — **erst nach Task 2** (oder Task 2 vorziehen). Falls Task 2 noch nicht da: diesen Step zurückstellen und in Task 2 erledigen.

- [ ] **Step 7: Commit**
```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/ReviewQueueService.cs tests/AuswertungPro.Next.Infrastructure.Tests/ReviewQueuePersistenceTests.cs
git commit -m "feat(review): SampleId im Queue-Item + Persistenz-Roundtrip (Altbestand-kompatibel)"
```

---

## Task 2: `SelfTrainingReviewRouting` — NoFindings routen + KI-Fehler-first-Priorität (Spec Fix 3)

**Problem:** NoFindings landen nie in der Queue (Guard `TrainingCenterViewModel.cs:~1980` überspringt bei reinen NoFindings; Filter `~1985` schließt sie aus). Priorität für NoFindings ist 0.3 (würde zuletzt sortiert — falsch). Regel wird zentral + testbar.

**Files:** Create `SelfTrainingReviewRouting.cs` + Test; Modify `ReviewQueueService.cs` (Priorität), `TrainingCenterViewModel.cs` (Guard+Filter).

- [ ] **Step 1: Failing test**
```csharp
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SelfTrainingReviewRoutingTests
{
    [Theory]
    [InlineData(MatchLevel.NoFindings, TrainingSampleStatus.New, true)]
    [InlineData(MatchLevel.Mismatch, TrainingSampleStatus.New, true)]
    [InlineData(MatchLevel.PartialMatch, TrainingSampleStatus.New, true)]
    [InlineData(MatchLevel.ExactMatch, TrainingSampleStatus.New, true)]
    [InlineData(MatchLevel.ExactMatch, TrainingSampleStatus.Approved, false)]
    [InlineData(MatchLevel.NoFindings, TrainingSampleStatus.Rejected, false)]
    [InlineData(MatchLevel.NoFindings, TrainingSampleStatus.Removed, false)]
    public void ShouldEnqueue(MatchLevel level, TrainingSampleStatus status, bool expected)
        => Assert.Equal(expected, SelfTrainingReviewRouting.ShouldEnqueue(level, status));

    [Theory]
    [InlineData(MatchLevel.NoFindings, 0.95)]
    [InlineData(MatchLevel.Mismatch, 0.90)]
    [InlineData(MatchLevel.PartialMatch, 0.60)]
    [InlineData(MatchLevel.ExactMatch, 0.30)]
    public void Priority(MatchLevel level, double expected)
        => Assert.Equal(expected, SelfTrainingReviewRouting.Priority(level), precision: 3);
}
```

- [ ] **Step 2: rot.**

- [ ] **Step 3: Implementieren** — `SelfTrainingReviewRouting.cs`:
```csharp
namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Zentrale, reine Regel: welcher Self-Training-Befund kommt in die Review-Queue und mit welcher
/// Prioritaet. KI-Fehler zuerst (NoFindings = uebersehener Schaden, Mismatch = falscher Code).
/// </summary>
public static class SelfTrainingReviewRouting
{
    public static bool ShouldEnqueue(MatchLevel level, TrainingSampleStatus status)
    {
        if (status is TrainingSampleStatus.Approved or TrainingSampleStatus.Rejected or TrainingSampleStatus.Removed)
            return false;
        return level is MatchLevel.NoFindings or MatchLevel.Mismatch
                     or MatchLevel.PartialMatch or MatchLevel.ExactMatch;
    }

    public static double Priority(MatchLevel level) => level switch
    {
        MatchLevel.NoFindings => 0.95,
        MatchLevel.Mismatch => 0.90,
        MatchLevel.PartialMatch => 0.60,
        _ => 0.30,
    };
}
```

- [ ] **Step 4: grün.**

- [ ] **Step 5: `ReviewQueueService.EnqueueFromSelfTraining`** nutzt `SelfTrainingReviewRouting.Priority` (String→Enum via `Enum.TryParse<MatchLevel>(matchLevel, true, out var ml) ? ml : MatchLevel.PartialMatch`). Alten Switch entfernen.

- [ ] **Step 6: ViewModel-Enqueue (`~Z.1979-2003`)** — Guard um `|| result.NoFindings > 0` erweitern; Kandidaten-Filter ersetzen durch:
```csharp
var candidates = newSamples.Where(s =>
    Enum.TryParse<MatchLevel>(s.MatchLevel, ignoreCase: true, out var lvl)
    && SelfTrainingReviewRouting.ShouldEnqueue(lvl, s.Status)).ToList();
```
`EnqueueFromSelfTraining(...)` zusätzlich mit `sampleId: s.SampleId` aufrufen (Task 1).

- [ ] **Step 7: `ReviewQueueTests.cs` erweitern** — NoFindings-Item hat `Priority >= 0.9` und sortiert vor PartialMatch.

- [ ] **Step 8: Build + Tests grün. Commit**
```bash
git add src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingReviewRouting.cs tests/AuswertungPro.Next.Pipeline.Tests/SelfTrainingReviewRoutingTests.cs src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/ReviewQueueService.cs src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs tests/AuswertungPro.Next.UI.Tests/ReviewQueueTests.cs
git commit -m "feat(review): NoFindings in die Review-Queue + KI-Fehler-first-Prioritaet"
```

---

## Task 3: `TrainingSampleMerge` — BBox-Felder beim Update erhalten

**Problem:** `TrainingSamplesStore.MergeOrUpdateAsync` (Z.85-137) überträgt beim In-Place-Update keine `Bbox*`-Felder → eine im Review gezogene Box ginge beim nächsten Status-Update verloren. Logik in eine reine Funktion ziehen.

**Files:** Create `TrainingSampleMerge.cs` + Test; Modify `TrainingSamplesStore.cs:85-137`.

- [ ] **Step 1: Failing test** (`TrainingSampleMergeTests.cs`):
```csharp
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingSampleMergeTests
{
    [Fact]
    public void ApplyUpdatableFields_uebernimmt_Status_KbState_und_BBox()
    {
        var t = new TrainingSample { SampleId = "x", Status = TrainingSampleStatus.New };
        var s = new TrainingSample { SampleId = "x", Status = TrainingSampleStatus.Approved,
            KbIndexState = KbIndexState.Pending, BboxXCenter = 0.5, BboxYCenter = 0.4, BboxWidth = 0.3, BboxHeight = 0.2 };
        TrainingSampleMerge.ApplyUpdatableFields(t, s);
        Assert.Equal(TrainingSampleStatus.Approved, t.Status);
        Assert.True(t.HasBbox);
        Assert.Equal(0.5, t.BboxXCenter);
    }

    [Fact]
    public void ApplyUpdatableFields_behaelt_bestehende_BBox_wenn_Source_keine_hat()
    {
        var t = new TrainingSample { SampleId = "x", BboxXCenter = 0.5, BboxYCenter = 0.5, BboxWidth = 0.2, BboxHeight = 0.2 };
        var s = new TrainingSample { SampleId = "x", Status = TrainingSampleStatus.Approved };
        TrainingSampleMerge.ApplyUpdatableFields(t, s);
        Assert.True(t.HasBbox);
    }
}
```

- [ ] **Step 2: rot. Step 3: Implementieren** (`TrainingSampleMerge.cs`): kopiert die bestehende Update-Feldliste aus `MergeOrUpdateAsync` (zuerst lesen!) + BBox nur wenn `source.HasBbox`:
```csharp
namespace AuswertungPro.Next.Application.Ai.Training;

public static class TrainingSampleMerge
{
    public static void ApplyUpdatableFields(TrainingSample target, TrainingSample source)
    {
        target.Status = source.Status;
        target.KbIndexState = source.KbIndexState;
        target.MatchLevel = source.MatchLevel;
        target.Notes = source.Notes;
        target.ExportedUtc = source.ExportedUtc;
        target.TrainingEligible = source.TrainingEligible;
        target.TrainingEligibilityReason = source.TrainingEligibilityReason;
        // ... alle weiteren Felder, die MergeOrUpdateAsync heute schon kopiert (vollstaendig abbilden!)
        if (source.HasBbox)
        {
            target.BboxXCenter = source.BboxXCenter;
            target.BboxYCenter = source.BboxYCenter;
            target.BboxWidth = source.BboxWidth;
            target.BboxHeight = source.BboxHeight;
        }
    }
}
```

- [ ] **Step 4: grün. Step 5:** `MergeOrUpdateAsync` ruft `TrainingSampleMerge.ApplyUpdatableFields(existing, incoming)`.
- [ ] **Step 6:** Voller Pipeline-/Infra-Testlauf (breite Nutzung → Regression). **Step 7: Commit** `fix(training): BBox-Felder beim Sample-Update erhalten (MergeOrUpdate)`.

---

## Task 4: `BoundingBox` — normierte Box validieren und setzen

**Files:** Create `BoundingBox.cs` + Test.

- [ ] **Step 1: Failing test** (`BoundingBoxTests.cs`): gültige Box akzeptiert; Breite 0 / negative Höhe / Center außerhalb 0-1 / Box ragt raus → abgelehnt; `ApplyTo` setzt alle vier Felder + `HasBbox`. *(Testkörper siehe Vorgänger-Plan; identisch.)*
- [ ] **Step 2: rot. Step 3: Implementieren** — `readonly record struct BoundingBox(double XCenter, double YCenter, double Width, double Height)` mit `static bool TryCreate(...)` (Größe > 0, Center in [0,1], Box komplett im Bild mit 1e-6-Toleranz) und `void ApplyTo(TrainingSample)`. *(Code identisch zum Vorgänger-Plan.)*
- [ ] **Step 4: grün. Step 5: Commit** `feat(training): normierte BoundingBox mit Validierung`.

---

## Task 5: `ProtocolReviewCandidateFilter` — Protokoll-Startdaten (Spec §6)

**Files:** Create `ProtocolReviewCandidateFilter.cs` + Test.

- [ ] **Step 1: Failing test** (`ProtocolReviewCandidateFilterTests.cs`): aus gemischten Samples bleiben nur `Status==New` mit katalog-gültigem, selektierbarem Code (Phantom `MWST` raus, `ObservedExtension` raus, `Approved` raus). *(In-Memory-`ICodeCatalogProvider`-Stub wie in `TrainingSampleEligibilityTests`; Testkörper siehe Vorgänger-Plan.)*
- [ ] **Step 2: rot. Step 3: Implementieren** — `static IEnumerable<TrainingSample> SelectCandidates(IEnumerable<TrainingSample>, ICodeCatalogProvider)`: `Status==New` UND `TrainingSampleEligibility.Evaluate(s, catalog).IsEligible`. *(Code identisch zum Vorgänger-Plan.)*
- [ ] **Step 4: grün. Step 5: Commit** `feat(review): Protokoll-Startdaten-Filter (nur katalog-gueltige New-Samples)`.

---

## Task 6: `ReviewApprovalService` — Approval/Reject/KB-Index aus dem ViewModel ziehen (§11)

**Problem:** `ApproveReviewItemAsync`/`RejectReviewItemAsync`/`ApplySelfTrainingReviewAsync`/`IncrementalKbUpdateAsync` liegen im VM und greifen direkt auf `TrainingSamplesStore` + bauen `KnowledgeBaseManager`. Das wandert in einen **testbaren** Infrastructure-Service. **Verhalten identisch** (inkl. der Phase-0-Härtung: Reject deindexiert + setzt `KbIndexState.None`; korrigierter Code erzeugt `_corr`-Sample). Lookup künftig per `SampleId` (Task 1), optionale Box (Task 4), Persistenz-Erhalt (Task 3).

**Files:** Create `IKnowledgeBaseIndexer.cs`, `ITrainingSampleStore.cs`, `IReviewApprovalService.cs` (Application); `ReviewApprovalService.cs`, `KnowledgeBaseIndexerAdapter.cs`, `TrainingSamplesStoreAdapter.cs` (Infrastructure); Test `ReviewApprovalServiceTests.cs`. Modify `TrainingCenterViewModel.cs` (delegieren).

- [ ] **Step 1: Abstraktionen definieren**
```csharp
// Application/Ai/Training/IKnowledgeBaseIndexer.cs
namespace AuswertungPro.Next.Application.Ai.Training;
public interface IKnowledgeBaseIndexer
{
    Task<bool> IndexAsync(TrainingSample sample, CancellationToken ct);
    void Deindex(string sampleId);
}

// Application/Ai/Training/ITrainingSampleStore.cs
public interface ITrainingSampleStore
{
    Task<List<TrainingSample>> LoadAsync();
    Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples);
    Task MergeAndSaveAsync(List<TrainingSample> samples);
}

// Application/Ai/Training/IReviewApprovalService.cs
public sealed record ReviewApplyResult(bool Indexed, bool Deindexed, string? CorrectedSampleId, string Reason);
public interface IReviewApprovalService
{
    Task<ReviewApplyResult> ApproveAsync(string sampleId, BoundingBox? box, CancellationToken ct);
    Task<ReviewApplyResult> RejectAsync(string sampleId, string? correctedCode, CancellationToken ct);
}
```

- [ ] **Step 2: Failing test mit Fakes** (`ReviewApprovalServiceTests.cs`, Infrastructure.Tests) — In-Memory-`ITrainingSampleStore` + Fake-`IKnowledgeBaseIndexer` (zählt Index/Deindex):
  - `ApproveAsync(id, box: gültig)` → Sample `Status=Approved`, `HasBbox==true`, Indexer.Index 1×, Result.Indexed.
  - `ApproveAsync(id, box: null)` → Approved, keine Box, Index 1×.
  - `RejectAsync(id, correctedCode: null)` → `Status=Rejected`, `KbIndexState=None`, Indexer.Deindex 1×.
  - `RejectAsync(id, correctedCode: "BAB")` → Original Rejected+deindexiert; neues `_corr`-Sample `Approved`, Index 1×, Result.CorrectedSampleId gesetzt.
  - Unbekannte `sampleId` → Result mit Reason „not found", keine Index/Deindex.

- [ ] **Step 3: rot.**

- [ ] **Step 4: Implementieren**
  - `ReviewApprovalService(ITrainingSampleStore store, IKnowledgeBaseIndexer indexer)` — Logik 1:1 aus `ApplySelfTrainingReviewAsync` portiert: Sample per `SampleId` aus `store.LoadAsync()` finden (kein CaseId/Meter-Lookup mehr — der Item führt jetzt die SampleId); Approve: `box?.ApplyTo(sample)`, `Status=Approved`, `KbIndexState=Pending`, `MatchLevel=ReviewApproved`, `store.MergeOrUpdateAsync`, `indexer.IndexAsync` → `KbIndexState=Indexed/Error`; Reject: `Status=Rejected`, `KbIndexState=None`, `indexer.Deindex`, bei `correctedCode` neues `_corr`-Sample (`Approved`, `MatchLevel=ReviewCorrected`, Signatur neu) → `MergeAndSaveAsync` + `indexer.IndexAsync`.
  - `TrainingSamplesStoreAdapter` (delegiert an die statischen `TrainingSamplesStore`-Methoden).
  - `KnowledgeBaseIndexerAdapter` — kapselt die heutige `IncrementalKbUpdateAsync`-Konstruktion (`KnowledgeBaseManager` + Eval-Hashes + `EmbeddingService`/Ollama-Config + Versions-Snapshot). Konfiguration wird hineingereicht (z.B. `Func<KnowledgeBaseManager>` aus dem Composition-Root / UI), damit Infrastructure UI-frei bleibt.

- [ ] **Step 5: grün.**

- [ ] **Step 6: ViewModel umstellen** — `ApproveReviewItemAsync`/`RejectReviewItemAsync` (und der ExactMatch-Auto-Index-Pfad) delegieren an `IReviewApprovalService`; die KB-Orchestrierung (`IncrementalKbUpdateAsync`) wandert in den `KnowledgeBaseIndexerAdapter`. Window-Code-Behind (`CreateFeedbackService`) baut Adapter im Composition-Root. **Verhalten unverändert** — bestehende Tests (Phase 0) bleiben grün.

- [ ] **Step 7: Voller Testlauf grün (Regression!). Commit**
```bash
git add src/AuswertungPro.Next.Application/Ai/Training/IKnowledgeBaseIndexer.cs src/AuswertungPro.Next.Application/Ai/Training/ITrainingSampleStore.cs src/AuswertungPro.Next.Application/Ai/Training/IReviewApprovalService.cs src/AuswertungPro.Next.Infrastructure/Ai/Training/ReviewApprovalService.cs src/AuswertungPro.Next.Infrastructure/Ai/Training/KnowledgeBaseIndexerAdapter.cs src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingSamplesStoreAdapter.cs tests/AuswertungPro.Next.Infrastructure.Tests/ReviewApprovalServiceTests.cs src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml.cs
git commit -m "refactor(review): Approval/Reject/KB-Index in ReviewApprovalService (Thin-UI, SampleId-Lookup)"
```

---

## Abschluss Plan 2a

- [ ] **Voller Testlauf:** `dotnet test AuswertungPro.sln` — alle grün, 0 Skips.
- [ ] **Final-Review** (superpowers:requesting-code-review) der 2a-Änderungen — Fokus: Verhalten der Approval-/Reject-Pfade unverändert, Persistenz-Roundtrip robust, keine UI-Abhängigkeit im Service.

**Danach:** Plan 2b (UI „Review & Freigabe") setzt auf `IReviewApprovalService`, `SelfTrainingReviewRouting`, `BoundingBox` und das `SampleId`-Queue-Item auf.

## Offene Punkte
- `KnowledgeBaseIndexerAdapter`-Konstruktion: genaue Form der Config-Übergabe (Func/Options) beim VM-Umbau (Task 6 Step 6) — im Spec-Review festklopfen.
- `ITrainingSampleStore` umschließt die statischen Store-Methoden; falls weitere Aufrufer den statischen Store direkt nutzen, bleiben die unverändert (kein globaler Umbau).
