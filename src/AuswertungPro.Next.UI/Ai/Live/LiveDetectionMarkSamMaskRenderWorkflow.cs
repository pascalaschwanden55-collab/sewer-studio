using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionMarkSamMaskRenderOutcome
{
    Skipped,
    BendMarkerShown,
    MaskRendered,
    Failed
}

public sealed record LiveDetectionMarkSamMaskRenderRequest(
    BoxSegmentationResult Segmentation);

public sealed record LiveDetectionMarkSamMaskRenderActions(
    Func<Rect> GetContentRect,
    Func<BoxSegmentationResult, bool> ContainsVanishingPoint,
    Action<double, double, Rect> ShowBendMarker,
    Action<SamResponse, IReadOnlyList<MaskQuantificationService.QuantifiedMask>, Rect> RenderMasks,
    Action<string> TraceError);

public sealed record LiveDetectionMarkSamMaskRenderResult(
    LiveDetectionMarkSamMaskRenderOutcome Outcome);

public static class LiveDetectionMarkSamMaskRenderWorkflow
{
    public static LiveDetectionMarkSamMaskRenderResult Execute(
        LiveDetectionMarkSamMaskRenderRequest request,
        LiveDetectionMarkSamMaskRenderActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        try
        {
            var rect = actions.GetContentRect();
            if (rect.Width <= 0 || rect.Height <= 0)
                return Result(LiveDetectionMarkSamMaskRenderOutcome.Skipped);

            var segmentation = request.Segmentation;
            // IsBend ist ein zusaetzliches Geometriesignal, aber keine Segmentierung.
            // Im manuellen Codierablauf muss vor dem Codierfenster immer die echte
            // SAM-Maske sichtbar sein; ein Oval darf sie nicht ersetzen.
            var samResponse = new SamResponse(
                new[] { segmentation.Mask },
                segmentation.ImageWidth,
                segmentation.ImageHeight,
                InferenceTimeMs: 0);
            actions.RenderMasks(samResponse, new[] { segmentation.Quant }, rect);
            return Result(LiveDetectionMarkSamMaskRenderOutcome.MaskRendered);
        }
        catch (Exception ex)
        {
            actions.TraceError($"[Mark-SAM] Masken-Render uebersprungen: {ex.Message}");
            return Result(LiveDetectionMarkSamMaskRenderOutcome.Failed);
        }
    }

    private static LiveDetectionMarkSamMaskRenderResult Result(
        LiveDetectionMarkSamMaskRenderOutcome outcome)
        => new(outcome);
}
