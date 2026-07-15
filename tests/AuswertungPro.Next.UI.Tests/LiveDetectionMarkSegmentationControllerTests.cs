using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionMarkSegmentationControllerTests
{
    [Fact]
    public async Task TrySegmentAsync_preserves_box_calibration_and_quantification_mapping()
    {
        var calls = new List<string>();
        var frameBytes = new byte[] { 1, 2, 3 };
        var overlay = new OverlayGeometry
        {
            Points = [new NormalizedPoint(0.2, 0.3), new NormalizedPoint(0.6, 0.7)]
        };
        var calibration = new PipeCalibration { NominalDiameterMm = 300 };
        var segmentation = Result();
        var controller = new LiveDetectionMarkSegmentationController(
            Bindings(
                hasBoxSegmentation: () => true,
                getCalibration: () =>
                {
                    calls.Add("calibration");
                    return calibration;
                },
                segmentBoxAsync: (actualFrame, box, dn, actualCalibration) =>
                {
                    Assert.Same(frameBytes, actualFrame);
                    Assert.Equal(0.4, box.XCenter, 10);
                    Assert.Equal(0.5, box.YCenter, 10);
                    Assert.Equal(0.4, box.Width, 10);
                    Assert.Equal(0.4, box.Height, 10);
                    Assert.Equal(300, dn);
                    Assert.Same(calibration, actualCalibration);
                    calls.Add("segment");
                    return Task.FromResult<BoxSegmentationResult?>(segmentation);
                }));

        var result = await controller.TrySegmentAsync(overlay, frameBytes);

        Assert.Same(segmentation, result);
        Assert.Equal(["calibration", "segment"], calls);
        Assert.Equal(12, overlay.Q1Mm);
        Assert.Equal(20, overlay.Q2Mm);
        Assert.Equal(3, overlay.FillPercent);
        Assert.Equal(3, overlay.ClockFrom);
    }

    [Fact]
    public void ShowMask_preserves_bend_marker_decision()
    {
        var calls = new List<string>();
        var overlay = new OverlayGeometry
        {
            Points = [new NormalizedPoint(0.2, 0.3), new NormalizedPoint(0.6, 0.7)]
        };
        var controller = new LiveDetectionMarkSegmentationController(
            Bindings(
                getContentRect: () =>
                {
                    calls.Add("rect");
                    return new Rect(0, 0, 640, 480);
                },
                showBendMarker: (x, y, rect) =>
                {
                    Assert.Equal(0.4, x);
                    Assert.Equal(0.5, y);
                    Assert.Equal(480, rect.Height);
                    calls.Add("bend");
                },
                renderMasks: (_, _, _) => throw new InvalidOperationException("Maske darf bei erkanntem Bogen nicht erscheinen.")));

        controller.ShowMask(Result(isBend: true, vanishX: 0.4, vanishY: 0.5), overlay);

        Assert.Equal(["rect", "bend"], calls);
    }

    private static LiveDetectionMarkSegmentationControllerBindings Bindings(
        Func<bool>? hasBoxSegmentation = null,
        Func<byte[], NormalizedBoundingBox, int, PipeCalibration?, Task<BoxSegmentationResult?>>? segmentBoxAsync = null,
        Func<PipeCalibration?>? getCalibration = null,
        Func<Rect>? getContentRect = null,
        Action<double, double, Rect>? showBendMarker = null,
        Action<SamResponse, IReadOnlyList<MaskQuantificationService.QuantifiedMask>, Rect>? renderMasks = null)
        => new(
            HasBoxSegmentation: hasBoxSegmentation ?? (() => false),
            SegmentBoxAsync: segmentBoxAsync ?? ((_, _, _, _) => Task.FromResult<BoxSegmentationResult?>(null)),
            GetCalibration: getCalibration ?? (() => null),
            GetContentRect: getContentRect ?? (() => Rect.Empty),
            ShowBendMarker: showBendMarker ?? ((_, _, _) => { }),
            RenderMasks: renderMasks ?? ((_, _, _) => { }),
            TraceError: _ => { });

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
