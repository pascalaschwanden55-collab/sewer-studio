using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSamMaskOverlayControllerTests
{
    [Fact]
    public void RenderCandidates_draws_visible_masks_into_content_rect()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();

            var summary = CodingSamMaskOverlayController.RenderCandidates(
                canvas,
                [Candidate("incrustation infiltration", 0.86, 0.55, 0.26)],
                imageWidth: 100,
                imageHeight: 100,
                contentRect: new Rect(10, 20, 100, 100),
                logger: null);

            return (summary.Rendered, summary.SubtleFill, canvas.Children.Count);
        });

        Assert.Equal(1, result.Rendered);
        Assert.Equal(1, result.SubtleFill);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Clear_removes_masks_rendered_by_controller()
    {
        var childCount = RunOnSta(() =>
        {
            var canvas = new Canvas();

            CodingSamMaskOverlayController.RenderCandidates(
                canvas,
                [Candidate("incrustation infiltration", 0.86, 0.55, 0.26)],
                imageWidth: 100,
                imageHeight: 100,
                contentRect: new Rect(0, 0, 100, 100),
                logger: null);

            CodingSamMaskOverlayController.Clear(canvas);

            return canvas.Children.Count;
        });

        Assert.Equal(0, childCount);
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

        var quant = new MaskQuantificationService.QuantifiedMask(
            label,
            samConfidence,
            HeightMm: null,
            WidthMm: null,
            ExtentPercent: null,
            CrossSectionReductionPercent: null,
            IntrusionPercent: null,
            ClockPosition: null);

        return new SamMaskRenderer.MaskRenderCandidate(mask, quant, dinoConfidence);
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
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
        return result!;
    }
}
