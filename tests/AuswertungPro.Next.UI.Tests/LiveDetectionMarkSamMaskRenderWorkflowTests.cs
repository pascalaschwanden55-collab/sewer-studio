using System.Windows;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionMarkSamMaskRenderWorkflowTests
{
    [Fact]
    public void Execute_renders_mask_when_content_rect_is_valid()
    {
        var calls = new List<string>();
        var segmentation = Result();

        var result = LiveDetectionMarkSamMaskRenderWorkflow.Execute(
            new LiveDetectionMarkSamMaskRenderRequest(segmentation),
            new LiveDetectionMarkSamMaskRenderActions(
                GetContentRect: () =>
                {
                    calls.Add("rect");
                    return new Rect(0, 0, 640, 480);
                },
                ContainsVanishingPoint: _ => throw new InvalidOperationException("No bend check expected."),
                ShowBendMarker: (_, _, _) => throw new InvalidOperationException("No bend marker expected."),
                RenderMasks: (response, quantifications, rect) =>
                {
                    Assert.Same(segmentation.Mask, Assert.Single(response.Masks));
                    Assert.Same(segmentation.Quant, Assert.Single(quantifications));
                    Assert.Equal(640, rect.Width);
                    calls.Add("render");
                },
                TraceError: message => calls.Add($"trace:{message}")));

        Assert.Equal(LiveDetectionMarkSamMaskRenderOutcome.MaskRendered, result.Outcome);
        Assert.Equal(["rect", "render"], calls);
    }

    [Fact]
    public void Execute_shows_bend_marker_when_bend_contains_vanishing_point()
    {
        var calls = new List<string>();
        var segmentation = Result(isBend: true, vanishX: 0.42, vanishY: 0.55);

        var result = LiveDetectionMarkSamMaskRenderWorkflow.Execute(
            new LiveDetectionMarkSamMaskRenderRequest(segmentation),
            new LiveDetectionMarkSamMaskRenderActions(
                GetContentRect: () => new Rect(0, 0, 640, 480),
                ContainsVanishingPoint: selected =>
                {
                    Assert.Same(segmentation, selected);
                    calls.Add("contains");
                    return true;
                },
                ShowBendMarker: (x, y, rect) =>
                {
                    Assert.Equal(0.42, x);
                    Assert.Equal(0.55, y);
                    Assert.Equal(480, rect.Height);
                    calls.Add("bend");
                },
                RenderMasks: (_, _, _) => throw new InvalidOperationException("No mask render expected."),
                TraceError: message => calls.Add($"trace:{message}")));

        Assert.Equal(LiveDetectionMarkSamMaskRenderOutcome.BendMarkerShown, result.Outcome);
        Assert.Equal(["contains", "bend"], calls);
    }

    [Fact]
    public void Execute_traces_render_errors_without_throwing()
    {
        var calls = new List<string>();

        var result = LiveDetectionMarkSamMaskRenderWorkflow.Execute(
            new LiveDetectionMarkSamMaskRenderRequest(Result()),
            new LiveDetectionMarkSamMaskRenderActions(
                GetContentRect: () => new Rect(0, 0, 640, 480),
                ContainsVanishingPoint: _ => false,
                ShowBendMarker: (_, _, _) => throw new InvalidOperationException("No bend marker expected."),
                RenderMasks: (_, _, _) => throw new InvalidOperationException("kaputt"),
                TraceError: message => calls.Add(message)));

        Assert.Equal(LiveDetectionMarkSamMaskRenderOutcome.Failed, result.Outcome);
        Assert.Equal(["[Mark-SAM] Masken-Render uebersprungen: kaputt"], calls);
    }

    private static BoxSegmentationResult Result(
        bool isBend = false,
        double vanishX = 0.5,
        double vanishY = 0.5)
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
            ImageHeight: 80,
            IsBend: isBend,
            VanishX: vanishX,
            VanishY: vanishY);
}
