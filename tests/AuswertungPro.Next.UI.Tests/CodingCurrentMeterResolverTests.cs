using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCurrentMeterResolverTests
{
    [Fact]
    public void Resolve_prefers_osd_meter()
    {
        var meter = CodingCurrentMeterResolver.Resolve(
            osdMeter: 12.3,
            playerTimeMs: 500,
            playerLengthMs: 1000,
            endMeter: 50,
            sessionCurrentMeter: 4);

        Assert.Equal(12.3, meter);
    }

    [Fact]
    public void Resolve_uses_video_position_when_osd_is_missing()
    {
        var meter = CodingCurrentMeterResolver.Resolve(
            osdMeter: null,
            playerTimeMs: 250,
            playerLengthMs: 1000,
            endMeter: 80,
            sessionCurrentMeter: 4);

        Assert.Equal(20, meter);
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(1000, 0)]
    [InlineData(-1, 80)]
    public void Resolve_falls_back_to_session_meter_when_video_ratio_is_unusable(
        long playerLengthMs,
        double endMeter)
    {
        var meter = CodingCurrentMeterResolver.Resolve(
            osdMeter: null,
            playerTimeMs: 250,
            playerLengthMs: playerLengthMs,
            endMeter: endMeter,
            sessionCurrentMeter: 4.5);

        Assert.Equal(4.5, meter);
    }

    [Fact]
    public void ResolveManualEntry_prefers_fresh_osd_over_cached_osd_and_video_position()
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: 12.346,
            cachedOsdMeter: 9.9,
            playerTimeMs: 500,
            playerLengthMs: 1000,
            endMeter: 50,
            sessionCurrentMeter: 4);

        Assert.Equal(12.35, meter);
    }

    [Fact]
    public void ResolveManualEntry_uses_cached_osd_before_video_position()
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: 9.876,
            playerTimeMs: 500,
            playerLengthMs: 1000,
            endMeter: 50,
            sessionCurrentMeter: 4,
            cachedOsdTimestampSeconds: 0.5);

        Assert.Equal(9.88, meter);
    }

    [Fact]
    public void ResolveManualEntry_after_backward_seek_uses_video_position_without_cache_timestamp()
    {
        var cachedMeter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: 70,
            cachedOsdMeter: null,
            playerTimeMs: 90000,
            playerLengthMs: 100000,
            endMeter: 80,
            sessionCurrentMeter: 4);

        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: cachedMeter,
            playerTimeMs: 5000,
            playerLengthMs: 100000,
            endMeter: 80,
            sessionCurrentMeter: 4);

        Assert.Equal(4, meter);
    }

    [Fact]
    public void ResolveManualEntry_without_cache_timestamp_and_video_ratio_uses_session_meter()
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: 70,
            playerTimeMs: 5000,
            playerLengthMs: 0,
            endMeter: 80,
            sessionCurrentMeter: 4.567);

        Assert.Equal(4.57, meter);
    }

    [Fact]
    public void ResolveManualEntry_uses_rounded_video_position_before_session_meter()
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: null,
            playerTimeMs: 333,
            playerLengthMs: 1000,
            endMeter: 80,
            sessionCurrentMeter: 4);

        Assert.Equal(26.64, meter);
    }

    [Fact]
    public void ResolveManualEntry_clamps_negative_meter_to_zero()
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: -1.2,
            cachedOsdMeter: null,
            playerTimeMs: 333,
            playerLengthMs: 1000,
            endMeter: 80,
            sessionCurrentMeter: 4);

        Assert.Equal(0, meter);
    }

    [Theory]
    [InlineData(8.5, 9.88)]
    [InlineData(11.5, 9.88)]
    [InlineData(8.499, 8)]
    [InlineData(11.501, 8)]
    public void ResolveManualEntry_accepts_cached_osd_only_within_inclusive_time_window(
        double cachedTimestampSeconds,
        double expected)
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: 9.876,
            playerTimeMs: 10000,
            playerLengthMs: 100000,
            endMeter: 80,
            sessionCurrentMeter: 4,
            cachedOsdTimestampSeconds: cachedTimestampSeconds);

        Assert.Equal(expected, meter);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ResolveManualEntry_without_finite_cache_timestamp_uses_video_position(
        double? cachedTimestampSeconds)
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: 70,
            playerTimeMs: 10000,
            playerLengthMs: 100000,
            endMeter: 80,
            sessionCurrentMeter: 4,
            cachedOsdTimestampSeconds: cachedTimestampSeconds);

        Assert.Equal(8, meter);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(500.0, 500.0)]
    [InlineData(-0.01, 8)]
    [InlineData(500.01, 8)]
    [InlineData(double.NaN, 8)]
    [InlineData(double.PositiveInfinity, 8)]
    [InlineData(double.NegativeInfinity, 8)]
    [InlineData(null, 8)]
    public void ResolveManualEntry_accepts_only_plausible_fresh_cached_osd(
        double? cachedMeter,
        double expected)
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: cachedMeter,
            playerTimeMs: 10000,
            playerLengthMs: 100000,
            endMeter: 80,
            sessionCurrentMeter: 4,
            cachedOsdTimestampSeconds: 10);

        Assert.Equal(expected, meter);
    }

    [Theory]
    [InlineData(12.346, 12.35)]
    [InlineData(-1.2, 0)]
    public void ResolveManualEntry_fresh_osd_keeps_priority_and_rounding_with_valid_cache(
        double freshOsdMeter,
        double expected)
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: freshOsdMeter,
            cachedOsdMeter: 70,
            playerTimeMs: 10000,
            playerLengthMs: 100000,
            endMeter: 80,
            sessionCurrentMeter: 4,
            cachedOsdTimestampSeconds: 10);

        Assert.Equal(expected, meter);
    }

    [Fact]
    public void ResolveManualEntry_video_position_beyond_duration_keeps_meter_above_end_meter()
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: null,
            playerTimeMs: 1250,
            playerLengthMs: 1000,
            endMeter: 80,
            sessionCurrentMeter: 4);

        Assert.Equal(100, meter);
    }

    [Theory]
    [InlineData("12.34m", 12.34)]
    [InlineData("12,34m", 12.34)]
    [InlineData(" 12.34 m ", 12.34)]
    [InlineData("12.34", 12.34)]
    public void ParseDisplayedMeterOrZero_reads_invariant_meter_text(
        string text,
        double expected)
    {
        var meter = CodingCurrentMeterResolver.ParseDisplayedMeterOrZero(text);

        Assert.Equal(expected, meter);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void ParseDisplayedMeterOrZero_returns_zero_when_text_is_missing_or_invalid(
        string? text)
    {
        var meter = CodingCurrentMeterResolver.ParseDisplayedMeterOrZero(text);

        Assert.Equal(0, meter);
    }
}
