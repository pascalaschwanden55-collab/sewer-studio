using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFrameReadinessTrackerTests
{
    [Fact]
    public void Update_requires_second_meter_frame_before_ready()
    {
        var tracker = new CodingFrameReadinessTracker();

        tracker.Update(frameTimestampSeconds: 1.0, hasMeterThisFrame: true, fallbackTimestampSeconds: 0);

        Assert.False(tracker.IsReady);
        Assert.Equal(CodingFrameReadinessState.Warmup, tracker.State);
        Assert.Equal(1, tracker.MeterConfirmCount);

        tracker.Update(frameTimestampSeconds: 2.0, hasMeterThisFrame: true, fallbackTimestampSeconds: 0);

        Assert.True(tracker.IsReady);
        Assert.Equal(CodingFrameReadinessState.Ready, tracker.State);
        Assert.Equal(2.0, tracker.FirstCleanFrameSeconds);
    }

    [Fact]
    public void Update_releases_after_three_frames_without_osd_meter()
    {
        var tracker = new CodingFrameReadinessTracker();

        tracker.Update(frameTimestampSeconds: 1.0, hasMeterThisFrame: false, fallbackTimestampSeconds: 0);
        tracker.Update(frameTimestampSeconds: 2.0, hasMeterThisFrame: false, fallbackTimestampSeconds: 0);
        tracker.Update(frameTimestampSeconds: 3.0, hasMeterThisFrame: false, fallbackTimestampSeconds: 0);

        Assert.True(tracker.IsReady);
        Assert.Equal(3, tracker.SkippedFrames);
        Assert.Equal(3.0, tracker.FirstCleanFrameSeconds);
    }

    [Fact]
    public void Update_releases_warmup_after_two_missing_confirmation_frames()
    {
        var tracker = new CodingFrameReadinessTracker();

        tracker.Update(frameTimestampSeconds: 1.0, hasMeterThisFrame: true, fallbackTimestampSeconds: 0);
        tracker.Update(frameTimestampSeconds: 2.0, hasMeterThisFrame: false, fallbackTimestampSeconds: 0);
        tracker.Update(frameTimestampSeconds: 3.0, hasMeterThisFrame: false, fallbackTimestampSeconds: 0);

        Assert.True(tracker.IsReady);
        Assert.Equal(3.0, tracker.FirstCleanFrameSeconds);
    }

    [Fact]
    public void Update_uses_fallback_timestamp_when_frame_timestamp_is_invalid()
    {
        var tracker = new CodingFrameReadinessTracker();

        tracker.Update(frameTimestampSeconds: -1.0, hasMeterThisFrame: false, fallbackTimestampSeconds: 12.5);
        tracker.Update(frameTimestampSeconds: -1.0, hasMeterThisFrame: false, fallbackTimestampSeconds: 12.5);
        tracker.Update(frameTimestampSeconds: -1.0, hasMeterThisFrame: false, fallbackTimestampSeconds: 12.5);

        Assert.True(tracker.IsReady);
        Assert.Equal(12.5, tracker.FirstCleanFrameSeconds);
    }
}
