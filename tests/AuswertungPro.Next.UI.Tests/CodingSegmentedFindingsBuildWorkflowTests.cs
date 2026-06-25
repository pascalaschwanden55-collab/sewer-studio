using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSegmentedFindingsBuildWorkflowTests
{
    [Fact]
    public void Execute_returns_empty_without_sam_response()
    {
        var result = CodingSegmentedFindingsBuildWorkflow.Execute(
            new CodingSegmentedFindingsBuildRequest(
                Result: Result(samResponse: null),
                Calibration: null),
            NoActions());

        Assert.Equal(CodingSegmentedFindingsBuildWorkflowOutcome.NoSamResponse, result.Outcome);
        Assert.Empty(result.Segmented);
    }

    [Fact]
    public void Execute_resolves_calibration_and_delegates_to_segment_builder()
    {
        var samResponse = new SamResponse(
            Masks: [],
            ImageWidth: 640,
            ImageHeight: 480,
            InferenceTimeMs: 1);
        var dino = new[]
        {
            new DinoDetectionDto(1, 2, 3, 4, "crack", 0.9, "crack")
        };
        var quantified = new[]
        {
            new MaskQuantificationService.QuantifiedMask(
                Label: "crack",
                Confidence: 0.9,
                CrossSectionReductionPercent: 12,
                IntrusionPercent: 8,
                ExtentPercent: 50,
                HeightMm: 20,
                WidthMm: 4,
                ClockPosition: "3 Uhr")
        };
        IReadOnlyList<SegmentedFinding> built = [];
        var calibration = new PipeCalibration
        {
            PipeCenter = new NormalizedPoint(0.4, 0.6),
            NormalizedDiameter = 0.8
        };

        var result = CodingSegmentedFindingsBuildWorkflow.Execute(
            new CodingSegmentedFindingsBuildRequest(
                Result: Result(samResponse, dino, quantified),
                Calibration: calibration),
            new CodingSegmentedFindingsBuildActions(
                BuildSegmentedFindings: (sam, dinoDetections, quantifiedMasks, proximity) =>
                {
                    Assert.Same(samResponse, sam);
                    Assert.Same(dino, dinoDetections);
                    Assert.Same(quantified, quantifiedMasks);
                    Assert.Equal(0.4, proximity.VanishX);
                    Assert.Equal(0.6, proximity.VanishY);
                    Assert.Equal(0.4, proximity.PipeRadiusNorm);
                    return built;
                }));

        Assert.Equal(CodingSegmentedFindingsBuildWorkflowOutcome.Built, result.Outcome);
        Assert.Same(built, result.Segmented);
    }

    private static CodingSegmentedFindingsBuildActions NoActions()
        => new(
            BuildSegmentedFindings: (_, _, _, _) =>
                throw new InvalidOperationException("Segment builder should not run."));

    private static SingleFrameResult Result(
        SamResponse? samResponse,
        IReadOnlyList<DinoDetectionDto>? dinoDetections = null,
        IReadOnlyList<MaskQuantificationService.QuantifiedMask>? quantifiedMasks = null)
        => new(
            IsRelevant: true,
            DinoDetections: dinoDetections ?? [],
            SamResponse: samResponse,
            QuantifiedMasks: quantifiedMasks ?? [],
            YoloTimeMs: 0,
            DinoTimeMs: 0,
            SamTimeMs: 0,
            Error: null);
}
