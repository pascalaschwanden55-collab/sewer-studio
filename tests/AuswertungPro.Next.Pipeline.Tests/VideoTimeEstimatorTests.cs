using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests für VideoTimeEstimator.EstimateTime.
/// Stellt sicher, dass das IST-Verhalten der private static Methode in TrainingSampleGenerator erhalten bleibt.
/// </summary>
public sealed class VideoTimeEstimatorTests
{
    [Fact]
    public void EstimateTime_GibtNull_WennMaxMeterNull()
    {
        // maxMeter = 0: kein Verhältnis berechenbar → 0
        Assert.Equal(0, VideoTimeEstimator.EstimateTime(10, 0, 120));
    }

    [Fact]
    public void EstimateTime_GibtNull_WennMaxMeterNegativ()
    {
        Assert.Equal(0, VideoTimeEstimator.EstimateTime(10, -5, 120));
    }

    [Fact]
    public void EstimateTime_LinearesVerhältnis()
    {
        // 30m bei maxMeter=60m und duration=120s → 60s
        Assert.Equal(60.0, VideoTimeEstimator.EstimateTime(30, 60, 120), precision: 6);
    }

    [Fact]
    public void EstimateTime_KlemmtAufDurationMinus0_1()
    {
        // meter > maxMeter → wird auf duration - 0.1 begrenzt
        Assert.Equal(119.9, VideoTimeEstimator.EstimateTime(100, 60, 120), precision: 6);
    }

    [Fact]
    public void EstimateTime_KlemmtAufNull_BeiNegativemMeter()
    {
        // negativer Meterstand → 0 (Clamp)
        Assert.Equal(0, VideoTimeEstimator.EstimateTime(-5, 60, 120));
    }

    [Fact]
    public void EstimateTime_AmAnfang()
    {
        // meter = 0 → Zeitstempel = 0
        Assert.Equal(0, VideoTimeEstimator.EstimateTime(0, 60, 120));
    }

    [Fact]
    public void EstimateTime_AmEnde_KlemmtBei_DurationMinus0_1()
    {
        // meter = maxMeter → würde exakt duration ergeben, wird auf duration-0.1 begrenzt
        Assert.Equal(119.9, VideoTimeEstimator.EstimateTime(60, 60, 120), precision: 6);
    }
}
