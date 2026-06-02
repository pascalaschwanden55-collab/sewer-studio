# Trainingsmodule Phase 1 — Engine-Sicherheit — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die KB-Lern-Pfade sicher machen, bevor irgendetwas gelernt wird — Eval-Schutz überall, Eligibility am KB-Index, wirksame Korrektur (Deindex + Status `Removed`), keine ungeprüfte Massen-Indexierung, keine Dummy-Box-YOLO-Labels.

**Architecture:** Reine Härtung der bestehenden Engine (KnowledgeBaseManager, TrainingSamples-Modell, Self-Training-/TrainingCenter-Schreibpfade). Keine neue UI. Pure, testbare Logik zuerst; Wiring danach. Thin-AI/Layer-Disziplin bleibt.

**Tech Stack:** C# / .NET 8, xUnit, System.Text.Json, SQLite (Microsoft.Data.Sqlite). Build: `dotnet build AuswertungPro.sln`. Test: `dotnet test`.

**Spec:** `docs/superpowers/specs/2026-06-02-trainingsmodule-redesign-design.md` (§9 Fixes 1–6, Reihenfolge Phase 1).

---

## Dateien-Überblick

| Datei | Verantwortung | Aktion |
|---|---|---|
| `src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs` | `TrainingSampleStatus`-Enum | Wert `Removed` ergänzen |
| `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs` | `IsIndexWorthy`, `DeindexSample` | Eligibility-Gate ergänzen |
| `src/AuswertungPro.Next.Infrastructure/Ai/CodingSessionService.cs` | KB-Index nach Codierung | Eval-Hashes durchreichen |
| `src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml.cs` | Review-Approve/FeedbackIngestion-KB-Index | Eval-Hashes durchreichen |
| `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs` | Reject/Remove, Batch-Import, YOLO-Export | Deindex + `Removed`; Batch entschärfen; YOLO-Gate |
| `src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs` | YOLO-Labels | Samples ohne echte Box blockieren |
| `tests/AuswertungPro.Next.Infrastructure.Tests/…` | Tests | erweitern/ergänzen |

**Hinweis vorab (Migration):** `TrainingSampleStatus` wird als Zahl serialisiert (System.Text.Json Default). `New=0, Approved=1, Rejected=2`. `Removed` MUSS als **letzter** Wert `=3` angehängt werden, damit bestehende `training_samples.json` unverändert lesbar bleiben.

---

### Task 1: Neuer Status `TrainingSampleStatus.Removed`

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs:10`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleStatusTests.cs` (neu)

- [ ] **Step 1: Failing-Test schreiben** — sichert die Enum-Werte (Migrations-Sicherheit).

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleStatusTests.cs
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class TrainingSampleStatusTests
{
    [Fact]
    public void Removed_IsAppendedAsValue3_ExistingValuesUnchanged()
    {
        Assert.Equal(0, (int)TrainingSampleStatus.New);
        Assert.Equal(1, (int)TrainingSampleStatus.Approved);
        Assert.Equal(2, (int)TrainingSampleStatus.Rejected);
        Assert.Equal(3, (int)TrainingSampleStatus.Removed);
    }
}
```

- [ ] **Step 2: Test laufen lassen — muss fehlschlagen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter "FullyQualifiedName~TrainingSampleStatusTests"`
Expected: FAIL — `TrainingSampleStatus` enthält kein `Removed`.

- [ ] **Step 3: Enum erweitern**

```csharp
// TrainingSampleModels.cs:10
public enum TrainingSampleStatus { New, Approved, Rejected, Removed }
```

- [ ] **Step 4: Test laufen lassen — muss bestehen**

Run: gleicher Befehl wie Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleStatusTests.cs
git commit -m "feat(training): TrainingSampleStatus.Removed einfuehren"
```

---

### Task 2: `IsIndexWorthy` prüft Trainings-Eligibility (kein untaugliches Sample in die KB)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs:365-380`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseManagerEligibilityTests.cs` (neu)
- Test (anpassen): bestehende KB-Index-Tests, die Samples OHNE `InspectionDate` indexieren

- [ ] **Step 1: Failing-Test schreiben** — ein an sich indexwürdiges Sample ohne Inspektionsdatum darf NICHT indexwürdig sein.

```csharp
// tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseManagerEligibilityTests.cs
using System;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public class KnowledgeBaseManagerEligibilityTests
{
    private static TrainingSample BaseSample() => new()
    {
        SampleId = "s1", CaseId = "c1", Code = "BAB.B",
        Beschreibung = "Riss laengs, 12 Uhr, Scheitel",   // >=10 Zeichen, plausibel
        MeterStart = 3.0, MeterEnd = 3.0,
        InspectionDate = new DateTime(2024, 5, 1),
        TrainingEligible = true
    };

    [Fact]
    public void IndexWorthy_True_ForEligibleSample()
        => Assert.True(KnowledgeBaseManager.IsIndexWorthy(BaseSample()));

    [Fact]
    public void IndexWorthy_False_WhenInspectionDateMissing()
    {
        var s = BaseSample();
        s.InspectionDate = null;
        s.TrainingEligible = false;
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(s));
    }
}
```

- [ ] **Step 2: Test laufen lassen — Eligibility-Test muss fehlschlagen**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KnowledgeBaseManagerEligibilityTests"`
Expected: `IndexWorthy_False_WhenInspectionDateMissing` schlägt fehl (IsIndexWorthy gibt noch true).

- [ ] **Step 3: Eligibility-Gate in `IsIndexWorthy` ergänzen**

```csharp
// KnowledgeBaseManager.cs — am Anfang von IsIndexWorthy, nach dem Beschreibung/Code/Katalog-Block,
// vor dem Plausibilitaets-Check einfuegen:
        // Trainings-Eligibility (Datum/Herkunft) muss auch fuer die KB gelten — konsistent zum Export.
        // Evaluate nur EINMAL aufrufen, Ergebnis wiederverwenden.
        var eligibility = TrainingSampleEligibility.Evaluate(sample);
        if (!eligibility.IsEligible)
        {
            Debug.WriteLine($"[KnowledgeBaseManager] Sample {sample.SampleId} nicht trainingsfaehig: {eligibility.Reason}");
            return false;
        }
```

(`TrainingSampleEligibility` liegt in `AuswertungPro.Next.Application.Ai.Training` — bereits via `using` in der Datei vorhanden.)

- [ ] **Step 4: Bestehende KB-Index-Tests anpassen**

Run zuerst zur Ermittlung der Brüche: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KnowledgeBase"`
In jedem Test, der ein Sample erfolgreich indexieren erwartet (z.B. `KnowledgeBaseManagerEvalGuardTests` der NICHT-eval-Fall, `KnowledgeBaseInfrastructureTests`), beim Sample-Setup ergänzen:

```csharp
        InspectionDate = new DateTime(2024, 1, 1),
        TrainingEligible = true,
```

- [ ] **Step 5: Tests laufen lassen — alle grün**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KnowledgeBase|FullyQualifiedName~KnowledgeBaseManagerEligibilityTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs tests/AuswertungPro.Next.Infrastructure.Tests/
git commit -m "fix(kb): IsIndexWorthy prueft TrainingEligible (kein untaugliches Sample in die KB)"
```

---

### Task 3: `DeindexSample` wirksam — Reject & Remove entfernen aus der KB

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs:782-789` (Reject) + neuer Remove-Pfad
- Verifizieren: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs:131` (DeindexSample existiert)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseDeindexTests.cs` (neu)

- [ ] **Step 1: Failing-Test schreiben** — DeindexSample entfernt Sample + Embedding wirklich.

```csharp
// tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseDeindexTests.cs
using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public class KnowledgeBaseDeindexTests
{
    [Fact]
    public async Task DeindexSample_RemovesSampleAndEmbedding()
    {
        // In-Memory-KB wie in den bestehenden KB-Tests aufbauen (gleiche Helper-Konstruktion verwenden,
        // siehe KnowledgeBaseInfrastructureTests fuer den Aufbau von KnowledgeBaseContext + FakeEmbedder).
        using var ctx = TestKb.NewInMemoryContext();              // vorhandener Test-Helfer
        var mgr = new KnowledgeBaseManager(ctx, TestKb.FakeEmbedder());
        var s = new TrainingSample {
            SampleId = "s1", CaseId = "c1", Code = "BAB.B",
            Beschreibung = "Riss laengs, 12 Uhr, Scheitel",
            InspectionDate = new DateTime(2024,1,1), TrainingEligible = true
        };
        Assert.True(await mgr.IndexSampleAsync(s));
        Assert.True(mgr.IsIndexed("s1"));

        mgr.DeindexSample("s1");

        Assert.False(mgr.IsIndexed("s1"));
    }
}
```

> **Falls `TestKb`/`IsIndexed` nicht existieren:** zuerst den Aufbau aus `KnowledgeBaseInfrastructureTests.cs` bzw. `KnowledgeBaseManagerEvalGuardTests.cs` übernehmen (gleicher In-Memory-Context + Fake-Embedder); `IsIndexed` wird in `KnowledgeBaseManagerEvalGuardTests` bereits genutzt — denselben Mechanismus verwenden.

- [ ] **Step 2: Test laufen lassen — muss bestehen ODER zeigen, dass DeindexSample bereits korrekt ist**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KnowledgeBaseDeindexTests"`
Expected: PASS (DeindexSample ist bereits implementiert, KnowledgeBaseManager.cs:131). Dieser Test friert das Verhalten ein, das die UI gleich nutzt.

- [ ] **Step 3: VM — Reject deindexiert + neuer Remove-Befehl**

```csharp
// TrainingCenterViewModel.cs — RejectSampleAsync (782-789) ersetzen:
    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RejectSampleAsync()
    {
        if (SelectedSample is null) return;
        SelectedSample.Status = TrainingSampleStatus.Rejected;
        TryDeindexSample(SelectedSample.SampleId);          // wirksame Korrektur
        StatusText = $"Rejected: {SelectedSample.SampleId}";
        await PersistSamplesAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RemoveSampleAsync()
    {
        if (SelectedSample is null) return;
        SelectedSample.Status = TrainingSampleStatus.Removed;   // nachvollziehbar: Status entfernt
        TryDeindexSample(SelectedSample.SampleId);              // + KB-Eintrag weg
        StatusText = $"Entfernt: {SelectedSample.SampleId}";
        await PersistSamplesAsync();
    }

    // Exakt das Konstruktionsmuster aus IncrementalKbUpdateAsync (VM:2145-2160).
    // DeindexSample braucht kein erreichbares Ollama/echtes Embedding — der EmbeddingService
    // ist nur Konstruktor-Pflicht des KnowledgeBaseManager.
    private void TryDeindexSample(string sampleId)
    {
        try
        {
            var ollamaConfig = new AppSettingsAiSettingsProvider().Load().ToOllamaConfig();
            _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
            using var kbCtx = new KnowledgeBaseContext();
            var embedder = new EmbeddingService(_kbHttpClient, ollamaConfig);
            var kbManager = new KnowledgeBaseManager(kbCtx, embedder);
            kbManager.DeindexSample(sampleId);
        }
        catch { /* KB evtl. nicht erreichbar — Status-Aenderung bleibt persistiert */ }
    }
```

Dieses Muster ist 1:1 das aus `IncrementalKbUpdateAsync` (VM:2145-2160): `AppSettingsAiSettingsProvider().Load().ToOllamaConfig()`, wiederverwendeter `_kbHttpClient`, `new KnowledgeBaseContext()`, `new EmbeddingService(_kbHttpClient, ollamaConfig)`. Keine Eval-Hashes nötig (Deindex löscht nur).

- [ ] **Step 4: Build + manueller Verifikations-Hinweis**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly`
Expected: 0 Fehler. (UI-Pfad: nach „Reject"/„Entfernen" ist das Sample via `IsIndexed` nicht mehr in der KB — durch Task-3-Test abgesichert.)

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseDeindexTests.cs
git commit -m "fix(training): Reject/Remove entfernt Sample aus der KB (Deindex)"
```

---

### Task 4: Eval-Schutz im Codier-KB-Pfad (`CodingSessionService`)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/CodingSessionService.cs:23-26` (ctor), `:218` (Manager-Aufbau)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.LiveDetection.cs:1038` (produktive Konstruktionsstelle)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/CodingModeWindow.xaml.cs:80` (zweite Konstruktionsstelle; aktuell ungenutzt, der Konsistenz halber mitziehen)

> `CodingSessionService` wird NICHT im ServiceProvider gebaut. Belegt: `git grep -n "new CodingSessionService" -- src/` → genau zwei Treffer (PlayerWindow.LiveDetection.cs:1038, CodingModeWindow.xaml.cs:80). Beide UI-Code-behind, dort ist `AppSettings.Load()` der etablierte Config-Zugriff (kein vorab geladenes Settings-Objekt vorhanden).

- [ ] **Step 1: ctor um Eval-Hashes-Provider erweitern**

```csharp
// CodingSessionService.cs:20-26
    private readonly Func<OllamaConfig?> _ollamaConfigProvider;
    private readonly Func<IReadOnlySet<string>> _evalHashesProvider;
    private CodingSession? _session;

    public CodingSessionService(
        Func<OllamaConfig?>? ollamaConfigProvider = null,
        Func<IReadOnlySet<string>>? evalHashesProvider = null)
    {
        _ollamaConfigProvider = ollamaConfigProvider ?? (() => null);
        _evalHashesProvider = evalHashesProvider
            ?? (() => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
```

- [ ] **Step 2: Manager mit Eval-Hashes bauen (Zeile 218)**

```csharp
// CodingSessionService.cs:218 — ersetzen:
            var kbManager = new InfraKnowledgeBase.KnowledgeBaseManager(db, embedder, _evalHashesProvider());
```

- [ ] **Step 3: Beide Konstruktionsstellen verdrahten**

An BEIDEN Treffern den `new CodingSessionService(...)`-Aufruf um den Eval-Hashes-Provider als zweites Argument erweitern (gleiche Hash-Quelle wie TrainingCenterViewModel.cs:1247):

```csharp
// PlayerWindow.LiveDetection.cs:1038  und  CodingModeWindow.xaml.cs:80
// dem bestehenden new CodingSessionService(<vorhandenes erstes Argument>) als 2. Argument hinzufuegen:
    , () => AuswertungPro.Next.Application.Ai.Training.EvalContaminationGuard
              .LoadEvalImageHashes(AppSettings.Load().EvalSetRoot)
```

Falls ein Treffer kein erstes Argument übergibt, das vorhandene erste Argument unverändert lassen und den Provider als zweites ergänzen. Beide Stellen sind UI-Code-behind → `AppSettings.Load()` ist dort der etablierte, einzige Config-Zugriff (kein redundanter Reload).

- [ ] **Step 4: Build**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly`
Expected: 0 Fehler.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/CodingSessionService.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.LiveDetection.cs src/AuswertungPro.Next.UI/Views/Windows/CodingModeWindow.xaml.cs
git commit -m "fix(kb): Eval-Kontaminationsschutz im Codier-KB-Pfad"
```

---

### Task 5: Eval-Schutz im Review-Approve/FeedbackIngestion-Pfad (`TrainingCenterWindow`)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml.cs:225` (Manager-Aufbau ohne Eval-Hashes)

- [ ] **Step 1: Eval-Hashes ergänzen** — in der Methode `CreateFeedbackIngestionService` (≈ Zeile 216-230) die Zeile 225 ersetzen.

Vorher (Zeile 225):
```csharp
            kbManager = new InfraKnowledgeBase.KnowledgeBaseManager(db, embedder);
```
Nachher:
```csharp
            var evalHashes = AuswertungPro.Next.Application.Ai.Training.EvalContaminationGuard
                .LoadEvalImageHashes(AppSettings.Load().EvalSetRoot);
            kbManager = new InfraKnowledgeBase.KnowledgeBaseManager(db, embedder, evalHashes);
```

`cfg` wird in dieser Methode bereits via `AppSettingsAiSettingsProvider().Load()` für Ollama geladen; der EvalSetRoot kommt aus `AppSettings.Load()` — ein einmaliger, zusätzlicher Load (kein Reload in einer Schleife).

- [ ] **Step 2: Build**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly`
Expected: 0 Fehler.

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml.cs
git commit -m "fix(kb): Eval-Schutz im Review-Approve/FeedbackIngestion-Pfad"
```

---

### Task 6: Batch-Import entschärfen — kein Auto-Approve, kein Auto-Index, nur über Review

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs` (`BatchImportAndIndexAsync`, ~1136; Auto-Approve ~1397; KB-Index ~1448-1466)

- [ ] **Step 1: Auto-Approve entfernen (Zeile 1397)**

Vorher:
```csharp
                s.Status = !string.IsNullOrEmpty(s.FramePath)
                    ? TrainingSampleStatus.Approved
                    : TrainingSampleStatus.New;
```
Nachher:
```csharp
                s.Status = TrainingSampleStatus.New;   // nie Auto-Approve; Freigabe nur ueber Review (Modul I)
```

- [ ] **Step 2: KB-Index aus dem Batch-Pfad entfernen**

Den gesamten KB-Index-Block in `BatchImportAndIndexAsync` (≈ Zeilen 1430-1466, der mit `if (kbManager is not null)` beginnt und `await kbManager.IndexSamplesAsync(approvedForKb, ct)` enthält) entfernen und durch eine Log-Zeile ersetzen:
```csharp
            Log($"{totalNew} Samples als Kandidaten gespeichert (Status: Neu). Freigabe ueber Review (Modul I) — KEIN Auto-Index.");
```
Damit ebenfalls entfernen (sonst ungenutzt → Compile-Fehler): die KB-Manager-Konstruktion oben in derselben Methode (≈ Zeilen 1245-1248: `kbCtx`, `kbEvalHashes`, `kbManager`) und alle `totalIndexed`-Verwendungen in dieser Methode (Variable löschen bzw. aus der Abschluss-Meldung nehmen).

- [ ] **Step 3: Build + Verifikation**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly`
Expected: 0 Fehler.
Verifikation: `git grep -n "IndexSamplesAsync\|IndexSampleAsync" src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs` zeigt KEINEN Treffer mehr innerhalb von `BatchImportAndIndexAsync` (nur noch der Self-Training-/Nachhol-Pfad `IncrementalKbUpdateAsync`).

> Hinweis: Das Einreihen der Protokoll-Kandidaten in die Review-Queue ist die **Startdaten-Brücke** und gehört zu Plan 2 (Modul I). In Phase 1 bleiben die Batch-Samples als `New` im Store — sie werden NICHT mehr gelernt, das ist das Sicherheitsziel.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs
git commit -m "fix(training): Batch-Import ohne Auto-Approve/Auto-Index, nur ueber Review"
```

---

### Task 7: YOLO-Export blockiert Samples ohne echte Box

**Es gibt ZWEI YOLO-Label-Schreibpfade — BEIDE müssen gegen Dummy-Boxen geschlossen werden:**
- Pfad 1: `src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs` — Filter (Z.30-35) + Dummy-Box-Getter (`return (0.5, 0.5, 0.8, 0.8);`, Z.155).
- Pfad 2: `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs` — `ExportYoloLocalAsync`, Gate vor Bild-Kopie (Z.1063) + Dummy-Else-Zweig (Z.1080-1085, `"... 0.500000 0.500000 0.800000 0.800000"`).

- [ ] **Step 1: Pfad 1 — `YoloDatasetExportService` schließen**

(a) Filter (Z.30-35) um `&& s.HasBbox` erweitern:
```csharp
        var approved = samples
            .Where(s => s.Status == TrainingSampleStatus.Approved
                        && IsTrainingExportEligible(s)
                        && s.HasBbox                              // YOLO nur mit echter Box
                        && !string.IsNullOrEmpty(s.FramePath)
                        && File.Exists(s.FramePath))
            .ToList();
```
(b) Dummy-Box-Getter (Z.146-155): die Dummy-Rückgabe entfernen — nach dem Filter ist sie unerreichbar; defensiv hart machen:
```csharp
        if (sample.HasBbox)
            return (sample.BboxXCenter!.Value, sample.BboxYCenter!.Value,
                    sample.BboxWidth!.Value, sample.BboxHeight!.Value);
        throw new InvalidOperationException(
            $"YOLO-Export ohne BBox fuer Sample {sample.SampleId} — darf nach dem HasBbox-Filter nie passieren.");
```

- [ ] **Step 2: Pfad 2 — `ExportYoloLocalAsync` schließen**

(a) Gate VOR der Bild-Kopie einfügen (direkt nach `if (!File.Exists(s.FramePath)) continue;`, Z.1063), damit kein verwaistes Bild ohne Label kopiert wird:
```csharp
                if (!s.HasBbox) continue;   // YOLO nur mit echter Box — keine Dummy-Labels, kein Bild ohne Label
```
(b) Den Else-Zweig mit der Fallback-Box (Z.1080-1085) ersatzlos entfernen — nur der `if (s.HasBbox)`-Zweig (Z.1074-1079) bleibt:
```csharp
                // Echte BBox aus Eingabemarker:
                await File.WriteAllTextAsync(lblPath,
                    $"{clsIdx} {s.BboxXCenter!.Value:F6} {s.BboxYCenter!.Value:F6} " +
                    $"{s.BboxWidth!.Value:F6} {s.BboxHeight!.Value:F6}", ct);
```
Die Log-Zeile Z.1049 (`{withBbox} mit echten BBoxen`) auf die tatsächlich exportierte Anzahl anpassen.

- [ ] **Step 3: Build + Verifikation (beide Pfade dicht)**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly` → 0 Fehler.
Verifikation (keine Dummy-Box bleibt übrig):
```bash
git grep -n "0.5, 0.5, 0.8\|0.500000 0.500000 0.800000" -- src/
```
Expected: **kein Treffer** in den beiden App-Pfaden (`YoloDatasetExportService`, `ExportYoloLocalAsync`).
Ausnahme (bewusst belassen, Entscheid 2026-06-02): `StageAExporter.cs:497` ist ein **dritter** YOLO-Label-Schreiber im Offline-Bulk-Export, aber per `requireBoundingBox`-Schalter gesteuert (true = boxlose Samples verworfen, Z.244; false = Dummy-Box). Kein unbedingter Dummy wie die App-Pfade; Code + pinnender Test (`StageAExporterTests.cs:94`) bleiben unverändert.

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs
git commit -m "fix(yolo): Export nur mit echter Box, keine Dummy-Labels (beide Pfade)"
```

---

## Abschluss Phase 1

- [ ] **Volltest:** `dotnet test AuswertungPro.sln` — alle grün, 0 Skips.
- [ ] **Untersuchung (Spec Fix 8):** Warum fehlt `InspectionDate` bei den Göschenen/9866-Daten? (PDF-Datum nicht geparst vs. nicht im Dokument.) Ergebnis notieren — entscheidet, ob viele Samples export-/index-gesperrt bleiben. Diese Untersuchung ist **read-only** und blockiert Phase 1 nicht.

**Danach:** Plan 2 (Modul ① Review & Freigabe) und Plan 3 (Modul ② Trainingsdaten & Modell) gemäß Spec.
