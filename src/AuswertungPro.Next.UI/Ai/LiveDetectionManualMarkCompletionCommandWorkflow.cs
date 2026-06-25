using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionManualMarkCompletionCommandOutcome
{
    MissingOverlay,
    Completed,
    Failed
}

public sealed record LiveDetectionManualMarkCompletionCommandActions<TSegment>(
    Func<OverlayGeometry?> GetCurrentOverlay,
    Func<double> GetTimestampSeconds,
    Func<Task<byte[]?>> CaptureCurrentFrameAsync,
    Func<OverlayGeometry, string?> EstimateClockPosition,
    Func<OverlayGeometry, byte[]?, Task<TSegment?>> SegmentMarkAsync,
    Func<TSegment, string?> GetSegmentClockPosition,
    Action<TSegment, OverlayGeometry> ShowSegment,
    Func<Task> DelayAfterSegmentAsync,
    Func<OverlayGeometry, double, string?, byte[]?, Task<bool>> SaveTrainingAsync,
    Action<bool> CompleteManualMark,
    Action<string> TraceError);

public sealed record LiveDetectionManualMarkCompletionCommandResult(
    LiveDetectionManualMarkCompletionCommandOutcome Outcome)
{
    public bool Completed => Outcome == LiveDetectionManualMarkCompletionCommandOutcome.Completed;
}

public static class LiveDetectionManualMarkCompletionCommandWorkflow
{
    public static TimeSpan SegmentPreviewDelay { get; } = TimeSpan.FromSeconds(3);

    public static Task DelayAfterSegmentPreviewAsync()
        => Task.Delay(SegmentPreviewDelay);

    public static async Task<LiveDetectionManualMarkCompletionCommandResult> ExecuteAsync<TSegment>(
        LiveDetectionManualMarkCompletionCommandActions<TSegment> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        try
        {
            var overlay = actions.GetCurrentOverlay();
            if (overlay == null)
                return Result(LiveDetectionManualMarkCompletionCommandOutcome.MissingOverlay);

            var timestampSec = actions.GetTimestampSeconds();
            var frameBytes = await actions.CaptureCurrentFrameAsync();
            var clockPosition = actions.EstimateClockPosition(overlay);

            var segment = await actions.SegmentMarkAsync(overlay, frameBytes);
            if (segment != null)
            {
                var segmentClockPosition = actions.GetSegmentClockPosition(segment);
                if (!string.IsNullOrEmpty(segmentClockPosition))
                    clockPosition = segmentClockPosition;

                actions.ShowSegment(segment, overlay);
                await actions.DelayAfterSegmentAsync();
            }

            var saved = await actions.SaveTrainingAsync(overlay, timestampSec, clockPosition, frameBytes);
            actions.CompleteManualMark(saved);
            return Result(LiveDetectionManualMarkCompletionCommandOutcome.Completed);
        }
        catch (Exception ex)
        {
            actions.TraceError(ex.Message);
            return Result(LiveDetectionManualMarkCompletionCommandOutcome.Failed);
        }
    }

    private static LiveDetectionManualMarkCompletionCommandResult Result(
        LiveDetectionManualMarkCompletionCommandOutcome outcome)
        => new(outcome);
}
