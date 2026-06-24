using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingFrameReadinessController
{
    private readonly CodingFrameReadinessTracker _tracker = new();
    private LiveDetection? _pendingWarmupResult;

    public bool IsReady => _tracker.IsReady;
    public int SkippedFrames => _tracker.SkippedFrames;
    public double? FirstCleanFrameSeconds => _tracker.FirstCleanFrameSeconds;

    public void Reset()
    {
        _tracker.Reset();
        _pendingWarmupResult = null;
    }

    public void Update(LiveDetection result, double fallbackTimestampSeconds)
    {
        ArgumentNullException.ThrowIfNull(result);

        _tracker.Update(
            result.TimestampSeconds,
            result.MeterReading.HasValue,
            fallbackTimestampSeconds);
    }

    public bool StorePendingWarmupResult(LiveDetection result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Findings.Count == 0)
            return false;

        _pendingWarmupResult = result;
        return true;
    }

    public LiveDetection SelectReadyResult(LiveDetection current)
    {
        ArgumentNullException.ThrowIfNull(current);

        var selection = CodingWarmupResultBufferPolicy.Select(current, _pendingWarmupResult);
        if (selection.ShouldClearPending)
            _pendingWarmupResult = null;

        return selection.Result;
    }
}
