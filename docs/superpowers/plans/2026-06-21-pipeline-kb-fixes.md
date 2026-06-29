# Pipeline- & KB-Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Den Kern-Defekt „Frame verschwindet bei DINO=0 trotz Klassifikator-Hinweis" beheben, KI-Datenintegrität (Eval-Leakage, KB-Gold-Metadaten) härten und Betriebs-/Doku-Altlasten entfernen.

**Architecture:** Reine Verdrahtungs-/Härtungs-Fixes ohne großes Refactoring. Neue Logik als testbare Einheit (`ClassifierOnlyStructuralPolicy`). Bestehende Muster wiederverwenden (env-Flags wie `ClassifierDecisionEnabled`, additive SQLite-Migration `MigrateAddColumn`, `EvalContaminationGuard`).

**Tech Stack:** .NET 10 (C#), xUnit, Microsoft.Data.Sqlite (WAL), Python-FastAPI-Sidecar (pytest).

## Global Constraints

- Keine NuGet-Pakete ohne Rückfrage (CLAUDE.md).
- Keine Änderung an `sidecar/models/active.json` oder Modellgewichten (Modell-Promotion bleibt dem model-promotion-warden vorbehalten).
- Kommentare auf Deutsch (CLAUDE.md).
- Tests laufen über `dotnet test AuswertungPro.sln` bzw. Sidecar `pytest` (Default-Marker, ohne gpu/e2e).
- Schichtrichtung: Domain ← Application ← Infrastructure ← UI. Application darf Infrastructure NICHT referenzieren.
- Default-Verhalten der Sidecar-Modell-Residenz bleibt unverändert (Fix #5 nur reaktiv).
- Spec: `docs/superpowers/specs/2026-06-21-pipeline-kb-fixes-design.md`.

---

## Task 1: Fix #4 — Settings/Doku aufräumen (qwen2.5 raus, SAM2.1-Doku)

**Files:**
- Modify/Delete: `Start-KiMaximum4070.ps1`
- Modify: stale Doku/Kommentare (per Grep ermittelt)

**Interfaces:** Keine Code-Schnittstellen. Reine Cleanup-Task, de-riskt Betrieb (verbotenes qwen2.5-Preset).

- [ ] **Step 1: Verstöße auflisten**

Run:
```bash
cd "c:/Sewer-Studio_KI_4.4"
grep -rniE "qwen2\.5|qwen3\.5" --include=*.ps1 --include=*.md --include=*.cs --include=*.py . | grep -viE "OllamaConfig\.cs|nie.*qwen2\.5|never.*2\.5|deep-audit|specs/2026-06-21|plans/2026-06-21"
```
Expected: u.a. `Start-KiMaximum4070.ps1:18-19` (`qwen2.5vl:7b`, `qwen2.5:7b`), evtl. veraltete `docs/`-Stellen.

- [ ] **Step 2: Preset entfernen**

`Start-KiMaximum4070.ps1` ist ein veraltetes 4070-Preset auf einer 5090-Workstation und setzt verbotenes qwen2.5. Löschen:
```bash
git rm "Start-KiMaximum4070.ps1"
```
(Falls der User es behalten will: stattdessen die zwei Zeilen auf `qwen3-vl:8b-q8` / `qwen3-vl:8b-q8` ändern. Standard = löschen.)

- [ ] **Step 3: Verbleibende qwen2.5/qwen3.5-Texte in Doku bereinigen**

Für jede in Step 1 gefundene Doku-/Kommentar-Stelle (außer den bewussten „NIE qwen2.5"-Schutzkommentaren): Text auf den Ist-Zustand korrigieren (Primary `qwen3-vl:8b-q8` via GPU-Auto, Embeddings `nomic-embed-text`). Keine Code-Logik ändern.

- [ ] **Step 4: SAM-Fallback-Doku angleichen**

Run:
```bash
grep -rniE "vit_h|sam1|sam 1|sam2[^.]|sam 2 " --include=*.md --include=*.cs docs/ src/ | grep -vi "sam 2.1\|sam2.1"
```
Stale Stellen, die SAM1/SAM2-Fallback behaupten, auf „produktiv nur SAM 2.1; SAM1/alt-SAM2 werden abgelehnt" korrigieren. (`sidecar/sidecar/config.py`-Kommentar ist bereits korrekt — nicht anfassen.)

- [ ] **Step 5: Build prüfen (keine Code-Änderung, nur Sicherheit)**

Run: `dotnet build AuswertungPro.sln`
Expected: unverändert grün (Doku/PS1 sind nicht Teil des Builds).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: veraltetes qwen2.5-Preset entfernen, SAM2.1-Doku angleichen (Audit Fix #4)"
```

---

## Task 2: Fix #1a — ClassifierOnlyStructuralPolicy (reine, testbare Einheit)

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/ClassifierOnlyStructuralPolicy.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/ClassifierOnlyStructuralPolicyTests.cs`

**Interfaces:**
- Consumes: `VsaCodeResolver.ResolveFromClassifier(IReadOnlyList<YoloClassifyPrediction>?, double currentMeter, double totalLength, IReadOnlyList<(string,string,double)>?, bool isBend) → VsaCodeResolver.ResolvedCode?` (Infrastructure); `YoloClassifyPrediction(string ClassName, double Confidence)`.
- Produces: `ClassifierOnlyStructuralPolicy.TryResolve(IReadOnlyList<YoloClassifyPrediction>? predictions, double meter, double reachLength, bool isBend, double minConfidence) → VsaCodeResolver.ResolvedCode?` — liefert einen Grundgerüst-Code (BCA/BCC/BCD/BCE) oder `null`.

*(Liegt in Infrastructure, weil `VsaCodeResolver` und `YoloClassifyPrediction` Infrastructure sind — Application darf sie nicht referenzieren. Reine Logik, kein I/O → unit-testbar wie `TemporalFindingDeduplicator`.)*

- [ ] **Step 1: Failing test schreiben**

`tests/AuswertungPro.Next.Pipeline.Tests/ClassifierOnlyStructuralPolicyTests.cs`:
```csharp
using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline; // VisionPipelineDtos (YoloClassifyPrediction)
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class ClassifierOnlyStructuralPolicyTests
{
    private static IReadOnlyList<YoloClassifyPrediction> Preds(params (string c, double p)[] xs)
    {
        var list = new List<YoloClassifyPrediction>();
        foreach (var (c, p) in xs) list.Add(new YoloClassifyPrediction(c, p));
        return list;
    }

    [Fact]
    public void Bcd_HighConfidence_AtPipeStart_ResolvesToBcd()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            Preds(("BCD", 0.91)), meter: 0.2, reachLength: 50, isBend: false, minConfidence: 0.60);
        Assert.NotNull(r);
        Assert.Equal("BCD", r!.Code);
    }

    [Fact]
    public void DamageCode_IsRejected_NotGrundgeruest()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            Preds(("BAB", 0.95)), meter: 10, reachLength: 50, isBend: false, minConfidence: 0.60);
        Assert.Null(r);
    }

    [Fact]
    public void BelowMinConfidence_IsRejected()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            Preds(("BCA", 0.40)), meter: 10, reachLength: 50, isBend: false, minConfidence: 0.60);
        Assert.Null(r);
    }

    [Fact]
    public void Bend_ResolvesToBcc_ViaVeto()
    {
        // isBend=true + top1 BCE -> ResolveFromClassifier liefert BCC (Bogen-Veto)
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            Preds(("BCE", 0.55)), meter: 12, reachLength: 50, isBend: true, minConfidence: 0.60);
        Assert.NotNull(r);
        Assert.Equal("BCC", r!.Code);
    }

    [Fact]
    public void NoPredictions_ReturnsNull()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            null, meter: 5, reachLength: 50, isBend: false, minConfidence: 0.60);
        Assert.Null(r);
    }
}
```

- [ ] **Step 2: Test fehlschlagen lassen**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~ClassifierOnlyStructuralPolicyTests"`
Expected: FAIL (Compile-Fehler: `ClassifierOnlyStructuralPolicy` existiert nicht).

- [ ] **Step 3: Policy implementieren**

`src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/ClassifierOnlyStructuralPolicy.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Erzeugt aus reinen YOLO-cls-Predictions einen Grundgeruest-Code (BCA/BCC/BCD/BCE),
/// wenn DINO keine Box liefert. Reine Business-Logik (kein I/O) -> unit-testbar.
/// Nutzt VsaCodeResolver.ResolveFromClassifier (erbt damit Bogen-Veto + Ortsgebunden-Gate)
/// und akzeptiert nur Grundgeruest-Codes oberhalb der Mindestkonfidenz.
/// </summary>
public static class ClassifierOnlyStructuralPolicy
{
    // Bestandsaufnahme/Grundgeruest: Anschluss, Bogen, Rohranfang, Rohrende.
    // Bewusst KEINE Schadenscodes (ohne SAM-Maske fehlt Geometrie/Quantifizierung).
    private static readonly HashSet<string> Grundgeruest =
        new(StringComparer.OrdinalIgnoreCase) { "BCA", "BCC", "BCD", "BCE" };

    public static VsaCodeResolver.ResolvedCode? TryResolve(
        IReadOnlyList<YoloClassifyPrediction>? predictions,
        double meter,
        double reachLength,
        bool isBend,
        double minConfidence)
    {
        if (predictions is null || predictions.Count == 0)
            return null;

        var resolved = VsaCodeResolver.ResolveFromClassifier(
            predictions, meter, reachLength, importContext: null, isBend: isBend);

        if (resolved is null)
            return null;

        if (!Grundgeruest.Contains(resolved.Code))
            return null;

        if (resolved.Confidence < minConfidence)
            return null;

        return resolved;
    }
}
```

- [ ] **Step 4: Test grün**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~ClassifierOnlyStructuralPolicyTests"`
Expected: PASS (5 Tests).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/ClassifierOnlyStructuralPolicy.cs tests/AuswertungPro.Next.Pipeline.Tests/ClassifierOnlyStructuralPolicyTests.cs
git commit -m "feat: ClassifierOnlyStructuralPolicy (Grundgeruest-Code bei DINO=0) (Audit Fix #1a)"
```

---

## Task 3: Fix #1b — Policy in MultiModelAnalysisService verdrahten

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs` (Flags + `dino_no_boxes`-Zweig ~440-448)

**Interfaces:**
- Consumes: `ClassifierOnlyStructuralPolicy.TryResolve(...)` (Task 2), `_codeVoting.RegisterAndVote(string?, double)`, `VsaCodeTree.LookupLabel(string)`, `EnhancedFinding(...)` (15-arg record), `EvidenceVector(...)`, `GetDedupMeterMetadata(bool)`, `deduplicator.Update(...)` / `deduplicator.AdvanceAll()`.
- Produces: zwei neue Properties `ClassifierOnlyStructuralEnabled` (bool, Default true) und `ClassifierOnlyMinConfidence` (double, Default 0.60) am Service.

- [ ] **Step 1: Neue Flags als Env-Properties hinzufügen**

In `MultiModelAnalysisService.cs` direkt nach `ClassifierDecisionEnabled` (~Zeile 52-54) einfügen:
```csharp
    /// <summary>
    /// Fix #1: Wenn DINO keine Box liefert, aber der Klassifikator einen Grundgeruest-Code
    /// (BCA/BCC/BCD/BCE) ueber das Voting bestaetigt, wird ein box-loser Befund erzeugt,
    /// statt den Frame still zu verwerfen. Default AN, reversibel ueber Env.
    /// </summary>
    public bool ClassifierOnlyStructuralEnabled { get; set; } =
        !Configuration.AiSettingsFactory.ParseBool(
            Environment.GetEnvironmentVariable("SEWERSTUDIO_CLASSIFIER_ONLY_STRUCTURAL_OFF"));

    /// <summary>Mindestkonfidenz fuer den box-losen Grundgeruest-Befund (Fix #1).</summary>
    public double ClassifierOnlyMinConfidence { get; set; } = 0.60;
```
*(Default-AN-Muster: Flag heißt `..._OFF` und wird negiert — so ist ohne Env-Var `true`. `ParseBool` ist die etablierte Helper-Methode.)*

- [ ] **Step 2: `dino_no_boxes`-Zweig erweitern**

Aktueller Block (~Zeile 440-448):
```csharp
            if (dinoResult.Detections.Count == 0)
            {
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: false));
                trace.Path = "dino_no_boxes";
                trace.DropReason = "dino_no_boxes";
                await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                continue;
            }
```
Ersetzen durch:
```csharp
            if (dinoResult.Detections.Count == 0)
            {
                // Fix #1: Bevor der Frame verworfen wird — wenn der Klassifikator einen
                // Grundgeruest-Code (BCA/BCC/BCD/BCE) ueber das Voting bestaetigt, einen
                // box-losen Befund erzeugen. Rettet Bestandsaufnahme, die DINO nicht boxt.
                var meterNoBox = EstimateMeter(t, duration, ref lastMeter);
                EnhancedFinding? structuralOnly = null;
                if (ClassifierOnlyStructuralEnabled && clsResult is { Predictions.Count: > 0 })
                {
                    var resolved = ClassifierOnlyStructuralPolicy.TryResolve(
                        clsResult.Predictions, meterNoBox, EstimatedReachLengthM,
                        isBend: clsResult.IsBend, minConfidence: ClassifierOnlyMinConfidence);
                    if (resolved is not null)
                    {
                        var confirmed = _codeVoting.RegisterAndVote(resolved.Code, meterNoBox);
                        if (confirmed is not null)
                        {
                            structuralOnly = new EnhancedFinding(
                                Label: VsaCodeTree.LookupLabel(confirmed) ?? confirmed,
                                VsaCodeHint: confirmed,
                                Severity: 1,                 // Bestandsaufnahme: keine Schadensschwere
                                PositionClock: null,
                                ExtentPercent: null, HeightMm: null, WidthMm: null,
                                IntrusionPercent: null, CrossSectionReductionPercent: null,
                                DiameterReductionMm: null,
                                BboxX1: null, BboxY1: null, BboxX2: null, BboxY2: null,
                                Notes: $"classifier-only (DINO 0 Boxen), conf={resolved.Confidence:F2}, {resolved.Source}");
                            trace.ClassifierCode = confirmed;
                            trace.ClassifierConfidence = resolved.Confidence;
                            trace.ClassifierModel = ClassifierModelTag(clsResult);
                            trace.ClassifierVoteConfirmed = true;
                        }
                    }
                }

                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: structuralOnly is null));

                if (structuralOnly is not null)
                {
                    trace.Path = "classifier_only_structural";
                    trace.FindingsBuilt = 1;
                    var evidence = new EvidenceVector(
                        YoloConf: clsResult.Predictions[0].Confidence, DinoConf: 0.0, FrameCount: 1);
                    var (mSrc, mEst) = GetDedupMeterMetadata(qwenMeterAccepted: false);
                    detections.AddRange(deduplicator.Update(
                        new List<EnhancedFinding> { structuralOnly },
                        meterNoBox, evidence, meterSource: mSrc, isMeterEstimated: mEst));
                    trace.ActiveCount = deduplicator.ActiveCount;
                    trace.DetectionsTotal = detections.Count;
                }
                else
                {
                    trace.Path = "dino_no_boxes";
                    trace.DropReason = "dino_no_boxes";
                    detections.AddRange(deduplicator.AdvanceAll());
                }

                await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                continue;
            }
```
*(Hinweis: ggf. `using System.Collections.Generic;` und `using AuswertungPro.Next.Domain.VsaCatalog;` für `VsaCodeTree` ergänzen, falls nicht vorhanden — vor Build prüfen.)*

- [ ] **Step 3: Build**

Run: `dotnet build AuswertungPro.sln`
Expected: grün. Falls `VsaCodeTree`/`EnhancedFinding`/`EvidenceVector` nicht aufgelöst → fehlendes `using` ergänzen (`AuswertungPro.Next.Domain.VsaCatalog`, `AuswertungPro.Next.Application.Ai`, `AuswertungPro.Next.Application.Ai.QualityGate`).

- [ ] **Step 4: Bestehende Pipeline-Tests grün**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~Pipeline.Tests"`
Expected: PASS (keine Regression; End-to-End-Abdeckung folgt in Task 4).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs
git commit -m "feat: box-loser Grundgeruest-Befund bei DINO=0 im Batch (Audit Fix #1b)"
```

---

## Task 4: Fix #2 — IVisionPipelineClient-Seam + End-to-End-Test

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/IVisionPipelineClient.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs` (`: IVisionPipelineClient`)
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs` (Feld-Typ + optionaler Frame-Source-Seam)
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/MultiModelAnalysisServiceTests.cs`

**Interfaces:**
- Produces: `IVisionPipelineClient` mit den von der Pipeline genutzten Methoden; Konstruktor-Seam `Func<string, double, double, CancellationToken, IAsyncEnumerable<VideoFrame>>? frameSource`.

- [ ] **Step 1: Interface extrahieren**

`src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/IVisionPipelineClient.cs`:
```csharp
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Vom Multi-Model-Pfad genutzte Sidecar-Aufrufe (Seam fuer Tests).</summary>
public interface IVisionPipelineClient
{
    Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default);
    Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default);
    Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default);
    Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default);
    Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 2: VisionPipelineClient das Interface implementieren lassen**

In `VisionPipelineClient.cs` Klassendeklaration (~Zeile 16):
```csharp
public sealed class VisionPipelineClient : IVisionPipelineClient
```
(Signaturen stimmen bereits überein — keine weiteren Änderungen nötig.)

- [ ] **Step 3: Build (Interface-Konformität)**

Run: `dotnet build AuswertungPro.sln`
Expected: grün (alle Interface-Methoden existieren bereits public auf der Klasse).

- [ ] **Step 4: Service-Feld auf Interface + Frame-Seam umstellen**

In `MultiModelAnalysisService.cs`:
- Feldtyp `private readonly VisionPipelineClient _client;` → `private readonly IVisionPipelineClient _client;`
- Konstruktor-Parameter `VisionPipelineClient client` → `IVisionPipelineClient client`.
- Neues optionales Feld + Parameter für die Frame-Quelle:
```csharp
    // Seam fuer Tests: Default = echter ffmpeg-Stream. Ersetzt VideoFrameStream.Open im Test.
    private readonly Func<string, double, double, CancellationToken, IAsyncEnumerable<VideoFrame>> _frameSource;
```
Im Konstruktor (zusätzlicher optionaler Parameter am Ende):
```csharp
        Func<string, double, double, CancellationToken, IAsyncEnumerable<VideoFrame>>? frameSource = null)
```
und im Rumpf:
```csharp
        _frameSource = frameSource ?? ((ffmpeg, vid, dur, token) =>
            VideoFrameStream.Open(ffmpeg, vid, FrameStepSeconds, dur, token).ReadFramesAsync(token));
```
Den Aufruf in `AnalyzeAsync` (~Zeile 146-149) ersetzen:
```csharp
        await foreach (var frame in _frameSource(_ffmpegPath, videoPath, duration, ct).ConfigureAwait(false))
```
*(Den `await using var stream = VideoFrameStream.Open(...)` entfernen; die Default-Lambda kapselt Open+ReadFramesAsync. Falls `VideoFrameStream` IAsyncDisposable über die Lebensdauer braucht, alternativ die Lambda ein `IAsyncEnumerable` liefern lassen, das den Stream intern disposed — siehe Step 5 Implementierungsnotiz.)*

- [ ] **Step 5: Frame-Source-Default sauber kapseln (Dispose)**

Damit der echte Stream korrekt disposed wird, eine private Helper-Methode statt der Inline-Lambda:
```csharp
    private async IAsyncEnumerable<VideoFrame> DefaultFrameSource(
        string ffmpeg, string videoPath, double duration,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = VideoFrameStream.Open(ffmpeg, videoPath, FrameStepSeconds, duration, ct);
        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
            yield return frame;
    }
```
und `_frameSource = frameSource ?? DefaultFrameSource;`

- [ ] **Step 6: ServiceProvider-Aufrufstelle prüfen**

Run:
```bash
grep -rn "new MultiModelAnalysisService\|CreateVideoAnalysisPipeline" src/AuswertungPro.Next.UI/ServiceProvider.cs src/AuswertungPro.Next.Infrastructure/ | grep -v obj
```
Sicherstellen, dass alle `new MultiModelAnalysisService(...)`-Aufrufe weiterhin kompilieren (neuer Parameter ist optional). `VisionPipelineClient` wird implizit als `IVisionPipelineClient` akzeptiert.

- [ ] **Step 7: End-to-End-Test schreiben (failing)**

`tests/AuswertungPro.Next.Pipeline.Tests/MultiModelAnalysisServiceTests.cs` — Stub-Client + Stub-Frames:
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class MultiModelAnalysisServiceTests
{
    // Stub: cls meldet BCD hoch+usable; YOLO relevant; DINO leer.
    private sealed class StubClient : IVisionPipelineClient
    {
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => Task.FromResult<SidecarHealthResponse?>(null);
        public Task<YoloResponse> DetectYoloAsync(YoloRequest r, CancellationToken ct = default)
            => Task.FromResult(new YoloResponse(IsRelevant: true, Detections: new List<YoloDetectionDto>(), FrameClass: "sweep", InferenceTimeMs: 0));
        public Task<DinoResponse> DetectDinoAsync(DinoRequest r, CancellationToken ct = default)
            => Task.FromResult(new DinoResponse(Detections: new List<DinoDetectionDto>(), InferenceTimeMs: 0));
        public Task<SamResponse> SegmentSamAsync(SamRequest r, CancellationToken ct = default)
            => Task.FromResult(new SamResponse(Masks: new List<SamMaskResult>(), ImageWidth: 1920, ImageHeight: 1080, InferenceTimeMs: 0));
        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest r, CancellationToken ct = default)
            => Task.FromResult(new YoloClassifyResponse(
                Predictions: new List<YoloClassifyPrediction> { new("BCD", 0.95) },
                InferenceTimeMs: 0, Usable: true));
    }

    private static async IAsyncEnumerable<VideoFrame> Frames(int n)
    {
        for (int i = 0; i < n; i++)
            yield return new VideoFrame(PngBytes: new byte[] { 1, 2, 3 }, TimestampSeconds: 1.0 + i * 0.5);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DinoEmpty_WithConfirmedBcd_ProducesStructuralFinding()
    {
        var cfg = PipelineConfig.Defaults();
        var svc = new MultiModelAnalysisService(
            new StubClient(), cfg, ffmpegPath: "ffmpeg", qwenVision: null,
            logger: null,
            frameSource: (_, _, dur, ct) => Frames(5)) // genug Frames fuer Voting-Bestaetigung
        { ClassifierOnlyStructuralEnabled = true };

        // GetVideoDurationAsync nutzt ffprobe -> im Test ueber EstimatedReachLengthM/Frames umgangen?
        // (Falls AnalyzeAsync die Dauer ueber den Frame-Stream nicht braucht: Stub liefert Dauer via Probe-Seam.)
        var result = await svc.AnalyzeAsync("dummy.mp4");

        Assert.Contains(result.Detections, d => d.VsaCode == "BCD" || d.FindingLabel == "Rohranfang");
    }

    [Fact]
    public async Task DinoEmpty_FlagOff_ProducesNoStructuralFinding()
    {
        var cfg = PipelineConfig.Defaults();
        var svc = new MultiModelAnalysisService(
            new StubClient(), cfg, ffmpegPath: "ffmpeg", qwenVision: null, logger: null,
            frameSource: (_, _, dur, ct) => Frames(5))
        { ClassifierOnlyStructuralEnabled = false };

        var result = await svc.AnalyzeAsync("dummy.mp4");
        Assert.DoesNotContain(result.Detections, d => d.VsaCode == "BCD");
    }
}
```
**Implementierungsnotiz:** `AnalyzeAsync` ruft am Anfang `File.Exists(videoPath)` und `GetVideoDurationAsync` (ffprobe). Für den Test zwei kleine Anpassungen nötig: (a) `File.Exists`-Guard nur greifen lassen, wenn kein Frame-Source-Override gesetzt ist, ODER (b) zusätzlichen optionalen Seam `Func<..., Task<double>>? durationProbe` analog `frameSource` (Default = `GetVideoDurationAsync`). Empfehlung: (b) — denselben Seam-Mechanismus nutzen. Test setzt `durationProbe: (_, _) => Task.FromResult(50.0)` und der `File.Exists`-Guard wird übersprungen, wenn `frameSource` injiziert ist. Konkrete Felder/Records (`YoloResponse`, `DinoResponse`, `SamResponse`, `VideoFrame`, `RawVideoDetection.VsaCode/FindingLabel`) vor dem Schreiben in `VisionPipelineDtos.cs` / `VideoFrameStream.cs` / dem Detection-Record verifizieren und Konstruktor-Argumente exakt angleichen.

- [ ] **Step 8: Test fehlschlagen lassen, dann Seam-Anpassungen umsetzen**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~MultiModelAnalysisServiceTests"`
Expected: zuerst FAIL (Seam/Guard). `durationProbe`-Seam + `File.Exists`-Skip-bei-Override umsetzen, Record-Konstruktoren angleichen.

- [ ] **Step 9: Test grün + volle Pipeline-Suite**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~Pipeline.Tests"`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/IVisionPipelineClient.cs src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs tests/AuswertungPro.Next.Pipeline.Tests/MultiModelAnalysisServiceTests.cs
git commit -m "test: IVisionPipelineClient-Seam + End-to-End-Test fuer box-losen Grundgeruest-Befund (Audit Fix #2)"
```

---

## Task 5: Fix #3 — KB-Gold-Metadaten persistieren

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseContext.cs` (EnsureSchema + Migration)
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs` (UpsertSample)
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/RetrievalService.cs` (Lese-Mapping in `LoadAllEmbeddingsWithSamples`/`SampleRecord`)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseGoldMetadataTests.cs`

**Interfaces:**
- Consumes: `TrainingSample.HumanConfirmed (bool?)`, `.Corrected (bool?)`, `.ConfirmedByUser (string?)`, `.ConfirmedAtUtc (DateTime?)`.
- Produces: vier neue Spalten in Tabelle `Samples`; `UpsertSample` schreibt sie; Lesepfad mappt sie auf `SampleRecord`.

- [ ] **Step 1: Failing test (gegen echte Temp-DB)**

`tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseGoldMetadataTests.cs`:
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public class KnowledgeBaseGoldMetadataTests
{
    [Fact]
    public async Task UpsertSample_PersistsGoldMetadata()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kbgold_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var ctx = new KnowledgeBaseContext(Path.Combine(dir, "kb.db"));
            // direkter DB-Lesetest ueber den Context: Spalten existieren + Werte persistiert
            // (Embedding wird hier nicht gebraucht -> UpsertSample separat testbar machen, s. Step 3)
            var sample = new TrainingSample
            {
                SampleId = "s1", CaseId = "865-864", Code = "BAB",
                Beschreibung = "Riss laengs", MeterStart = 12.0, MeterEnd = 12.0,
                HumanConfirmed = true, Corrected = true,
                ConfirmedByUser = "pascal", ConfirmedAtUtc = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc)
            };
            ctx.UpsertSampleForTest(sample, "v1"); // Test-Hook, s. Step 3
            var read = ctx.ReadSampleForTest("s1");
            Assert.True(read!.HumanConfirmed);
            Assert.True(read.Corrected);
            Assert.Equal("pascal", read.ConfirmedByUser);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
```
*(Hinweis: Falls `KnowledgeBaseContext` keinen pfadbasierten Ctor / keine Test-Hooks hat, stattdessen über `KnowledgeBaseManager` mit Fake-Embedder testen — exakten Konstruktor in Step 2 prüfen und Test daran angleichen. Ziel-Assertion bleibt: Gold-Felder sind nach Upsert lesbar.)*

- [ ] **Step 2: Konstruktor/Reader prüfen**

Run:
```bash
grep -n "public KnowledgeBaseContext\|SampleRecord\|LoadAllEmbeddingsWithSamples\|class SampleRecord\|record SampleRecord" src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseContext.cs src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/RetrievalService.cs
```
Test aus Step 1 an die reale API angleichen (Ctor-Signatur, ob `SampleRecord` die Felder hat).

- [ ] **Step 3: Migration ergänzen**

In `KnowledgeBaseContext.EnsureSchema()` nach den bestehenden `MigrateAddColumn`-Aufrufen (~Zeile 87):
```csharp
        // Migration (Audit Fix #3): Gold-Fund-Metadaten persistieren, damit die KB
        // korrigiertes Gold von normalem Approved unterscheiden kann. Nullable -> alte
        // Zeilen bleiben "nie beurteilt".
        MigrateAddColumn("Samples", "HumanConfirmed", "INTEGER");
        MigrateAddColumn("Samples", "Corrected", "INTEGER");
        MigrateAddColumn("Samples", "ConfirmedByUser", "TEXT");
        MigrateAddColumn("Samples", "ConfirmedAtUtc", "TEXT");
```

- [ ] **Step 4: UpsertSample erweitern**

In `KnowledgeBaseManager.UpsertSample` (~Zeile 333-350) die INSERT-Spalten + Werte ergänzen:
```csharp
        ExecuteNonQuery("""
            INSERT OR REPLACE INTO Samples
                (SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                 IsStreck, FramePath, ExportedUtc, VersionId, SourceType, QualityGateLevel,
                 HumanConfirmed, Corrected, ConfirmedByUser, ConfirmedAtUtc)
            VALUES ($id, $caseId, $code, $desc, $ms, $me, $streck, $frame, $exp, $ver, $source, $qg,
                 $hc, $corr, $by, $at)
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
            ("$qg",     s.QualityGateLevel ?? ""),
            ("$hc",     (object?)(s.HumanConfirmed is bool hc ? (hc ? 1 : 0) : null) ?? DBNull.Value),
            ("$corr",   (object?)(s.Corrected is bool cr ? (cr ? 1 : 0) : null) ?? DBNull.Value),
            ("$by",     (object?)s.ConfirmedByUser ?? DBNull.Value),
            ("$at",     (object?)s.ConfirmedAtUtc?.ToString("O") ?? DBNull.Value));
```
*(`ExecuteNonQuery` nutzt `AddWithValue`; NULL muss als `DBNull.Value` übergeben werden.)*

- [ ] **Step 5: Lesepfad mappen**

In `RetrievalService.LoadAllEmbeddingsWithSamples` (SELECT + `SampleRecord`-Konstruktion): die 4 Spalten mit in den SELECT aufnehmen und auf `SampleRecord` mappen (Record um die Felder erweitern, falls für die spätere Unterscheidung benötigt). Exakte SELECT-Liste vor Ort lesen und ergänzen.

- [ ] **Step 6: Test fehlschlagen lassen → grün**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~KnowledgeBaseGoldMetadataTests"`
Expected: erst FAIL, nach Step 3-5 PASS.

- [ ] **Step 7: Volle KB-Test-Suite (Migration darf alte Tests nicht brechen)**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~KnowledgeBase"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/ tests/AuswertungPro.Next.Infrastructure.Tests/KnowledgeBaseGoldMetadataTests.cs
git commit -m "feat: KB persistiert Gold-Metadaten (HumanConfirmed/Corrected/ConfirmedBy/At) (Audit Fix #3)"
```

---

## Task 6: Fix #6a — RetrievalService Eval-Lesefilter (Defense-in-Depth)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/RetrievalService.cs` (Ctor eval-keys + Filter beim Cache-Aufbau)
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs` (eval-keys an RetrievalService durchreichen)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/RetrievalEvalFilterTests.cs`

**Interfaces:**
- Consumes: `EvalContaminationGuard.IsEvalHaltung(IReadOnlySet<string>, string?)`, `.IsEvalContaminated(IReadOnlySet<string>, string?)`; `SampleRecord` mit `CaseId`/`FramePath`.
- Produces: `RetrievalService`-Ctor mit optionalen `IReadOnlySet<string>? evalImageHashes, IReadOnlySet<string>? evalHaltungKeys`; Filter im Cache-Aufbau.

- [ ] **Step 1: Failing test**

`tests/AuswertungPro.Next.Infrastructure.Tests/RetrievalEvalFilterTests.cs`:
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public class RetrievalEvalFilterTests
{
    [Fact]
    public async Task Retrieve_ExcludesEvalHaltungSamples()
    {
        // KB mit zwei Samples: eines aus Eval-Haltung "865-864", eines normal.
        // RetrievalService mit evalHaltungKeys={"865-864"} darf das Eval-Sample NICHT liefern.
        // (Setup: echte Temp-DB via KnowledgeBaseManager + Fake-Embedder; exakte Helper s. bestehende KB-Tests.)
        // Assert: kein zurueckgegebenes Result hat CaseId der Eval-Haltung.
        await Task.CompletedTask;
        Assert.True(false, "Setup gemaess bestehender KnowledgeBaseManagerEvalGuardTests nachziehen");
    }
}
```
*(Setup-Muster aus `KnowledgeBaseManagerEvalGuardTests` übernehmen — denselben Fake-Embedder + Temp-DB. Der Platzhalter-Assert wird in Step 3 durch echtes Setup ersetzt; er existiert nur, damit Step 2 rot ist.)*

- [ ] **Step 2: Test rot**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~RetrievalEvalFilterTests"`
Expected: FAIL.

- [ ] **Step 3: RetrievalService-Ctor + Filter implementieren**

Ctor erweitern:
```csharp
public sealed class RetrievalService(
    KnowledgeBaseContext db,
    EmbeddingService embedder,
    IReadOnlySet<string>? evalImageHashes = null,
    IReadOnlySet<string>? evalHaltungKeys = null) : IRetrievalService
{
```
Im Cache-Aufbau (`LoadAllEmbeddingsWithSamples`, dort wo `SampleRecord` pro Zeile entsteht) kontaminierte Samples ausschließen — zweite Verteidigungslinie identisch zum Schreib-Guard:
```csharp
        // Audit Fix #6: Defense-in-Depth — Eval-kontaminierte Samples nie als Few-Shot
        // liefern, auch wenn sie (historisch/aus Alt-DB) in der Tabelle stehen.
        if ((evalImageHashes is { Count: > 0 } && EvalContaminationGuard.IsEvalContaminated(evalImageHashes, sample.FramePath))
            || (evalHaltungKeys is { Count: > 0 } && EvalContaminationGuard.IsEvalHaltung(evalHaltungKeys, sample.CaseId)))
        {
            continue; // Zeile ueberspringen, nicht in den Cache aufnehmen
        }
```
`using AuswertungPro.Next.Infrastructure.Ai;` für `EvalContaminationGuard` ergänzen, falls nötig. Den Test aus Step 1 mit echtem Setup füllen + den Platzhalter-Assert entfernen.

- [ ] **Step 4: ServiceProvider verdrahten**

In `ServiceProvider.cs` an der Stelle, wo `new RetrievalService(kbCtx, embedder)` erzeugt wird (~Zeile 143): dieselben Eval-Keys übergeben, die schon der `KnowledgeBaseManager` bekommt. Quelle der Keys lokalisieren:
```bash
grep -rn "new KnowledgeBaseManager(\|evalImageHashes\|evalHaltungKeys\|LoadEval" src/AuswertungPro.Next.UI/ServiceProvider.cs
```
Die vorhandene Lade-Logik wiederverwenden und an `new RetrievalService(kbCtx, embedder, evalHashes, evalHaltungen)` durchreichen.

- [ ] **Step 5: Test grün**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~RetrievalEvalFilterTests"`
Expected: PASS. Zusätzlich Gegenprobe (Nicht-Eval-Sample wird weiterhin geliefert) im selben Test absichern.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/RetrievalService.cs src/AuswertungPro.Next.UI/ServiceProvider.cs tests/AuswertungPro.Next.Infrastructure.Tests/RetrievalEvalFilterTests.cs
git commit -m "feat: Eval-Lesefilter im RetrievalService (Defense-in-Depth) (Audit Fix #6a)"
```

---

## Task 7: Fix #6b — FeedbackIngestionService: Eval-Bypass + degenerierte Samples schließen

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/FeedbackIngestionService.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/FeedbackIngestionEvalTests.cs`

**Interfaces:**
- Consumes: `ITrainingSampleIndexer.IsPermanentlySkipped(TrainingSample)` — falls auf dem Interface vorhanden; sonst Typprüfung auf `KnowledgeBaseManager`.
- Produces: kein neues öffentliches API; verhärtetes Verhalten + Logging.

- [ ] **Step 1: Interface prüfen**

Run:
```bash
grep -n "interface ITrainingSampleIndexer\|IsPermanentlySkipped\|IsEvalContaminated\|IndexSampleAsync" src/AuswertungPro.Next.Application/Ai/Training/*.cs
```
Feststellen, ob `ITrainingSampleIndexer` eine Eval-/Eligibility-Prüfung exponiert. Wenn nein → in Step 3 `IsPermanentlySkipped` zum Interface hinzufügen (Implementierung existiert bereits auf `KnowledgeBaseManager`).

- [ ] **Step 2: Failing test**

`tests/AuswertungPro.Next.Infrastructure.Tests/FeedbackIngestionEvalTests.cs`:
```csharp
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public class FeedbackIngestionEvalTests
{
    private sealed class RecordingIndexer : ITrainingSampleIndexer
    {
        public TrainingSample? Indexed;
        public bool IsPermanentlySkipped(TrainingSample s) => false; // s. Step 1, falls Interface erweitert
        public Task<bool> IndexSampleAsync(TrainingSample s, CancellationToken ct = default)
        { Indexed = s; return Task.FromResult(true); }
    }

    [Fact]
    public async Task Accept_DegenerateSampleWithoutHaltungIdentity_IsNotIndexed()
    {
        var indexer = new RecordingIndexer();
        var svc = new FeedbackIngestionService(/* ValidationLogger */ TestDoubles.Logger(),
            /* WeightLearningService */ TestDoubles.WeightLearner(), indexer);

        var entry = TestDoubles.MappedEntry(findingLabel: "Riss", suggested: "BAB"); // keine Haltung, kein FramePath
        await svc.ProcessFeedbackAsync(entry, finalCode: "BAB", accepted: true);

        Assert.Null(indexer.Indexed); // degeneriertes Sample (CaseId=Label, kein Frame) wird NICHT indexiert
    }
}
```
*(`TestDoubles` = kleine Helfer für `ValidationLogger`/`WeightLearningService`/`MappedProtocolEntry`. Exakte Konstruktoren von `MappedProtocolEntry`/`RawVideoDetection` vor Ort prüfen und Helfer angleichen. Falls `ValidationLogger` schwer zu instanziieren ist, minimalen Fake/echten mit Temp-Pfad nutzen.)*

- [ ] **Step 3: Implementierung — Guard + Logging statt stillem Schlucken**

In `FeedbackIngestionService.ProcessFeedbackAsync` den Accept-Index-Block (~Zeile 50-70) härten:
```csharp
        if (accepted && _sampleIndexer is not null && !string.IsNullOrWhiteSpace(vsaCode))
        {
            var det = entry.Detection;
            var sample = new TrainingSample
            {
                SampleId = $"feedback_{Guid.NewGuid():N}",
                CaseId = det.FindingLabel ?? "",
                Code = vsaCode,
                Beschreibung = det.FindingLabel ?? "",
                MeterStart = det.MeterStart,
                MeterEnd = det.MeterEnd
            };

            // Audit Fix #6b: Eval-Bypass + degenerierte Samples schliessen.
            // Ohne echten Frame-/Haltungsbezug kann der Eval-Guard nicht greifen UND das
            // Sample ist ein nahezu inhaltsleeres Text-Embedding -> gar nicht indexieren.
            if (_sampleIndexer.IsPermanentlySkipped(sample))
            {
                // bewusst uebersprungen (Eval-kontaminiert oder nicht index-wuerdig)
            }
            else
            {
                try
                {
                    await _sampleIndexer.IndexSampleAsync(sample, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // war zuvor stilles catch{} — jetzt sichtbar
                    System.Diagnostics.Debug.WriteLine($"[FeedbackIngestion] Indexierung fehlgeschlagen: {ex.Message}");
                }
            }
        }
```
Falls `ITrainingSampleIndexer` `IsPermanentlySkipped` noch nicht hat: Methode zum Interface hinzufügen (Signatur `bool IsPermanentlySkipped(TrainingSample sample)`) — `KnowledgeBaseManager` implementiert sie bereits.

**Hinweis Reject-Pfad:** Der Klassenkommentar (Zeile 15) verspricht „On Reject logs as hard-negative". Das ist NICHT implementiert. In Scope hier nur Konsistenz: Kommentar auf Ist-Zustand korrigieren („Reject wird in ValidationLog protokolliert; kein Hard-Negative-Lernen") — kein neues Lernverhalten ohne separate Diskussion.

- [ ] **Step 4: Test grün**

Run: `dotnet test AuswertungPro.sln --filter "FullyQualifiedName~FeedbackIngestionEvalTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/FeedbackIngestionService.cs src/AuswertungPro.Next.Application/Ai/Training/ tests/AuswertungPro.Next.Infrastructure.Tests/FeedbackIngestionEvalTests.cs
git commit -m "fix: FeedbackIngestion indexiert keine eval-/degenerierten Samples mehr + Logging (Audit Fix #6b)"
```

---

## Task 8: Fix #5 — Sidecar VRAM-Eviction bei OOM + cls-Sichtbarkeit

**Files:**
- Modify: `sidecar/sidecar/main.py` (OOM-Handler)
- Modify: `sidecar/sidecar/gpu_manager.py` (optional: cls-Sichtbarkeit, falls cls dort registrierbar)
- Test: `sidecar/tests/test_gpu_manager_evict.py`

**Interfaces:**
- Consumes: `gpu_manager.evict_lru()`, `gpu_manager.empty_cache()` (existieren bereits).
- Produces: OOM-Pfad ruft `evict_lru()`; `get_status()` enthält cls-Slot-Info.

- [ ] **Step 1: Failing test**

`sidecar/tests/test_gpu_manager_evict.py`:
```python
from sidecar.gpu_manager import GpuModelManager, ModelSlot

def test_evict_lru_unloads_least_recently_used():
    mgr = GpuModelManager()
    mgr.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo_model", None))
    mgr.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino_model", None))
    # YOLO ist aelter (zuerst geladen) -> sollte zuerst evicted werden
    victim = mgr.evict_lru()
    assert victim == ModelSlot.YOLO
    assert mgr.get_status()["loaded_models"].get("yolo") is None
```

- [ ] **Step 2: Test laufen lassen**

Run:
```bash
cd "c:/Sewer-Studio_KI_4.4/sidecar"
python -m pytest tests/test_gpu_manager_evict.py -v -m "not gpu and not e2e"
```
Expected: PASS, falls `evict_lru` schon korrekt — dieser Test sichert nur das bestehende Verhalten ab (Regressionsnetz). Falls FAIL (z.B. `last_used`-Auflösung), in `gpu_manager.py` korrigieren.

- [ ] **Step 3: OOM-Handler reaktiv entlasten**

In `sidecar/sidecar/main.py` `handle_unexpected`, OOM-Zweig (~Zeile 77-79):
```python
    if _looks_like_oom(exc):
        # Audit Fix #5: bei OOM zuerst den am laengsten ungenutzten Slot entladen,
        # damit der naechste Frame wieder Platz hat (statt nur Cache leeren).
        gpu_manager.evict_lru()
        gpu_manager.empty_cache()
        return JSONResponse({"detail": "GPU out of memory"}, status_code=503)
```

- [ ] **Step 4: cls-Slot-Sichtbarkeit (Monitoring)**

Run:
```bash
grep -n "get_status\|loaded_models\|_cls\|classifier" sidecar/sidecar/gpu_manager.py sidecar/sidecar/models/yolo_wrapper.py | head
```
Wenn der cls-Slot außerhalb des `GpuModelManager` lebt (eigenes Singleton in `yolo_wrapper.py`): in `get_status()` (oder im `/health`-Aufbau) ein Feld `classifier_loaded: bool` ergänzen, das den cls-Ladezustand spiegelt. Reines Reporting, kein Verhaltenseingriff.

- [ ] **Step 5: Sidecar-Tests grün**

Run:
```bash
cd "c:/Sewer-Studio_KI_4.4/sidecar"
python -m pytest tests/ -v -m "not gpu and not e2e"
```
Expected: PASS (inkl. bestehender test_honesty/test_sam).

- [ ] **Step 6: Commit**

```bash
git add sidecar/sidecar/main.py sidecar/sidecar/gpu_manager.py sidecar/tests/test_gpu_manager_evict.py
git commit -m "fix: Sidecar entlaedt bei OOM per evict_lru + cls-VRAM-Sichtbarkeit (Audit Fix #5)"
```

---

## Task 9: Gesamtabnahme

- [ ] **Step 1: Voller Build**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Errors.

- [ ] **Step 2: Volle C#-Testsuite**

Run: `dotnet test AuswertungPro.sln`
Expected: alle grün (Baseline lt. Memory ~1094 Tests + neue).

- [ ] **Step 3: Sidecar-Tests**

Run: `cd "c:/Sewer-Studio_KI_4.4/sidecar"; python -m pytest tests/ -m "not gpu and not e2e"`
Expected: grün.

- [ ] **Step 4: Spec abhaken**

Spec-Datei prüfen: jeder Fix #1–#6 hat einen umgesetzten Task. Abweichungen (z.B. Policy in Infrastructure statt Application; Flags als Service-Property statt PipelineConfig) im Spec-Dokument als „Umsetzungsabweichung" notieren.

---

## Self-Review (gegen Spec)

**Spec-Abdeckung:**
- Fix #1 → Task 2 (Policy) + Task 3 (Verdrahtung). ✔
- Fix #2 → Task 4 (Seam + E2E-Test). ✔ (volle End-to-End-Abdeckung wie vom User gefordert)
- Fix #3 → Task 5. ✔
- Fix #4 → Task 1. ✔
- Fix #5 → Task 8. ✔
- Fix #6 → Task 6 (Lesefilter) + Task 7 (Schreib-Guard). ✔

**Umsetzungsabweichungen ggü. Spec (bewusst):**
1. `ClassifierOnlyStructuralPolicy` liegt in **Infrastructure** statt Application (Layering: nutzt `VsaCodeResolver`/`YoloClassifyPrediction`, beide Infrastructure). Tests in Pipeline.Tests (referenziert Infrastructure).
2. Fix-#1-Flags als **Env-Property am Service** (Muster `ClassifierDecisionEnabled`) statt `PipelineConfig`/Factory — weniger Fläche. Env: `SEWERSTUDIO_CLASSIFIER_ONLY_STRUCTURAL_OFF` (negiert → Default AN).

**Typkonsistenz:** `TryResolve(...)` (Task 2) ↔ Aufruf (Task 3) identische Signatur; `IVisionPipelineClient`-Methoden (Task 4) = bestehende `VisionPipelineClient`-Signaturen; KB-Spaltennamen (Task 5) = `TrainingSample`-Property-Namen.

**Offene Verifikationspunkte für den Implementierer (vor Ort lesen, dann exakt angleichen):**
- Record-Konstruktoren `YoloResponse`/`DinoResponse`/`SamResponse`/`VideoFrame`/`RawVideoDetection` (Task 4-Stub).
- `KnowledgeBaseContext`-Ctor + `SampleRecord`-Felder + `LoadAllEmbeddingsWithSamples`-SELECT (Task 5/6).
- `ITrainingSampleIndexer`-Oberfläche (Task 7, ggf. `IsPermanentlySkipped` ergänzen).
- Eval-Key-Ladequelle in `ServiceProvider` (Task 6).
