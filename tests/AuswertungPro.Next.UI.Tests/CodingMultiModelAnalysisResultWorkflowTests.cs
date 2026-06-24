using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelAnalysisResultWorkflowTests
{
    [Fact]
    public void Execute_shows_error_without_building_segmented_findings()
    {
        var calls = new List<string>();

        var result = CodingMultiModelAnalysisResultWorkflow.Execute(
            new CodingMultiModelAnalysisResultWorkflowRequest(
                Result: SingleFrameResult.Empty("Sidecar down"),
                ActivityText: "Analysiere"),
            Actions(
                setAiState: (status, color, detail, pulse) =>
                {
                    calls.Add($"state:{status}|{detail}|{pulse}");
                    Assert.Equal(PlayerStatusColors.Error, color);
                },
                buildSegmentedFindings: _ => throw new InvalidOperationException("No segmentation should run.")));

        Assert.Equal(["state:Fehler: Sidecar down|Multi-Model|False"], calls);
        Assert.Equal(CodingMultiModelAnalysisResultWorkflowOutcome.Error, result.Outcome);
    }

    [Fact]
    public void Execute_clears_masks_when_no_damage_was_detected()
    {
        var calls = new List<string>();

        var result = CodingMultiModelAnalysisResultWorkflow.Execute(
            new CodingMultiModelAnalysisResultWorkflowRequest(
                Result: new SingleFrameResult(
                    IsRelevant: false,
                    DinoDetections: [],
                    SamResponse: null,
                    QuantifiedMasks: [],
                    YoloTimeMs: 12,
                    DinoTimeMs: 34,
                    SamTimeMs: 0,
                    Error: null),
                ActivityText: "Analysiere"),
            Actions(
                setAiState: (status, color, detail, pulse) =>
                {
                    calls.Add($"state:{status}|{detail}|{pulse}");
                    Assert.Equal(PlayerStatusColors.Success, color);
                },
                clearMasks: () => calls.Add("clear-masks"),
                buildSegmentedFindings: _ => throw new InvalidOperationException("No segmentation should run.")));

        Assert.Equal(["state:Kein Schaden erkannt|YOLO 12ms | 0 Detektionen|False", "clear-masks"], calls);
        Assert.Equal(CodingMultiModelAnalysisResultWorkflowOutcome.NoDamage, result.Outcome);
    }

    [Fact]
    public void Execute_shows_segmented_results_and_adds_visible_coding_findings()
    {
        var calls = new List<string>();
        var segmented = new[] { Finding("crack", MetrierungProximity.Codierbar) };
        var frame = ResultWithDetections();

        var result = CodingMultiModelAnalysisResultWorkflow.Execute(
            new CodingMultiModelAnalysisResultWorkflowRequest(
                Result: frame,
                ActivityText: "Analysiere"),
            Actions(
                setAiState: (status, color, detail, pulse) =>
                {
                    calls.Add($"state:{status}|{detail}|{pulse}");
                    if (pulse)
                        Assert.Equal(PlayerStatusColors.Warning, color);
                    else
                        Assert.Equal(PlayerStatusColors.Success, color);
                },
                buildSegmentedFindings: seen =>
                {
                    calls.Add("build");
                    Assert.Same(frame, seen);
                    return segmented;
                },
                showMultiModelResults: (seen, shown) =>
                {
                    calls.Add("show");
                    Assert.Same(frame, seen);
                    Assert.Same(segmented, shown);
                },
                addFindingsAsEvents: (findings, imageWidth, imageHeight, yoloMaxConfidence) =>
                {
                    calls.Add($"add:{imageWidth}x{imageHeight}|{yoloMaxConfidence:0.00}");
                    Assert.Same(segmented[0], Assert.Single(findings));
                }));

        Assert.Equal(
            [
                "state:Analysiere|Schritt 3 von 4: SAM-Masken (1 Befunde)|True",
                "build",
                "show",
                "state:1 Befunde erkannt|YOLO 11ms | DINO 22ms | SAM 33ms|False",
                "add:320x240|0.91"
            ],
            calls);
        Assert.Equal(CodingMultiModelAnalysisResultWorkflowOutcome.EventsAdded, result.Outcome);
        Assert.Equal(1, result.VisibleFindingCount);
    }

    private static CodingMultiModelAnalysisResultWorkflowActions Actions(
        Action<string, Color, string?, bool>? setAiState = null,
        Action? clearMasks = null,
        Func<SingleFrameResult, IReadOnlyList<SegmentedFinding>>? buildSegmentedFindings = null,
        Action<SingleFrameResult, IReadOnlyList<SegmentedFinding>>? showMultiModelResults = null,
        Action<IReadOnlyList<SegmentedFinding>, double, double, double?>? addFindingsAsEvents = null)
        => new(
            SetAiState: setAiState ?? ((_, _, _, _) => throw new InvalidOperationException("SetAiState should not run.")),
            ClearMasks: clearMasks ?? (() => throw new InvalidOperationException("ClearMasks should not run.")),
            BuildSegmentedFindings: buildSegmentedFindings ?? (_ => throw new InvalidOperationException("BuildSegmentedFindings should not run.")),
            ShowMultiModelResults: showMultiModelResults ?? ((_, _) => throw new InvalidOperationException("ShowMultiModelResults should not run.")),
            AddFindingsAsEvents: addFindingsAsEvents ?? ((_, _, _, _) => throw new InvalidOperationException("AddFindingsAsEvents should not run.")));

    private static SingleFrameResult ResultWithDetections()
        => new(
            IsRelevant: true,
            DinoDetections: [new DinoDetectionDto(0, 0, 100, 100, "crack", 0.8, "crack")],
            SamResponse: new SamResponse(
                Masks: [Mask("crack")],
                ImageWidth: 320,
                ImageHeight: 240,
                InferenceTimeMs: 33),
            QuantifiedMasks: [],
            YoloTimeMs: 11,
            DinoTimeMs: 22,
            SamTimeMs: 33,
            Error: null,
            YoloMaxConfidence: 0.91);

    private static SegmentedFinding Finding(string label, MetrierungProximity proximity)
    {
        var mask = Mask(label);
        var quant = new MaskQuantificationService.QuantifiedMask(
            label,
            0.9,
            null,
            null,
            null,
            null,
            null,
            null);
        var dino = new DinoDetectionDto(0, 0, 100, 100, label, 0.8, label);
        var proximityResult = new MetrierungProximityResult(proximity, "", 0, 0, 0, false, false);
        return new SegmentedFinding(dino, mask, quant, proximityResult);
    }

    private static SamMaskResult Mask(string label)
        => new(
            Label: label,
            Confidence: 0.9,
            Bbox: [0, 0, 100, 100],
            MaskRle: "0",
            MaskAreaPixels: 100,
            ImageAreaPixels: 10_000,
            HeightPixels: 100,
            WidthPixels: 100,
            CentroidX: 50,
            CentroidY: 50);
}
