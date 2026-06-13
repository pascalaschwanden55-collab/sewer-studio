# WinCan-Style AI Overlays Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make SewerStudio AI overlays readable like the WinCan reference video without hiding real severe findings.

**Architecture:** Keep inference unchanged. Add one shared UI policy that separates non-protocol background masks from real finding candidates, then render finding candidates outline-first. A large mask is not automatically hidden; large real findings stay visible as contour-only, while background masks such as `water wall` are suppressed before rendering and before event creation.

**Tech Stack:** C#/.NET 10, WPF Canvas/Path rendering, xUnit UI tests, existing `SamMaskRenderer`, `SegmentedFinding`, `PlayerWindow.Coding.cs`.

---

## Preflight Evidence

Real sidecar check against `.tmp\ai-check\sewerstudio_ai_check_frame.png`:

- DINO labels: `root ball seal:42%`, `water wall:32%`, `root ball seal:26%`, `incrustation infiltration:26%`
- SAM masks:
  - `root ball seal`, SAM `96%`, area `6.4%`
  - `water wall`, SAM `98%`, area `95.0%`
  - `root ball seal`, SAM `96%`, area `6.4%`
  - `incrustation infiltration`, SAM `86%`, area `3.9%`

Conclusion:

- `water wall` is confirmed as a real background label and must be suppressed.
- SAM confidence is not the same as DINO/finding confidence; render decisions must account for DINO confidence when available.
- Size alone must not hide defects. Large non-background findings are rendered as outline-only.

---

## File Structure

- Modify: `src/AuswertungPro.Next.UI/Ai/Pipeline/SamMaskRenderer.cs`
  - Add render options, render policy, `MaskRenderCandidate`, render summary.
  - Add null-safe bbox checks.
  - Render large finding candidates as outline-only, not hidden.
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`
  - Build render candidates from `SegmentedFinding`.
  - Suppress background candidates before render and before event creation.
  - Keep every protocol candidate visible with at least a contour.
  - Show short status if masks were suppressed.
- Modify: `tests/AuswertungPro.Next.UI.Tests/SamMaskRendererTests.cs`
  - Add policy and render tests.
- Modify: `tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerCodingSidePanelTests.cs`
  - Add source-level wiring tests.

No sidecar/model/training/eval data changes.

---

### Task 1: Render Policy That Preserves Large Findings

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Ai/Pipeline/SamMaskRenderer.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/SamMaskRendererTests.cs`

- [ ] **Step 1: Write failing policy tests**

Add these helpers and tests to `SamMaskRendererTests.cs` before `CapturingLogger`:

```csharp
[Fact]
public void DecideVisualMode_HidesConfirmedBackgroundWaterWall()
{
    var candidate = Candidate("water wall", samConfidence: 0.98, dinoConfidence: 0.32, areaRatio: 0.95);

    var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

    Assert.Equal(SamMaskRenderer.MaskVisualMode.Hidden, decision.Mode);
    Assert.Equal("background_label", decision.Reason);
}

[Fact]
public void DecideVisualMode_KeepsLargeDefectAsOutline()
{
    var candidate = Candidate("incrustation infiltration", samConfidence: 0.86, dinoConfidence: 0.26, areaRatio: 0.55);

    var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

    Assert.Equal(SamMaskRenderer.MaskVisualMode.OutlineOnly, decision.Mode);
    Assert.Equal("large_finding_outline", decision.Reason);
}

[Fact]
public void DecideVisualMode_KeepsDinoThresholdFindingVisible()
{
    var candidate = Candidate("root ball seal", samConfidence: 0.96, dinoConfidence: 0.26, areaRatio: 0.064);

    var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

    Assert.NotEqual(SamMaskRenderer.MaskVisualMode.Hidden, decision.Mode);
}

[Fact]
public void DecideVisualMode_UsesSubtleFillForSmallHighConfidenceDefect()
{
    var candidate = Candidate("root ball seal", samConfidence: 0.96, dinoConfidence: 0.72, areaRatio: 0.064);

    var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

    Assert.Equal(SamMaskRenderer.MaskVisualMode.SubtleFill, decision.Mode);
}

[Fact]
public void DecideVisualMode_IsNullSafeForMissingBbox()
{
    var mask = new SamMaskResult(
        Label: "root",
        Confidence: 0.8,
        Bbox: null!,
        MaskRle: "1,1,9999",
        MaskAreaPixels: 100,
        ImageAreaPixels: 10_000,
        HeightPixels: 10,
        WidthPixels: 10,
        CentroidX: 10,
        CentroidY: 10);
    var candidate = new SamMaskRenderer.MaskRenderCandidate(mask, Quant("root", 0.8), DetectionConfidence: 0.3);

    var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

    Assert.NotEqual(SamMaskRenderer.MaskVisualMode.Hidden, decision.Mode);
}

private static SamMaskRenderer.MaskRenderCandidate Candidate(
    string label,
    double samConfidence,
    double? dinoConfidence,
    double areaRatio)
{
    var imageArea = 10_000;
    var maskArea = (int)Math.Round(imageArea * areaRatio);
    var mask = new SamMaskResult(
        Label: label,
        Confidence: samConfidence,
        Bbox: [10, 10, 40, 40],
        MaskRle: "1,1,9999",
        MaskAreaPixels: maskArea,
        ImageAreaPixels: imageArea,
        HeightPixels: 30,
        WidthPixels: 30,
        CentroidX: 25,
        CentroidY: 25);
    return new SamMaskRenderer.MaskRenderCandidate(mask, Quant(label, samConfidence), dinoConfidence);
}

private static MaskQuantificationService.QuantifiedMask Quant(string label, double confidence)
    => new(label, confidence, null, null, null, null, null, null);
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "DecideVisualMode" -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-policy-red\
```

Expected: compile fails because the policy types do not exist.

- [ ] **Step 3: Add policy types and decision logic**

Add this block inside `SamMaskRenderer` after `LabelTag`:

```csharp
public enum MaskVisualMode
{
    Hidden,
    OutlineOnly,
    SubtleFill
}

public sealed record RenderOptions(
    double LargeFindingOutlineAreaRatio,
    double MinimumVisibleDetectionConfidence,
    double MinimumVisibleSamConfidence,
    double MinimumFillDetectionConfidence,
    byte FillAlpha,
    byte StrokeAlpha,
    IReadOnlySet<string> HiddenLabelTokens)
{
    public static RenderOptions WinCanStyle { get; } = new(
        LargeFindingOutlineAreaRatio: 0.30,
        MinimumVisibleDetectionConfidence: 0.25,
        MinimumVisibleSamConfidence: 0.25,
        MinimumFillDetectionConfidence: 0.60,
        FillAlpha: 24,
        StrokeAlpha: 230,
        HiddenLabelTokens: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "water wall",
            "structure water wall",
            "pipe wall",
            "black border",
            "osd"
        });
}

public sealed record MaskRenderCandidate(
    SamMaskResult Mask,
    MaskQuantificationService.QuantifiedMask? Quant,
    double? DetectionConfidence = null);

public sealed record RenderDecision(MaskVisualMode Mode, string? Reason);

public static RenderOptions WinCanStyleOptions => RenderOptions.WinCanStyle;
```

Add this method before `RenderMasks`:

```csharp
public static RenderDecision DecideVisualMode(MaskRenderCandidate candidate, RenderOptions? options = null)
{
    options ??= WinCanStyleOptions;

    var mask = candidate.Mask;
    var label = NormalizeLabel(mask.Label ?? candidate.Quant?.Label ?? "");
    if (options.HiddenLabelTokens.Any(token => label.Contains(NormalizeLabel(token), StringComparison.Ordinal)))
        return new RenderDecision(MaskVisualMode.Hidden, "background_label");

    var detectionConfidence = candidate.DetectionConfidence;
    var samConfidence = Math.Max(mask.Confidence, candidate.Quant?.Confidence ?? 0);
    if ((detectionConfidence ?? samConfidence) < options.MinimumVisibleDetectionConfidence
        && samConfidence < options.MinimumVisibleSamConfidence)
        return new RenderDecision(MaskVisualMode.Hidden, "confidence_too_low");

    var areaRatio = GetAreaRatio(mask);
    if (areaRatio >= options.LargeFindingOutlineAreaRatio)
        return new RenderDecision(MaskVisualMode.OutlineOnly, "large_finding_outline");

    if ((detectionConfidence ?? samConfidence) >= options.MinimumFillDetectionConfidence)
        return new RenderDecision(MaskVisualMode.SubtleFill, null);

    return new RenderDecision(MaskVisualMode.OutlineOnly, null);
}

private static double GetAreaRatio(SamMaskResult mask)
{
    if (mask.ImageAreaPixels > 0 && mask.MaskAreaPixels >= 0)
        return mask.MaskAreaPixels / (double)mask.ImageAreaPixels;
    return 0;
}

private static string NormalizeLabel(string label)
{
    return label
        .Trim()
        .Replace('_', ' ')
        .Replace('-', ' ')
        .ToLowerInvariant();
}
```

Use null-safe bbox checks everywhere new code reads bbox:

```csharp
if (mask.Bbox is { Count: >= 4 })
{
    // use bbox
}
```

- [ ] **Step 4: Run tests to verify GREEN**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "DecideVisualMode" -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-policy-green\
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Ai/Pipeline/SamMaskRenderer.cs tests/AuswertungPro.Next.UI.Tests/SamMaskRendererTests.cs
git commit -m "KI-Overlay: sichere Render-Policy ergaenzen"
```

---

### Task 2: Render Candidates Outline-First

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Ai/Pipeline/SamMaskRenderer.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/SamMaskRendererTests.cs`

- [ ] **Step 1: Write failing render tests**

Add these tests to `SamMaskRendererTests.cs`:

```csharp
[Fact]
public void RenderCandidates_DoesNotDrawHiddenBackgroundMask()
{
    Exception? threadError = null;
    int childCount = -1;

    var thread = new Thread(() =>
    {
        try
        {
            var canvas = new Canvas();
            var summary = SamMaskRenderer.RenderCandidates(
                canvas,
                [Candidate("water wall", 0.98, 0.32, 0.95)],
                imageWidth: 100,
                imageHeight: 100,
                canvasWidth: 100,
                canvasHeight: 100,
                options: SamMaskRenderer.WinCanStyleOptions);

            childCount = canvas.Children.Count;
            Assert.Equal(1, summary.Hidden);
            Assert.Equal(0, summary.Rendered);
        }
        catch (Exception ex)
        {
            threadError = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    Assert.Null(threadError);
    Assert.Equal(0, childCount);
}

[Fact]
public void RenderCandidates_LargeDefectDrawsOutlineAndLabelOnly()
{
    Exception? threadError = null;
    int childCount = -1;

    var thread = new Thread(() =>
    {
        try
        {
            var canvas = new Canvas();
            var summary = SamMaskRenderer.RenderCandidates(
                canvas,
                [Candidate("incrustation infiltration", 0.86, 0.26, 0.55)],
                imageWidth: 100,
                imageHeight: 100,
                canvasWidth: 100,
                canvasHeight: 100,
                options: SamMaskRenderer.WinCanStyleOptions);

            childCount = canvas.Children.Count;
            Assert.Equal(1, summary.Rendered);
            Assert.Equal(1, summary.OutlineOnly);
            Assert.Equal(0, summary.SubtleFill);
        }
        catch (Exception ex)
        {
            threadError = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    Assert.Null(threadError);
    Assert.Equal(2, childCount); // contour path + label, no fill path
}
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "RenderCandidates_" -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-render-red\
```

Expected: compile fails because `RenderCandidates` and `RenderSummary` do not exist.

- [ ] **Step 3: Add render summary and candidate rendering**

Add this record after `RenderDecision`:

```csharp
public sealed record RenderSummary(
    int Rendered,
    int Hidden,
    int OutlineOnly,
    int SubtleFill,
    IReadOnlyDictionary<string, int> HiddenReasons);
```

Add this method before existing `RenderMasks`:

```csharp
public static RenderSummary RenderCandidates(
    Canvas canvas,
    IReadOnlyList<MaskRenderCandidate> candidates,
    int imageWidth,
    int imageHeight,
    double canvasWidth,
    double canvasHeight,
    ILogger? logger = null,
    RenderOptions? options = null)
{
    if (candidates.Count == 0)
        return new RenderSummary(0, 0, 0, 0, new Dictionary<string, int>());

    int rendered = 0, hidden = 0, outlineOnly = 0, subtleFill = 0;
    var hiddenReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    options ??= WinCanStyleOptions;

    for (var i = 0; i < candidates.Count; i++)
    {
        try
        {
            var candidate = candidates[i];
            var decision = DecideVisualMode(candidate, options);
            if (decision.Mode == MaskVisualMode.Hidden)
            {
                hidden++;
                var reason = decision.Reason ?? "hidden";
                hiddenReasons[reason] = hiddenReasons.TryGetValue(reason, out var count) ? count + 1 : 1;
                continue;
            }

            RenderSingleMask(
                canvas,
                candidate.Mask,
                candidate.Quant,
                imageWidth,
                imageHeight,
                canvasWidth,
                canvasHeight,
                decision.Mode,
                options);
            rendered++;
            if (decision.Mode == MaskVisualMode.OutlineOnly)
                outlineOnly++;
            else
                subtleFill++;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "SamMaskRenderer: Maske {MaskIndex} uebersprungen.", i);
        }
    }

    return new RenderSummary(rendered, hidden, outlineOnly, subtleFill, hiddenReasons);
}
```

Keep the existing `RenderMasks` method as a compatibility wrapper:

```csharp
public static RenderSummary RenderMasks(
    Canvas canvas,
    SamResponse samResponse,
    IReadOnlyList<MaskQuantificationService.QuantifiedMask> quantified,
    double canvasWidth,
    double canvasHeight,
    ILogger? logger = null,
    RenderOptions? options = null)
{
    if (samResponse == null || samResponse.Masks.Count == 0)
        return new RenderSummary(0, 0, 0, 0, new Dictionary<string, int>());

    var candidates = samResponse.Masks
        .Select((mask, index) => new MaskRenderCandidate(
            mask,
            index < quantified.Count ? quantified[index] : null,
            DetectionConfidence: null))
        .ToList();

    return RenderCandidates(
        canvas,
        candidates,
        samResponse.ImageWidth,
        samResponse.ImageHeight,
        canvasWidth,
        canvasHeight,
        logger,
        options);
}
```

Change `RenderSingleMask` to accept `MaskVisualMode visualMode` and `RenderOptions options`. Only render fill when `visualMode == MaskVisualMode.SubtleFill`:

```csharp
if (visualMode == MaskVisualMode.SubtleFill)
{
    var fillGeom = ExtractFillGeometry(decoded, imgW, imgH, canvasWidth, canvasHeight);
    var fillPath = new Path
    {
        Data = fillGeom,
        Fill = new SolidColorBrush(Color.FromArgb(options.FillAlpha, 0, 255, 0)),
        Tag = MaskTag,
        IsHitTestVisible = false
    };
    canvas.Children.Add(fillPath);
}
```

Use `options.StrokeAlpha` for the contour:

```csharp
Stroke = new SolidColorBrush(Color.FromArgb(options.StrokeAlpha, 0, 255, 0)),
```

Only position labels when bbox is present:

```csharp
if (quant != null && mask.Bbox is { Count: >= 4 })
{
    double bboxX = mask.Bbox[0] / imgW * canvasWidth;
    double bboxY = mask.Bbox[1] / imgH * canvasHeight;
    RenderMaskLabel(canvas, quant, bboxX, Math.Max(0, bboxY - 40));
}
```

- [ ] **Step 4: Run render tests to verify GREEN**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "RenderCandidates_|RenderMasks_LogsSkippedMaskViaLogger" -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-render-green\
```

Expected: selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Ai/Pipeline/SamMaskRenderer.cs tests/AuswertungPro.Next.UI.Tests/SamMaskRendererTests.cs
git commit -m "KI-Overlay: Kandidaten konturbasiert rendern"
```

---

### Task 3: Apply Same Policy To Player Rendering And Event Creation

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerCodingSidePanelTests.cs`

- [ ] **Step 1: Write failing player wiring test**

Add this test to `DesignAuditPlayerCodingSidePanelTests.cs`:

```csharp
[Fact]
public void Player_uses_same_overlay_policy_for_rendering_and_events()
{
    var coding = ReadUiFile("Views", "Windows", "PlayerWindow.Coding.cs");

    Assert.Contains("BuildVisibleCodingFindings", coding);
    Assert.Contains("SamMaskRenderer.RenderCandidates", coding);
    Assert.Contains("visibleCodierbar", coding);
    Assert.DoesNotContain("AddMultiModelFindingsAsEvents(\r\n                    segmented.Where(s => s.Proximity.IsCodierbar).ToList()", coding);
}
```

- [ ] **Step 2: Run test to verify RED**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter Player_uses_same_overlay_policy_for_rendering_and_events -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-player-red\
```

Expected: fails because player still passes all codierbar findings directly.

- [ ] **Step 3: Add visible finding helper**

In `PlayerWindow.Coding.cs`, add this helper near `ShowMultiModelResults`:

```csharp
private static IReadOnlyList<SegmentedFinding> BuildVisibleCodingFindings(
    IReadOnlyList<SegmentedFinding> segmented)
{
    return segmented
        .Where(s => s.Proximity.IsCodierbar)
        .Where(s =>
        {
            var candidate = new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate(
                s.Mask,
                s.Quant,
                s.Dino?.Confidence);
            var decision = Ai.Pipeline.SamMaskRenderer.DecideVisualMode(
                candidate,
                Ai.Pipeline.SamMaskRenderer.WinCanStyleOptions);
            return decision.Mode != Ai.Pipeline.SamMaskRenderer.MaskVisualMode.Hidden;
        })
        .ToList();
}
```

This intentionally means: background masks are not shown and not written as protocol events. Real finding candidates are never hidden by size.

- [ ] **Step 4: Use helper for render and event creation**

In `RunCodingAnalysisAsync`, replace:

```csharp
AddMultiModelFindingsAsEvents(
    segmented.Where(s => s.Proximity.IsCodierbar).ToList(),
    mmResult.SamResponse?.ImageWidth ?? 1, mmResult.SamResponse?.ImageHeight ?? 1,
    mmResult.YoloMaxConfidence, captureTimestampSec);
```

with:

```csharp
var visibleCodierbar = BuildVisibleCodingFindings(segmented);
AddMultiModelFindingsAsEvents(
    visibleCodierbar,
    mmResult.SamResponse?.ImageWidth ?? 1, mmResult.SamResponse?.ImageHeight ?? 1,
    mmResult.YoloMaxConfidence, captureTimestampSec);
```

In `ShowMultiModelResults`, replace the current `RenderMasks` call with candidate rendering:

```csharp
var codierbar = segmented.Where(s => s.Proximity.IsCodierbar).ToList();
var visibleCodierbar = BuildVisibleCodingFindings(segmented);
if (visibleCodierbar.Count > 0)
{
    var candidates = visibleCodierbar
        .Select(s => new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate(
            s.Mask,
            s.Quant,
            s.Dino?.Confidence))
        .ToList();

    Ai.Pipeline.SamMaskRenderer.RenderCandidates(
        CodingOverlayCanvas,
        candidates,
        mmResult.SamResponse.ImageWidth,
        mmResult.SamResponse.ImageHeight,
        CodingOverlayCanvas.ActualWidth,
        CodingOverlayCanvas.ActualHeight,
        logger: _serviceProvider?.LoggerFactory.CreateLogger("SamMaskRenderer"),
        options: Ai.Pipeline.SamMaskRenderer.WinCanStyleOptions);
}
```

- [ ] **Step 5: Run player wiring test to verify GREEN**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter Player_uses_same_overlay_policy_for_rendering_and_events -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-player-green\
```

Expected: pass.

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerCodingSidePanelTests.cs
git commit -m "KI-Overlay: Hintergrundmasken nicht als Befund codieren"
```

---

### Task 4: Status Summary For Suppressed Background Masks

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerCodingSidePanelTests.cs`

- [ ] **Step 1: Write failing status test**

Add this test to `DesignAuditPlayerCodingSidePanelTests.cs`:

```csharp
[Fact]
public void Player_status_mentions_background_masks_suppressed()
{
    var coding = ReadUiFile("Views", "Windows", "PlayerWindow.Coding.cs");

    Assert.Contains("Hintergrundmasken ausgeblendet", coding);
    Assert.Contains("BuildOverlaySuppressionText", coding);
}
```

- [ ] **Step 2: Run test to verify RED**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter Player_status_mentions_background_masks_suppressed -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-status-red\
```

Expected: fails because no helper exists.

- [ ] **Step 3: Add status helper**

Add near `ShowMultiModelResults`:

```csharp
private static string BuildOverlaySuppressionText(int suppressedBackgroundCount)
{
    if (suppressedBackgroundCount <= 0)
        return "";

    return suppressedBackgroundCount == 1
        ? "1 Hintergrundmaske ausgeblendet"
        : $"{suppressedBackgroundCount} Hintergrundmasken ausgeblendet";
}
```

In `RunCodingAnalysisAsync`, after `segmented` is built:

```csharp
var visibleCodierbar = BuildVisibleCodingFindings(segmented);
var suppressedBackgroundCount = segmented.Count(s => s.Proximity.IsCodierbar) - visibleCodierbar.Count;
var overlaySuppressionText = BuildOverlaySuppressionText(suppressedBackgroundCount);
```

Use `visibleCodierbar` for `AddMultiModelFindingsAsEvents`.

Append status detail only when non-empty:

```csharp
var timingText = $"YOLO {mmResult.YoloTimeMs:F0}ms | DINO {mmResult.DinoTimeMs:F0}ms | SAM {mmResult.SamTimeMs:F0}ms";
if (!string.IsNullOrEmpty(overlaySuppressionText))
    timingText += $" | {overlaySuppressionText}";
```

Then pass `timingText` into `SetCodingAiState`.

- [ ] **Step 4: Run status test to verify GREEN**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter Player_status_mentions_background_masks_suppressed -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-status-green\
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs tests/AuswertungPro.Next.UI.Tests/DesignAuditPlayerCodingSidePanelTests.cs
git commit -m "KI-Overlay: ausgeblendete Hintergrundmasken melden"
```

---

### Task 5: Full Verification And Visual QA

**Files:**
- No required source changes.

- [ ] **Step 1: Run UI build**

```powershell
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -v minimal -p:UseAppHost=false -o .tmp\wincan-overlay-build
```

Expected: `0 Fehler`.

- [ ] **Step 2: Run UI tests**

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -v minimal -p:UseAppHost=false -p:BaseOutputPath=.tmp\wincan-overlay-ui-tests\
```

Expected: all UI tests pass.

- [ ] **Step 3: Run pipeline tests**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj -v minimal
```

Expected: all pipeline tests pass.

- [ ] **Step 4: Manual visual QA on user video**

Open the same test video in the player and verify:

- `water wall` / `structure water wall` no longer creates a full green overlay.
- Large real findings remain visible as contour-only.
- Every finding that is written to the protocol has at least a visible contour.
- Small high-confidence findings can still get subtle fill.
- Video remains readable with overlays enabled.
- Status mentions hidden background masks when applicable.

- [ ] **Step 5: Compare with WinCan reference**

Use the downloaded reference video only for visual comparison:

```powershell
ffmpeg -y -i .tmp\wincan-ai-video\Wincan_AI_Video_Paul_1.mp4 -vf fps=1/5 .tmp\wincan-ai-video\frame_%02d.jpg
```

Expected visual target:

- Thin contours.
- Small defect-focused masks.
- Labels with confidence.
- No full image tint.
- Pipe wall, water, black borders and OSD remain readable.

---

## Self-Review

Your four review points are covered:

- Large masks are not completely hidden anymore. Large non-background findings are outline-only.
- Display and protocol stay aligned. Background labels are suppressed before protocol creation; all protocol candidates get at least a contour.
- Label check is documented with real sidecar output: `water wall` was confirmed at 95 percent mask area.
- BBox access is null-safe via `mask.Bbox is { Count: >= 4 }`.

Known limits:

- No model retraining.
- No new user settings UI yet.
- Thresholds start conservative and should be tuned after visual QA on several videos.

