# Gold-Fund Fundament (Plan A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Einen akzeptierten KI-Befund als menschlich bestaetigten "Gold-Fund" mit vollstaendigen Metadaten im bestehenden `TrainingSample` speichern (Player- UND Review-Queue-Pfad); abgelehnte Befunde als Negativbeispiel sichern statt zu loeschen (inkl. Frame-Snapshot); die QualityGate-Ampel beim Speichern persistieren (P5-Fix); robustes Snapshot-Ziehen. Kein Auto-Training.

**Architecture:** Der bestehende `TrainingSample` (sealed class) ist das EINZIGE Gold-Zuhause — keine neue Struktur. Zwei Bestaetigungs-Pfade: live im Player (`CodingEventToSampleMapper` + Confirm-Handler) und nachtraeglich in der Review-Queue (`ReviewApprovalService`); beide setzen die Gold-Felder. Drei Nebenpfade muessen mitgezogen werden, sonst stiller Datenverlust: die Re-Merge-Whitelist `TrainingSampleMerge.ApplyUpdatableFields`, die Export-Kopie `StageAExporter.CloneSample`, und die Ampel-Durchreichung (Ampel reist ueber `CodingEventAiContext.QualityGateLevel`). Neue Felder nullable/default — alte JSON-Samples laufen unveraendert.

**Tech Stack:** C#/.NET 10, xUnit, System.Text.Json, SQLite, WPF.

**Begriffsdefinition:** `HumanConfirmed` (bool?) = "Mensch hat den Befund als ECHT bestaetigt": `true` bei Accept/Edit, `false` bei Reject, `null` wenn nie menschlich beurteilt (Ignored/auto). `Corrected` (bool?) = "Mensch hat den KI-Code geaendert": `true` nur bei Edit+Accept, sonst `false`/`null`. `Status` (Approved/Rejected) sagt positiv/negativ. **Warum bool? statt bool:** ein spaeteres Teil-Update (z.B. nur KbIndexState) liefert keine Entscheidung → `null` → ueberschreibt einen gesetzten Gold-Fund NICHT.

**Test-Politik (CLAUDE.md):** Echtes TDD auf Logik (Mapping, Merge, Reject, QualityGate, Review-Service, CloneSample-Datenerhalt). Snapshot-IO per Build + manuellem Smoke.

---

## File Structure

- Modify: `src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs` — Gold-Felder (Task 1).
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingSampleMerge.cs` — Re-Merge-Whitelist (Task 1).
- Modify: `src/AuswertungPro.Next.Domain/Models/CodingSession.cs` — `CodingEventAiContext.QualityGateLevel` (Task 2).
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/CodingEventToSampleMapper.cs` — Mapping (Task 2).
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs` — Verdrahtung (Tasks 3, 5).
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs` — UpsertSample (Task 4).
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/StageAExporter.cs` — CloneSample (Task 6).
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/IReviewApprovalService.cs` + `src/AuswertungPro.Next.Infrastructure/Ai/Training/ReviewApprovalService.cs` — Review-Pfad (Task 7).
- Tests: `tests/AuswertungPro.Next.Pipeline.Tests/CodingEventToSampleMapperTests.cs`, `.../StageAExporterTests.cs`, `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingSampleMergeTests.cs`, neue `.../KnowledgeBaseQualityGateTests.cs`, `.../ReviewApprovalServiceTests.cs`.

Verifizierte Ist-Fakten (am echten Code geprueft): siehe Inline-Verweise je Task.

---

### Task 1: TrainingSample-Felder (bool?) + Re-Merge-Durchreichung

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingSampleMerge.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingSampleMergeTests.cs`

- [ ] **Step 1: Felder hinzufuegen**

In `TrainingSampleModels.cs`, in `public sealed class TrainingSample`, nach `public string? TrainingEligibilityReason { get; set; }` (Z.~102):

```csharp
    // ── Gold-Fund-Metadaten (nullable/default — alte JSON-Samples bleiben gueltig) ──
    /// <summary>true=Mensch bestaetigt (Accept/Edit), false=abgelehnt, null=nie menschlich beurteilt.</summary>
    public bool? HumanConfirmed { get; set; }
    /// <summary>true=Mensch hat den KI-Code korrigiert (Edit+Accept). null=nie beurteilt.</summary>
    public bool? Corrected { get; set; }
    /// <summary>Name des Bestaetigers (Bearbeiter). Null = unbekannt/alt.</summary>
    public string? ConfirmedByUser { get; set; }
    /// <summary>UTC-Zeitpunkt der Bestaetigung. Null = unbekannt/alt.</summary>
    public DateTime? ConfirmedAtUtc { get; set; }
    /// <summary>QualityGate-Ampel zum Bestaetigungszeitpunkt: "Green"/"Yellow"/"Red". Null = unbekannt.</summary>
    public string? QualityGateLevel { get; set; }
    /// <summary>Grund, falls der Snapshot beim Akzeptieren nicht gezogen werden konnte. Null = ok.</summary>
    public string? SnapshotError { get; set; }
```

- [ ] **Step 2: Failing Merge-Tests**

In `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingSampleMergeTests.cs` (Vorbild: vorhandene `ApplyUpdatableFields_*`-Tests):

```csharp
    [Fact]
    public void ApplyUpdatableFields_UebernimmtGoldFelder()
    {
        var target = new TrainingSample { SampleId = "s1", Code = "BCA" };
        var source = new TrainingSample
        {
            SampleId = "s1", Code = "BCA",
            HumanConfirmed = true, Corrected = true,
            ConfirmedByUser = "tester",
            ConfirmedAtUtc = new System.DateTime(2026, 6, 13, 9, 0, 0, System.DateTimeKind.Utc),
            QualityGateLevel = "Green"
        };

        TrainingSampleMerge.ApplyUpdatableFields(target, source);

        Assert.True(target.HumanConfirmed);
        Assert.True(target.Corrected);
        Assert.Equal("tester", target.ConfirmedByUser);
        Assert.Equal("Green", target.QualityGateLevel);
    }

    [Fact]
    public void ApplyUpdatableFields_EntwertetGesetztesGoldNichtBeiTeilUpdate()
    {
        // Re-Merge ohne Entscheidung/Bearbeiter (z.B. reines KbIndexState-Update)
        // darf einen gesetzten Gold-Fund NICHT auf null/leer zuruecksetzen.
        var target = new TrainingSample
        {
            SampleId = "s1", Code = "BCA",
            HumanConfirmed = true, Corrected = false,
            ConfirmedByUser = "tester", QualityGateLevel = "Green"
        };
        var source = new TrainingSample { SampleId = "s1", Code = "BCA" }; // HumanConfirmed/Corrected = null

        TrainingSampleMerge.ApplyUpdatableFields(target, source);

        Assert.True(target.HumanConfirmed);            // bleibt true (source war null)
        Assert.Equal("tester", target.ConfirmedByUser); // bleibt gesetzt
        Assert.Equal("Green", target.QualityGateLevel);
    }
```

- [ ] **Step 3: Test RED**

Run:
```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "ApplyUpdatableFields_UebernimmtGoldFelder|ApplyUpdatableFields_EntwertetGesetztesGoldNichtBeiTeilUpdate" -v minimal
```
Expected: FAIL (Felder fehlen / werden nicht uebernommen).

- [ ] **Step 4: ApplyUpdatableFields erweitern (HasValue-Guard)**

In `TrainingSampleMerge.cs` in `ApplyUpdatableFields`, vor der schliessenden Klammer (nach dem SAM-Maske-Block, Z.53):

```csharp
        // Gold-Fund-Metadaten: nur uebernehmen, wenn die Quelle wirklich eine Aussage macht
        // (bool? null = "keine Entscheidung" -> gesetzten Gold-Fund nicht entwerten).
        if (source.HumanConfirmed.HasValue) target.HumanConfirmed = source.HumanConfirmed;
        if (source.Corrected.HasValue) target.Corrected = source.Corrected;
        if (source.ConfirmedByUser is not null) target.ConfirmedByUser = source.ConfirmedByUser;
        if (source.ConfirmedAtUtc is not null) target.ConfirmedAtUtc = source.ConfirmedAtUtc;
        if (source.QualityGateLevel is not null) target.QualityGateLevel = source.QualityGateLevel;
        if (source.SnapshotError is not null) target.SnapshotError = source.SnapshotError;
```

- [ ] **Step 5: Test GRUEN + Build**

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "ApplyUpdatableFields_UebernimmtGoldFelder|ApplyUpdatableFields_EntwertetGesetztesGoldNichtBeiTeilUpdate" -v minimal
dotnet build AuswertungPro.sln -v minimal
```
Expected: 2 PASS, `0 Fehler`.

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingSampleMerge.cs tests/AuswertungPro.Next.Infrastructure.Tests/TrainingSampleMergeTests.cs
git commit -m "Gold-Fund: TrainingSample-Felder (bool?) + Re-Merge-Durchreichung mit HasValue-Schutz"
```

---

### Task 2: Ampel aufs Event + Accept/Edit/Reject-Mapping

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Models/CodingSession.cs`
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/CodingEventToSampleMapper.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/CodingEventToSampleMapperTests.cs`

- [ ] **Step 1: QualityGate-Feld auf den AiContext**

In `CodingSession.cs` in `public sealed class CodingEventAiContext` (Z.130) nach `Decision`:
```csharp
    /// <summary>QualityGate-Ampel ("Green"/"Yellow"/"Red") zum Bestaetigungszeitpunkt, vom UI gesetzt. Null = unbekannt.</summary>
    public string? QualityGateLevel { get; set; }
```

- [ ] **Step 2: Failing Mapping-Tests**

In `CodingEventToSampleMapperTests.cs`:
```csharp
    private static CodingEvent BuildEvent(CodingUserDecision decision, string? qg = null)
        => new()
        {
            Entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = "BCA", Beschreibung = "x" },
            MeterAtCapture = 12.3,
            VideoTimestamp = System.TimeSpan.FromSeconds(5),
            AiContext = new CodingEventAiContext { SuggestedCode = "BCA", Confidence = 0.8, Decision = decision, QualityGateLevel = qg }
        };

    [Fact]
    public void FromCodingEvent_Accept_SetztHumanConfirmedTrueOhneCorrected()
    {
        var s = CodingEventToSampleMapper.FromCodingEvent(
            BuildEvent(CodingUserDecision.Accepted, qg: "Green"), "H1", null, null,
            confirmedByUser: "tester",
            confirmedAtUtc: new System.DateTime(2026, 6, 13, 9, 0, 0, System.DateTimeKind.Utc));

        Assert.Equal(true, s.HumanConfirmed);
        Assert.Equal(false, s.Corrected);
        Assert.Equal("tester", s.ConfirmedByUser);
        Assert.Equal("Green", s.QualityGateLevel);
    }

    [Fact]
    public void FromCodingEvent_Edit_SetztCorrectedTrue()
    {
        var s = CodingEventToSampleMapper.FromCodingEvent(BuildEvent(CodingUserDecision.AcceptedWithEdit), "H1", null, null);
        Assert.Equal(true, s.HumanConfirmed);
        Assert.Equal(true, s.Corrected);
    }

    [Fact]
    public void FromCodingEvent_Reject_HumanConfirmedFalse_StatusRejected()
    {
        var s = CodingEventToSampleMapper.FromCodingEvent(BuildEvent(CodingUserDecision.Rejected), "H1", null, null);
        Assert.Equal(false, s.HumanConfirmed);
        Assert.Equal(TrainingSampleStatus.Rejected, s.Status);
    }

    [Fact]
    public void FromCodingEvent_OhneAiContext_HumanConfirmedNull()
    {
        var ev = new CodingEvent
        {
            Entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = "BCA" },
            MeterAtCapture = 1, VideoTimestamp = System.TimeSpan.Zero
        };
        var s = CodingEventToSampleMapper.FromCodingEvent(ev, "H1", null, null);
        Assert.Null(s.HumanConfirmed);
        Assert.Null(s.Corrected);
    }
```

- [ ] **Step 3: Test RED**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests --filter FromCodingEvent_Accept_SetztHumanConfirmedTrueOhneCorrected -v minimal
```
Expected: Compile-Fehler.

- [ ] **Step 4: Mapper erweitern**

In `CodingEventToSampleMapper.cs` `FromCodingEvent`-Signatur:
```csharp
    public static TrainingSample FromCodingEvent(
        CodingEvent ev,
        string caseId,
        string? framePath,
        DateTime? inspectionDate = null,
        string? confirmedByUser = null,
        DateTime? confirmedAtUtc = null)
```
Im `new TrainingSample { ... }`-Initializer nach `BboxHeight = ...`:
```csharp
            HumanConfirmed = ev.AiContext?.Decision switch
            {
                CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit => true,
                CodingUserDecision.Rejected => false,
                _ => (bool?)null
            },
            Corrected = ev.AiContext?.Decision switch
            {
                CodingUserDecision.AcceptedWithEdit => true,
                CodingUserDecision.Accepted or CodingUserDecision.Rejected => false,
                _ => (bool?)null
            },
            ConfirmedByUser = confirmedByUser,
            ConfirmedAtUtc = confirmedAtUtc,
            QualityGateLevel = ev.AiContext?.QualityGateLevel
```

- [ ] **Step 5: Test GRUEN**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests --filter "FromCodingEvent_Accept_SetztHumanConfirmedTrueOhneCorrected|FromCodingEvent_Edit_SetztCorrectedTrue|FromCodingEvent_Reject_HumanConfirmedFalse_StatusRejected|FromCodingEvent_OhneAiContext_HumanConfirmedNull" -v minimal
```
Expected: 4 PASS.

- [ ] **Step 6: Build + Commit**

```powershell
dotnet build AuswertungPro.sln -v minimal
git add src/AuswertungPro.Next.Domain/Models/CodingSession.cs src/AuswertungPro.Next.Application/Ai/Training/CodingEventToSampleMapper.cs tests/AuswertungPro.Next.Pipeline.Tests/CodingEventToSampleMapperTests.cs
git commit -m "Gold-Fund: Accept/Edit/Reject auf bool? HumanConfirmed/Corrected mappen, Ampel ueber AiContext"
```

---

### Task 3: Player-Verdrahtung — Bearbeiter/Ampel + Reject als Negativ

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`

(Logik in Task 2 getestet; hier UI-Verdrahtung. Build + Smoke. Der eigentliche Persist-Umbau auf `async Task` kommt in Task 5 — hier zunaechst nur Aufruf-Inhalte; Task 5 stellt die Methode auf `Task` um und die Aufrufe auf `SafeFireAndForget`.)

- [ ] **Step 1: Bearbeiter in beide Persist-Pfade**

In `PersistSingleEventAsTrainingSample` (Z.~614) den `FromCodingEvent`-Aufruf:
```csharp
            var sample = CodingEventToSampleMapper.FromCodingEvent(
                ev, caseId, framePath, ResolveTrainingInspectionDate(),
                confirmedByUser: System.Environment.UserName,
                confirmedAtUtc: System.DateTime.UtcNow);
```
In `PersistCodingEventsAsTrainingSamples` (Z.~640) den `FromCodingEvent`-Aufruf identisch.

- [ ] **Step 2: Beim Akzeptieren die Ampel aufs Event**

In `ConfirmAccept_Click` (Z.4097) VOR `PersistSingleEventAsTrainingSample(...)`:
```csharp
            if (_codingPendingGateResult != null)
                _codingPendingConfirmEvent.AiContext.QualityGateLevel =
                    _codingPendingGateResult.TrafficLight.ToString();
```

- [ ] **Step 3: ConfirmReject_Click — Negativ sichern (inkl. Snapshot) statt nur loeschen**

`ConfirmReject_Click` (Z.4123) ersetzen:
```csharp
    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        if (_codingPendingConfirmEvent != null)
        {
            _codingPendingConfirmEvent.AiContext!.Decision = CodingUserDecision.Rejected;
            if (_codingPendingGateResult != null)
                _codingPendingConfirmEvent.AiContext.QualityGateLevel =
                    _codingPendingGateResult.TrafficLight.ToString();

            // Gold-Fund: abgelehnten Befund als Negativbeispiel (Status=Rejected, inkl. Snapshot
            // aus Task 5) sichern, BEVOR er aus der Session entfernt wird.
            _codingPendingConfirmEvent.PersistAsNegativeAfterReject = true;
            PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent)
                .SafeFireAndForget("TrainingSaveReject");

            _codingSessionService?.RemoveEvent(_codingPendingConfirmEvent.EventId);
            _codingVm?.Events.Remove(_codingPendingConfirmEvent);
            RefreshCodingEventsList();
        }

        CloseConfirmationAndResume();
    }
```
(Hinweis: das `.SafeFireAndForget(...)` setzt voraus, dass Task 5 die Methode auf `async Task` umgestellt hat. Wird Task 3 vor Task 5 commitet, kompiliert dieser Aufruf noch nicht — daher Task 5 unmittelbar danach ausfuehren ODER Task 3 Step 3 erst zusammen mit Task 5 anwenden. Empfehlung: Task 3 und Task 5 als ein Subagent-Lauf. Das `PersistAsNegativeAfterReject`-Flag ist nur Doku-Marker; entfernen, falls nicht gebraucht — der Snapshot laeuft in Task 5 fuer JEDEN Persist-Aufruf, also auch fuer Reject.)

- [ ] **Step 4: Build + Commit** (gemeinsam mit Task 5, siehe dort)

---

### Task 4: QualityGate-Ampel in der KnowledgeBase persistieren (P5-Fix)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs`
- Test (neu): `tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseQualityGateTests.cs`

- [ ] **Step 1: Failing Test (konkret, mit Fake-Embedder + Temp-DB)**

Neue Datei `tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseQualityGateTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class KnowledgeBaseQualityGateTests : IDisposable
{
    private readonly ICodeCatalogProvider? _previousCatalog;

    public KnowledgeBaseQualityGateTests()
    {
        _previousCatalog = VsaCodeResolver.CurrentCatalog;
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previousCatalog);

    [Fact]
    public async Task UpsertSample_SchreibtQualityGateLevel()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-qg", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder());

            var sample = new TrainingSample
            {
                SampleId = "qg1", CaseId = "H-01", Code = "BAB",
                Beschreibung = "Laengsriss", MeterStart = 5.0, MeterEnd = 5.0,
                InspectionDate = new DateTime(2024, 6, 1), TrainingEligible = true,
                QualityGateLevel = "Green"
            };
            Assert.True(KnowledgeBaseManager.IsIndexWorthy(sample));
            Assert.True(await mgr.IndexSampleAsync(sample, CancellationToken.None));

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT QualityGateLevel FROM Samples WHERE SampleId = 'qg1'";
            var stored = cmd.ExecuteScalar() as string;

            Assert.Equal("Green", stored);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static EmbeddingService FakeEmbedder()
        => new(new HttpClient(new FixedEmbeddingHandler()),
            new OllamaConfig(new Uri("http://localhost:11434"), "v", "t", "nomic-embed-text", TimeSpan.FromSeconds(5)));

    private sealed class FixedEmbeddingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"embeddings":[[0.1,0.2,0.3,0.4]]}""", Encoding.UTF8, "application/json")
            });
    }

    private sealed class MinimalCatalog : ICodeCatalogProvider
    {
        private static readonly CodeDefinition[] Codes = { new() { Code = "BAB", Title = "Risse", IsSelectable = true } };
        public IReadOnlyList<CodeDefinition> GetAll() => Codes;
        public bool TryGet(string code, out CodeDefinition def)
        {
            def = Codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)) ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }
        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new InvalidOperationException();
        public IReadOnlyList<string> AllowedCodes() => Codes.Select(c => c.Code).ToList();
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => Array.Empty<string>();
    }
}
```
(Muster 1:1 aus `KnowledgeBaseDeindexTests`. Falls `IsIndexWorthy`/`IsIndexed`-Signaturen leicht abweichen, an der Vorlage-Datei verifizieren.)

- [ ] **Step 2: Test RED**

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter UpsertSample_SchreibtQualityGateLevel -v minimal
```
Expected: FAIL (gelesen: leer/null).

- [ ] **Step 3: UpsertSample erweitern**

In `KnowledgeBaseManager.cs` `UpsertSample` (Z.322) INSERT-Spalten + Wert ergaenzen:
```csharp
        ExecuteNonQuery("""
            INSERT OR REPLACE INTO Samples
                (SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                 IsStreck, FramePath, ExportedUtc, VersionId, SourceType, QualityGateLevel)
            VALUES ($id, $caseId, $code, $desc, $ms, $me, $streck, $frame, $exp, $ver, $source, $qg)
            """,
            ("$id",     s.SampleId),
            ("$caseId", s.CaseId),
            ("$code",   s.Code),
            ("$desc",   s.Beschreibung),
            ("$ms",     s.MeterStart),
            ("$me",     s.MeterEnd),
            ("$streck", s.IsStreckenschaden ? 1 : 0),
            ("$frame",  s.FramePath),
            ("$exp",    s.ExportedUtc?.ToString("O") ?? DateTime.UtcNow.ToString("O")),
            ("$ver",    versionId),
            ("$source", s.SourceType ?? ""),
            ("$qg",     s.QualityGateLevel ?? ""));
```

- [ ] **Step 4: Test GRUEN + Build**

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter UpsertSample_SchreibtQualityGateLevel -v minimal
dotnet build AuswertungPro.sln -v minimal
```
Expected: PASS, `0 Fehler`.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseQualityGateTests.cs
git commit -m "Gold-Fund: QualityGate-Ampel beim KB-Upsert persistieren (P5-Fix)"
```

---

### Task 5: Snapshot beim Speichern robust (async Task, fuer Accept UND Reject)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`

(UI-IO; Build + Smoke. Der Snapshot laeuft in `PersistSingleEventAsTrainingSample` und damit fuer JEDEN Aufruf — Accept, Edit-Batch UND Reject. Negativbeispiele bekommen also ebenfalls einen Frame.)

- [ ] **Step 1: Snapshot-Helfer**

Nahe `PersistSingleEventAsTrainingSample`:
```csharp
    private async System.Threading.Tasks.Task<(string? path, string? error)> TrySaveGoldFrameAsync(CodingEvent ev)
    {
        try
        {
            var bytes = _detectionPendingFrameBytes;
            if (bytes == null || bytes.Length == 0)
                bytes = await CaptureCurrentFrameAsync();
            if (bytes == null || bytes.Length == 0)
                return (null, "kein Frame verfuegbar");

            var dir = System.IO.Path.Combine(
                Infrastructure.Ai.KnowledgeBase.KnowledgeBasePaths.Root, "gold_frames");
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, $"{ev.EventId:N}.png");
            await System.IO.File.WriteAllBytesAsync(file, bytes);
            return (file, null);
        }
        catch (System.Exception ex)
        {
            return (null, ex.Message);
        }
    }
```
(Implementierer verifiziert den exakten `KnowledgeBasePaths`-Zugriff einmal; Startpunkt `KnowledgeBasePaths.Root`.)

- [ ] **Step 2: PersistSingleEventAsTrainingSample auf `async Task` (KEIN async void)**

Methode ersetzen:
```csharp
    private async System.Threading.Tasks.Task PersistSingleEventAsTrainingSample(CodingEvent ev)
    {
        if (ev.Entry == null || string.IsNullOrWhiteSpace(ev.Entry.Code)) return;
        try
        {
            var caseId = _codingVm?.HaltungName ?? "unknown";
            var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;

            string? snapshotError = null;
            if (string.IsNullOrWhiteSpace(framePath))
            {
                var (snapPath, snapErr) = await TrySaveGoldFrameAsync(ev);
                framePath = snapPath;          // null bei Fehler -> Speichern laeuft trotzdem
                snapshotError = snapErr;
            }

            var sample = CodingEventToSampleMapper.FromCodingEvent(
                ev, caseId, framePath, ResolveTrainingInspectionDate(),
                confirmedByUser: System.Environment.UserName,
                confirmedAtUtc: System.DateTime.UtcNow);
            sample.SnapshotError = snapshotError;

            if (ev.Entry.FotoPaths.Count > 1)
            {
                sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                    sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
            }
            await InfraTraining.TrainingSamplesStore.MergeAndSaveAsync(new List<TrainingSample> { sample });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Training] Einzelspeicherung Fehler: {ex.Message}");
        }
    }
```

- [ ] **Step 3: Alle Aufrufer auf `.SafeFireAndForget(...)` umstellen**

Es gibt drei Aufrufer von `PersistSingleEventAsTrainingSample`. Jeden auf Fire-and-Forget mit sichtbarer Fehlerbehandlung umstellen:
- `ConfirmAccept_Click` (Z.4102): `PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent).SafeFireAndForget("TrainingSaveAccept");`
- `ConfirmReject_Click` (Task 3 Step 3): bereits `.SafeFireAndForget("TrainingSaveReject");`
- Der dritte Aufrufer (Z.~2871): `... .SafeFireAndForget("TrainingSaveSingle");`

(`SafeFireAndForget` ist projektweit etabliert und meldet Faulted-Tasks — anders als `async void`, das Exceptions verschluckt.)

- [ ] **Step 4: Build**

```powershell
dotnet build AuswertungPro.sln -v minimal
```
Expected: `0 Fehler` (auch der Reject-Aufruf aus Task 3 kompiliert jetzt, da die Methode `Task` zurueckgibt).

- [ ] **Step 5: Smoke + Commit (Task 3 + Task 5 gemeinsam)**

Smoke: (a) Accept ohne Foto -> `gold_frames\<id>.png` da, `FramePath` gesetzt, `SnapshotError` null. (b) Reject -> Negativ-Sample (Status=Rejected) mit eigenem Snapshot entsteht. (c) Accept am Videoende (kein Frame) -> laeuft durch, `FramePath` null, `SnapshotError` gesetzt.
```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs
git commit -m "Gold-Fund: Reject als Negativbeispiel + robuster Snapshot (async Task statt async void)"
```

---

### Task 6: Export-Kopie (CloneSample) vollstaendig + getestet

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/StageAExporter.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/StageAExporterTests.cs`

- [ ] **Step 1: CloneSample testbar machen (private -> internal)**

In `StageAExporter.cs` die Signatur (Z.552) aendern:
```csharp
    internal static TrainingSample CloneSample(TrainingSample source)
```
(Application hat `[assembly: InternalsVisibleTo("AuswertungPro.Next.Pipeline.Tests")]` — damit direkt testbar.)

- [ ] **Step 2: Failing Test**

In `tests/AuswertungPro.Next.Pipeline.Tests/StageAExporterTests.cs`:
```csharp
    [Fact]
    public void CloneSample_KopiertSamMaskeUndGoldFelder()
    {
        var source = new TrainingSample
        {
            SampleId = "s1", Code = "BCA",
            SamMaskRle = "1,2,3", SamMaskImageWidth = 640, SamMaskImageHeight = 480,
            SamMaskAreaPixels = 100, SamMaskConfidence = 0.9, SamMaskLabel = "crack",
            KbCheck = "KbAgreement",
            HumanConfirmed = true, Corrected = false,
            ConfirmedByUser = "tester", QualityGateLevel = "Green"
        };

        var clone = StageAExporter.CloneSample(source);

        Assert.Equal("1,2,3", clone.SamMaskRle);
        Assert.Equal(640, clone.SamMaskImageWidth);
        Assert.Equal("crack", clone.SamMaskLabel);
        Assert.Equal("KbAgreement", clone.KbCheck);
        Assert.Equal(true, clone.HumanConfirmed);
        Assert.Equal("tester", clone.ConfirmedByUser);
        Assert.Equal("Green", clone.QualityGateLevel);
    }
```

- [ ] **Step 3: Test RED**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests --filter CloneSample_KopiertSamMaskeUndGoldFelder -v minimal
```
Expected: FAIL (SamMaske/KbCheck/Gold-Felder werden nicht kopiert).

- [ ] **Step 4: CloneSample erweitern**

Im Objekt-Initializer nach `BboxHeight = source.BboxHeight,`:
```csharp
            // Bisher fehlende Felder (latenter Bug) + Gold-Fund-Metadaten
            KbCheck = source.KbCheck,
            SamMaskRle = source.SamMaskRle,
            SamMaskImageWidth = source.SamMaskImageWidth,
            SamMaskImageHeight = source.SamMaskImageHeight,
            SamMaskAreaPixels = source.SamMaskAreaPixels,
            SamMaskConfidence = source.SamMaskConfidence,
            SamMaskLabel = source.SamMaskLabel,
            HumanConfirmed = source.HumanConfirmed,
            Corrected = source.Corrected,
            ConfirmedByUser = source.ConfirmedByUser,
            ConfirmedAtUtc = source.ConfirmedAtUtc,
            QualityGateLevel = source.QualityGateLevel,
            SnapshotError = source.SnapshotError,
```

- [ ] **Step 5: Test GRUEN + Build + Commit**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests --filter CloneSample_KopiertSamMaskeUndGoldFelder -v minimal
dotnet build AuswertungPro.sln -v minimal
git add src/AuswertungPro.Next.Application/Ai/Training/StageAExporter.cs tests/AuswertungPro.Next.Pipeline.Tests/StageAExporterTests.cs
git commit -m "Gold-Fund: CloneSample kopiert SAM-Maske/KbCheck/Gold-Felder (Datenverlust-Fix + Test)"
```

---

### Task 7: Review-Queue-Pfad (ReviewApprovalService) auf Gold-Fund — Bearbeiter PFLICHT

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/IReviewApprovalService.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/ReviewApprovalService.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/ReviewApprovalServiceTests.cs`

- [ ] **Step 1: Failing Tests (Approve + Reject setzen Bearbeiter)**

In `ReviewApprovalServiceTests.cs` (Setup wie die 5 vorhandenen Tests dieser Datei — Store-Fake + Indexer-Fake, ein Sample vorladen):
```csharp
    [Fact]
    public async Task ApproveSelfTraining_SetztGoldFelderMitBearbeiter()
    {
        var svc = BuildService(out var store, withSample: "s1");   // vorhandenes Test-Setup-Muster
        await svc.ApproveSelfTrainingAsync("s1", box: null, ct: default, confirmedByUser: "tester");

        var saved = (await store.LoadAsync()).First(s => s.SampleId == "s1");
        Assert.Equal(true, saved.HumanConfirmed);
        Assert.Equal(false, saved.Corrected);
        Assert.Equal("tester", saved.ConfirmedByUser);
        Assert.NotNull(saved.ConfirmedAtUtc);
        Assert.Equal(TrainingSampleStatus.Approved, saved.Status);
    }

    [Fact]
    public async Task RejectSelfTraining_SetztBearbeiterUndNegativ()
    {
        var svc = BuildService(out var store, withSample: "s1");
        await svc.RejectSelfTrainingAsync("s1", correctedCode: null, ct: default, confirmedByUser: "tester");

        var saved = (await store.LoadAsync()).First(s => s.SampleId == "s1");
        Assert.Equal(TrainingSampleStatus.Rejected, saved.Status);
        Assert.Equal(false, saved.HumanConfirmed);
        Assert.Equal("tester", saved.ConfirmedByUser);
        Assert.NotNull(saved.ConfirmedAtUtc);
    }
```

- [ ] **Step 2: Test RED**

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "ApproveSelfTraining_SetztGoldFelderMitBearbeiter|RejectSelfTraining_SetztBearbeiterUndNegativ" -v minimal
```
Expected: Compile-Fehler (Parameter `confirmedByUser` fehlt).

- [ ] **Step 3: Interface — confirmedByUser als PFLICHT-Parameter**

In `IReviewApprovalService.cs` beide Signaturen erweitern (Pflicht, KEIN Default — bewusst, damit jeder Aufrufer den Bearbeiter liefern MUSS):
```csharp
    Task<ReviewApplyResult> ApproveSelfTrainingAsync(
        string sampleId, BoundingBox? box, CancellationToken ct,
        string confirmedByUser, TrainingSegmentationMask? mask = null);

    Task<ReviewApplyResult> RejectSelfTrainingAsync(
        string sampleId, string? correctedCode, CancellationToken ct,
        string confirmedByUser, string? correctedDescription = null);
```
(`confirmedByUser` VOR den optionalen Parametern platzieren, sonst Compile-Fehler.)

- [ ] **Step 4: ReviewApprovalService — Signaturen + Gold-Felder**

`ApproveSelfTrainingAsync`-Signatur identisch anpassen. Nach `match.MatchLevel = MatchLevelNames.ReviewApproved;` (Z.50):
```csharp
        match.HumanConfirmed = true;
        match.Corrected = false;
        match.ConfirmedByUser = confirmedByUser;
        match.ConfirmedAtUtc = DateTime.UtcNow;
```
`RejectSelfTrainingAsync`-Signatur identisch anpassen. Nach `match.KbIndexState = KbIndexState.None;` (Z.86):
```csharp
        match.HumanConfirmed = false;   // abgelehnt = nicht bestaetigt
        match.Corrected = false;
        match.ConfirmedByUser = confirmedByUser;
        match.ConfirmedAtUtc = DateTime.UtcNow;
```
Im `corrected`-Initializer (Z.99-129) nach `Notes = ...` (korrigiertes Sample = bestaetigter, korrigierter Gold-Fund; Box/Maske duerfen nicht verloren gehen):
```csharp
                HumanConfirmed = true,
                Corrected = true,
                ConfirmedByUser = confirmedByUser,
                ConfirmedAtUtc = DateTime.UtcNow,
                BboxXCenter = match.BboxXCenter,
                BboxYCenter = match.BboxYCenter,
                BboxWidth = match.BboxWidth,
                BboxHeight = match.BboxHeight,
                SamMaskRle = match.SamMaskRle,
                SamMaskImageWidth = match.SamMaskImageWidth,
                SamMaskImageHeight = match.SamMaskImageHeight,
                SamMaskAreaPixels = match.SamMaskAreaPixels,
                SamMaskConfidence = match.SamMaskConfidence,
                SamMaskLabel = match.SamMaskLabel,
```

- [ ] **Step 5: Alle bestehenden Aufrufer anpassen (Build zeigt sie)**

Da `confirmedByUser` Pflicht ist, bricht der Build an allen Aufrufstellen. Alle anpassen:
- Die bestehenden 5 Aufrufe in `ReviewApprovalServiceTests.cs`: `confirmedByUser: "test"` ergaenzen.
- Den/die Produktiv-Aufrufer (TrainingCenter-ViewModel): `confirmedByUser: System.Environment.UserName` ergaenzen.
```powershell
dotnet build AuswertungPro.sln -v minimal
```
bis `0 Fehler` (zeigt jede fehlende Aufrufstelle).

- [ ] **Step 6: Tests GRUEN (neu + bestehende)**

```powershell
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter "ApproveSelfTraining_SetztGoldFelderMitBearbeiter|RejectSelfTraining_SetztBearbeiterUndNegativ" -v minimal
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests -v minimal
dotnet test tests/AuswertungPro.Next.Pipeline.Tests -v minimal
```
Expected: neue PASS, bestehende Review-Tests gruen.

- [ ] **Step 7: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Ai/Training/IReviewApprovalService.cs src/AuswertungPro.Next.Infrastructure/Ai/Training/ReviewApprovalService.cs tests/AuswertungPro.Next.Infrastructure.Tests/ReviewApprovalServiceTests.cs src/AuswertungPro.Next.UI
git commit -m "Gold-Fund: Review-Queue-Pfad setzt Gold-Felder mit Pflicht-Bearbeiter, corrected-Sample behaelt Box/Maske"
```

---

## Self-Review

**Spec-Abdeckung (gegen User-Semantik + die 4 Korrekturen):**
- `Accept=true/false-Corrected`, `Edit=true/true`, `Reject=false`, `Ignored=null` → Task 2 (`bool?`-switch) + Task 7 + Tests.
- `Reject = Negativsample` → Task 3 (Player, Persist vor RemoveEvent) + Task 7 (Review) + Tests.
- **Korrektur 1** (Re-Merge entwertet Gold nicht): `bool?` + HasValue-Guard in ApplyUpdatableFields → Task 1 + expliziter Test `EntwertetGesetztesGoldNichtBeiTeilUpdate`.
- **Korrektur 2** (kein async void): Task 5 — `async Task` + `SafeFireAndForget` an allen 3 Aufrufern; Snapshot laeuft fuer Accept UND Reject (Negativbeispiele bekommen Frame).
- **Korrektur 3** (Review-Reject braucht Bearbeiter): Task 7 — `confirmedByUser` ist PFLICHT-Parameter bei Approve UND Reject (kein Default).
- **Korrektur 4** (konkrete Tests): Task 4 voll ausformulierter KB-Test (Fake-Embedder + Temp-DB, kein Platzhalter); Task 6 CloneSample-Test (private→internal).
- `Bearbeiter + UTC` → Tasks 3/5 (Environment.UserName) + Task 7 (Pflicht-Parameter).
- `QualityGate-P5` → Task 2 (AiContext-Feld) + Task 3 (aufs Event) + Task 4 (UpsertSample) + Test.
- Re-Merge-/Export-Datenverlust → Task 1 + Task 6 (inkl. SAM-Maske-Altbug).
- `Quantifizierung` + `Auto-Training` → bewusst NICHT (Plan B / kein Trigger).

**Typ-Konsistenz:** `HumanConfirmed`/`Corrected` durchgaengig `bool?`; `QualityGateLevel` durchgaengig string "Green"/"Yellow"/"Red" (`TrafficLight.ToString()`); `FromCodingEvent` waechst additiv mit optionalen Parametern; `ApproveSelfTrainingAsync`/`RejectSelfTrainingAsync` bekommen `confirmedByUser` als Pflicht (alle Aufrufer in Task 7 Step 5 angepasst).

**Ausfuehrungs-Hinweis:** Task 3 und Task 5 zusammen ausfuehren (Task 3 Step 3 nutzt `SafeFireAndForget`, das erst nach Task 5s `async Task`-Umstellung kompiliert).

**Offene Vereinfachungen (bewusst):** `ConfirmedAtUtc` = Persist-/Review-Zeit. `QuantificationSource` + Kalibrierungs-Wahrheit = Plan B.
