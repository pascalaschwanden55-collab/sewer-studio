using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TimelineScaleCalculatorTests
{
    [Theory]
    [InlineData(10, 2)]
    [InlineData(25, 5)]
    [InlineData(50, 10)]
    [InlineData(100, 20)]
    [InlineData(250, 50)]
    [InlineData(251, 100)]
    public void ChooseInterval_keeps_existing_length_thresholds(double totalLength, double expectedInterval)
        => Assert.Equal(expectedInterval, TimelineScaleCalculator.ChooseInterval(totalLength));

    [Theory]
    [InlineData(-1, 10, 400, 0)]
    [InlineData(5, 10, 400, 200)]
    [InlineData(12, 10, 400, 400)]
    [InlineData(5, 10, 0, 200)]
    public void MeterToX_clamps_meter_and_uses_400_width_fallback(
        double meter,
        double totalLength,
        double canvasWidth,
        double expectedX)
        => Assert.Equal(expectedX, TimelineScaleCalculator.MeterToX(meter, totalLength, canvasWidth));

    [Fact]
    public void XToMeter_clamps_click_position_and_rejects_missing_width()
    {
        Assert.Equal(0, TimelineScaleCalculator.XToMeter(-10, totalLength: 10, canvasWidth: 400));
        Assert.Equal(5, TimelineScaleCalculator.XToMeter(200, totalLength: 10, canvasWidth: 400));
        Assert.Equal(10, TimelineScaleCalculator.XToMeter(500, totalLength: 10, canvasWidth: 400));
        Assert.Null(TimelineScaleCalculator.XToMeter(200, totalLength: 10, canvasWidth: 0));
    }

    [Fact]
    public void BuildTicks_keeps_current_labels_positions_and_duplicate_zero()
    {
        var ticks = TimelineScaleCalculator.BuildTicks(totalLength: 9, canvasWidth: 90);

        Assert.Equal(["0m", "0m", "2m", "4m", "6m", "9.0m"], ticks.Select(t => t.Text));
        Assert.Equal([false, false, false, false, false, true], ticks.Select(t => t.AlignRight));
        Assert.Equal([0d, 0d, 12d, 32d, 52d, 0d], ticks.Select(t => t.Left));
    }

    [Fact]
    public void BuildTicks_returns_empty_for_non_positive_length()
    {
        Assert.Empty(TimelineScaleCalculator.BuildTicks(0, 400));
        Assert.Empty(TimelineScaleCalculator.BuildTicks(-1, 400));
    }
}
