using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMeterResolverTests
{
    [Fact]
    public void Resolve_prefers_same_frame_osd_meter()
    {
        var result = CodingMeterResolver.Resolve(
            frameTimestampSeconds: 10,
            sameFrameOsdMeter: 12.345,
            cachedOsdMeter: 8,
            cachedOsdTimestampSeconds: 10,
            currentPlayerTimestampSeconds: 50,
            videoDurationSeconds: 100,
            endMeter: 40,
            currentMeter: 3);

        Assert.Equal(12.34, result.Meter);
        Assert.True(result.IsOsd);
    }

    [Fact]
    public void Resolve_uses_recent_cached_osd_meter_before_video_estimate()
    {
        var result = CodingMeterResolver.Resolve(
            frameTimestampSeconds: 10.5,
            sameFrameOsdMeter: null,
            cachedOsdMeter: 8.126,
            cachedOsdTimestampSeconds: 10,
            currentPlayerTimestampSeconds: 50,
            videoDurationSeconds: 100,
            endMeter: 40,
            currentMeter: 3);

        Assert.Equal(8.13, result.Meter);
        Assert.True(result.IsOsd);
    }

    [Fact]
    public void Resolve_uses_video_estimate_when_osd_is_missing_or_stale()
    {
        var result = CodingMeterResolver.Resolve(
            frameTimestampSeconds: 25,
            sameFrameOsdMeter: null,
            cachedOsdMeter: 8,
            cachedOsdTimestampSeconds: 20,
            currentPlayerTimestampSeconds: 50,
            videoDurationSeconds: 100,
            endMeter: 40,
            currentMeter: 3);

        Assert.Equal(10.0, result.Meter);
        Assert.False(result.IsOsd);
    }

    [Fact]
    public void Resolve_falls_back_to_current_meter_when_video_estimate_is_not_possible()
    {
        var result = CodingMeterResolver.Resolve(
            frameTimestampSeconds: null,
            sameFrameOsdMeter: null,
            cachedOsdMeter: null,
            cachedOsdTimestampSeconds: null,
            currentPlayerTimestampSeconds: null,
            videoDurationSeconds: null,
            endMeter: 0,
            currentMeter: 4.567);

        Assert.Equal(4.57, result.Meter);
        Assert.False(result.IsOsd);
    }

    [Fact]
    public void ShouldResetRecentMeterForSeek_detects_large_timestamp_gap()
    {
        Assert.False(CodingMeterResolver.ShouldResetRecentMeterForSeek(10, 5));
        Assert.True(CodingMeterResolver.ShouldResetRecentMeterForSeek(12, 5));
    }
}
