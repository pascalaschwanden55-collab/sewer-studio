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

    [Fact]
    public void Apply_setzt_frame_und_timestamp_wenn_decision_freigegeben_ist()
    {
        var lastUpdatedUtc = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var nowUtc = lastUpdatedUtc.AddMilliseconds(180);
        var framePath = "";
        var storedTimestamp = lastUpdatedUtc;

        TrainingLiveFrameThrottleController.Apply(
            @"C:\frames\b.jpg",
            () => storedTimestamp,
            value => storedTimestamp = value,
            value => framePath = value,
            () => nowUtc);

        Assert.Equal(@"C:\frames\b.jpg", framePath);
        Assert.Equal(nowUtc, storedTimestamp);
    }

    [Fact]
    public void Apply_ohne_uhr_delegate_verwendet_controller_default_zeit()
    {
        var lastUpdatedUtc = DateTime.UtcNow.AddSeconds(-10);
        var before = DateTime.UtcNow;
        var framePath = "";
        var storedTimestamp = lastUpdatedUtc;

        TrainingLiveFrameThrottleController.Apply(
            @"C:\frames\default.jpg",
            () => storedTimestamp,
            value => storedTimestamp = value,
            value => framePath = value);

        var after = DateTime.UtcNow;

        Assert.Equal(@"C:\frames\default.jpg", framePath);
        Assert.InRange(storedTimestamp, before, after);
    }

    [Fact]
    public void Apply_blockiert_frame_und_timestamp_innerhalb_des_throttle_fensters()
    {
        var lastUpdatedUtc = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var framePath = "old.jpg";
        var storedTimestamp = lastUpdatedUtc;

        TrainingLiveFrameThrottleController.Apply(
            @"C:\frames\b.jpg",
            () => storedTimestamp,
            value => storedTimestamp = value,
            value => framePath = value,
            () => lastUpdatedUtc.AddMilliseconds(50));

        Assert.Equal("old.jpg", framePath);
        Assert.Equal(lastUpdatedUtc, storedTimestamp);
    }
}
