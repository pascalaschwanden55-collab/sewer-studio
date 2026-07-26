using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionMarkBoxSegmentationOutcome
{
    Skipped,
    Segmented,
    Failed
}

public sealed record LiveDetectionMarkBoxSegmentationRequest(
    bool HasBoxSegmentation,
    byte[]? FrameBytes,
    int OverlayPointCount);

public sealed record LiveDetectionMarkBoxSegmentationActions(
    Func<NormalizedBoundingBox> BuildBox,
    Func<PipeCalibration?> GetCalibration,
    Func<byte[], NormalizedBoundingBox, int, PipeCalibration?, Task<BoxSegmentationResult?>> SegmentBoxAsync,
    Action<MaskQuantificationService.QuantifiedMask> ApplyQuantification,
    Action<string> TraceError);

public sealed record LiveDetectionMarkBoxSegmentationResult(
    LiveDetectionMarkBoxSegmentationOutcome Outcome,
    BoxSegmentationResult? Segmentation);

public static class LiveDetectionMarkBoxSegmentationWorkflow
{
    public static async Task<LiveDetectionMarkBoxSegmentationResult> ExecuteAsync(
        LiveDetectionMarkBoxSegmentationRequest request,
        LiveDetectionMarkBoxSegmentationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasBoxSegmentation
            || request.FrameBytes is null
            || request.FrameBytes.Length == 0
            || request.OverlayPointCount < 2)
            return Result(LiveDetectionMarkBoxSegmentationOutcome.Skipped, null);

        try
        {
            var box = actions.BuildBox();
            var calibration = actions.GetCalibration();
            var dn = calibration?.NominalDiameterMm ?? 0;

            var segmentation = await actions.SegmentBoxAsync(
                request.FrameBytes,
                box,
                dn,
                calibration);
            if (segmentation is null)
                return Result(LiveDetectionMarkBoxSegmentationOutcome.Skipped, null);

            actions.ApplyQuantification(segmentation.Quant);
            return Result(LiveDetectionMarkBoxSegmentationOutcome.Segmented, segmentation);
        }
        catch (Exception ex)
        {
            actions.TraceError($"[Mark-SAM] Segmentierung uebersprungen: {ex.Message}");
            return Result(LiveDetectionMarkBoxSegmentationOutcome.Failed, null);
        }
    }

    private static LiveDetectionMarkBoxSegmentationResult Result(
        LiveDetectionMarkBoxSegmentationOutcome outcome,
        BoxSegmentationResult? segmentation)
        => new(outcome, segmentation);
}
