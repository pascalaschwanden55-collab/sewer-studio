using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingLiveFrameThrottleControllerTests
{
    [Fact]
    public void Decide_gibt_leeren_pfad_sofort_frei_ohne_timestamp_update()
    {
        var lastUpdatedUtc = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

        var result = TrainingLiveFrameThrottleController.Decide(
            "",
            lastUpdatedUtc,
            lastUpdatedUtc.AddMilliseconds(50));

        Assert.True(result.ShouldUpdateFramePath);
        Assert.Equal("", result.FramePath);
        Assert.Equal(lastUpdatedUtc, result.LastUpdatedUtc);
    }

    [Fact]
    public void Decide_blockiert_bildwechsel_innerhalb_des_throttle_fensters()
    {
        var lastUpdatedUtc = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

        var result = TrainingLiveFrameThrottleController.Decide(
            @"C:\frames\a.jpg",
            lastUpdatedUtc,
            lastUpdatedUtc.AddMilliseconds(179));

        Assert.False(result.ShouldUpdateFramePath);
        Assert.Null(result.FramePath);
        Assert.Equal(lastUpdatedUtc, result.LastUpdatedUtc);
    }

    [Fact]
    public void Decide_gibt_bildwechsel_ab_180_millisekunden_frei_und_aktualisiert_timestamp()
    {
        var lastUpdatedUtc = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var nowUtc = lastUpdatedUtc.AddMilliseconds(180);

        var result = TrainingLiveFrameThrottleController.Decide(
            @"C:\frames\b.jpg",
            lastUpdatedUtc,
            nowUtc);

        Assert.True(result.ShouldUpdateFramePath);
        Assert.Equal(@"C:\frames\b.jpg", result.FramePath);
        Assert.Equal(nowUtc, result.LastUpdatedUtc);
    }
}
