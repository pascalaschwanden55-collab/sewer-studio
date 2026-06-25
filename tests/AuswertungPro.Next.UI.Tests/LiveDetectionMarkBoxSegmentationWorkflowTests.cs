using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionMarkBoxSegmentationWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_when_segmentation_inputs_are_missing()
    {
        var result = await LiveDetectionMarkBoxSegmentationWorkflow.ExecuteAsync(
            new LiveDetectionMarkBoxSegmentationRequest(
                HasBoxSegmentation: false,
                FrameBytes: [1, 2, 3],
                OverlayPointCount: 2),
            NoActions());

        Assert.Equal(LiveDetectionMarkBoxSegmentationOutcome.Skipped, result.Outcome);
        Assert.Null(result.Segmentation);
    }

    [Fact]
    public async Task ExecuteAsync_builds_box_segments_and_applies_quantification()
    {
        var calls = new List<string>();
        var frameBytes = new byte[] { 1, 2, 3 };
        var segmentation = Result();

        var result = await LiveDetectionMarkBoxSegmentationWorkflow.ExecuteAsync(
            new LiveDetectionMarkBoxSegmentationRequest(
                HasBoxSegmentation: true,
                FrameBytes: frameBytes,
                OverlayPointCount: 2),
            new LiveDetectionMarkBoxSegmentationActions(
                BuildBox: () =>
                {
                    calls.Add("box");
                    return new NormalizedBoundingBox { XCenter = 0.5, YCenter = 0.5, Width = 0.2, Height = 0.2 };
                },
                GetCalibration: () =>
                {
                    calls.Add("calibration");
                    return null;
                },
                SegmentBoxAsync: (actualFrame, box, dn, calibration) =>
                {
                    Assert.Same(frameBytes, actualFrame);
                    Assert.Equal(0.5, box.XCenter);
                    Assert.Equal(0, dn);
                    Assert.Null(calibration);
                    calls.Add("segment");
                    return Task.FromResult<BoxSegmentationResult?>(segmentation);
                },
                ApplyQuantification: quant =>
                {
                    Assert.Same(segmentation.Quant, quant);
                    calls.Add($"apply:{quant.Label}");
                },
                TraceError: message => calls.Add($"trace:{message}")));

        Assert.Equal(LiveDetectionMarkBoxSegmentationOutcome.Segmented, result.Outcome);
        Assert.Same(segmentation, result.Segmentation);
        Assert.Equal(["box", "calibration", "segment", "apply:root"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_traces_and_returns_null_when_segmentation_fails()
    {
        var calls = new List<string>();

        var result = await LiveDetectionMarkBoxSegmentationWorkflow.ExecuteAsync(
            new LiveDetectionMarkBoxSegmentationRequest(
                HasBoxSegmentation: true,
                FrameBytes: [1, 2, 3],
                OverlayPointCount: 2),
            new LiveDetectionMarkBoxSegmentationActions(
                BuildBox: () => throw new InvalidOperationException("kaputt"),
                GetCalibration: () => null,
                SegmentBoxAsync: (_, _, _, _) => throw new InvalidOperationException("should not segment"),
                ApplyQuantification: _ => throw new InvalidOperationException("should not apply"),
                TraceError: message => calls.Add(message)));

        Assert.Equal(LiveDetectionMarkBoxSegmentationOutcome.Failed, result.Outcome);
        Assert.Null(result.Segmentation);
        Assert.Equal(["[Mark-SAM] Segmentierung uebersprungen: kaputt"], calls);
    }

    private static LiveDetectionMarkBoxSegmentationActions NoActions()
        => new(
            BuildBox: () => throw new InvalidOperationException("BuildBox should not run."),
            GetCalibration: () => throw new InvalidOperationException("GetCalibration should not run."),
            SegmentBoxAsync: (_, _, _, _) => throw new InvalidOperationException("SegmentBoxAsync should not run."),
            ApplyQuantification: _ => throw new InvalidOperationException("ApplyQuantification should not run."),
            TraceError: _ => throw new InvalidOperationException("TraceError should not run."));

    private static BoxSegmentationResult Result()
        => new(
            new MaskQuantificationService.QuantifiedMask(
                Label: "root",
                Confidence: 0.9,
                HeightMm: 12,
                WidthMm: 20,
                ExtentPercent: 5,
                CrossSectionReductionPercent: 3,
                IntrusionPercent: null,
                ClockPosition: "3"),
            new SamMaskResult(
                "root",
                0.9,
                [1, 2, 3, 4],
                "",
                20,
                100,
                4,
                5,
                10,
                12),
            ImageWidth: 100,
            ImageHeight: 80);
}
