using AuswertungPro.Next.UI.Ai;

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
}
