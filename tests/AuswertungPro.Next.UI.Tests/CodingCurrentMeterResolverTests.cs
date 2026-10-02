using AuswertungPro.Next.Application.Ai;
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

    // Entscheid Pascal 02.10.2026 (E2): Beim Handeintrag gilt ein gemerkter OSD-Meter nur,
    // wenn er hoechstens CodingMeterResolver.RecentOsdMeterMaxAgeSeconds alt ist — dieselbe
    // Regel wie bei der Anzeige. Vorher galt er hier ohne Altersgrenze.
    [Fact]
    public void ResolveManualEntry_uses_recent_cached_osd_before_video_position()
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: 9.876,
            playerTimeMs: 500,
            playerLengthMs: 1000,
            endMeter: 50,
            sessionCurrentMeter: 4,
            cachedOsdTimestampSeconds: 0.2);

        Assert.Equal(9.88, meter);
    }

    [Fact]
    public void ResolveManualEntry_ignores_cached_osd_that_is_ten_seconds_old()
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: 9.876,
            playerTimeMs: 20_000,
            playerLengthMs: 40_000,
            endMeter: 50,
            sessionCurrentMeter: 4,
            cachedOsdTimestampSeconds: 10);

        Assert.Equal(25, meter);
    }

    [Fact]
    public void ResolveManualEntry_ignores_cached_osd_without_timestamp()
    {
        // Ohne Zeitstempel ist das Alter unbekannt: Der gemerkte Wert gilt nicht.
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: 9.876,
            playerTimeMs: 20_000,
            playerLengthMs: 40_000,
            endMeter: 50,
            sessionCurrentMeter: 4);

        Assert.Equal(25, meter);
    }

    [Theory]
    [InlineData(0.0, 9.88)]
    [InlineData(0.01, 25.0)]
    public void ResolveManualEntry_uses_the_shared_age_limit_of_the_display_rule(
        double zusaetzlichesAlter,
        double expected)
    {
        var meter = CodingCurrentMeterResolver.ResolveManualEntry(
            osdMeter: null,
            cachedOsdMeter: 9.876,
            playerTimeMs: 20_000,
            playerLengthMs: 40_000,
            endMeter: 50,
            sessionCurrentMeter: 4,
            cachedOsdTimestampSeconds: 20 - CodingMeterResolver.RecentOsdMeterMaxAgeSeconds - zusaetzlichesAlter);

        Assert.Equal(expected, meter);
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
