using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFrameReadinessControllerTests
{
    [Fact]
    public void StorePendingWarmupResult_and_select_ready_result_replays_pending_findings_once()
    {
        var controller = new CodingFrameReadinessController();
        var pending = Detection("warmup", hasFinding: true, meterReading: 1.2, timestampSeconds: 1);
        var ready = Detection("ready-empty", hasFinding: false, meterReading: 1.3, timestampSeconds: 2);

        controller.Update(pending, fallbackTimestampSeconds: 0);
        Assert.False(controller.IsReady);
        Assert.True(controller.StorePendingWarmupResult(pending));

        controller.Update(ready, fallbackTimestampSeconds: 0);
        Assert.True(controller.IsReady);

        Assert.Same(pending, controller.SelectReadyResult(ready));
        Assert.Same(ready, controller.SelectReadyResult(ready));
    }

    [Fact]
    public void Reset_clears_tracker_and_pending_warmup_result()
    {
        var controller = new CodingFrameReadinessController();
        var pending = Detection("warmup", hasFinding: true, meterReading: 1.2, timestampSeconds: 1);

        controller.Update(pending, fallbackTimestampSeconds: 0);
        controller.StorePendingWarmupResult(pending);
        controller.Reset();

        var ready = Detection("ready-empty", hasFinding: false, meterReading: null, timestampSeconds: 2);

        Assert.False(controller.IsReady);
        Assert.Equal(0, controller.SkippedFrames);
        Assert.Null(controller.FirstCleanFrameSeconds);
        Assert.Same(ready, controller.SelectReadyResult(ready));
    }

    private static LiveDetection Detection(
        string label,
        bool hasFinding,
        double? meterReading,
        double timestampSeconds)
        => new(
            TimestampSeconds: timestampSeconds,
            Findings: hasFinding
                ? [new LiveFrameFinding(label, Severity: 1, PositionClock: null, ExtentPercent: null)]
                : [],
            MeterReading: meterReading,
            Error: null);
}
