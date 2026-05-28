using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerTimelineLayoutCalculatorTests
{
    [Theory]
    [InlineData(0, 100, 20, 480, 20)]
    [InlineData(50, 100, 20, 480, 260)]
    [InlineData(100, 100, 20, 480, 500)]
    [InlineData(-10, 100, 20, 480, 20)]
    [InlineData(120, 100, 20, 480, 500)]
    public void CalculatePointX_clamps_meter_to_slider_track(double meter, double pipeLength, double offsetX, double trackWidth, double expectedX)
    {
        var x = PlayerTimelineLayoutCalculator.CalculatePointX(meter, pipeLength, offsetX, trackWidth);

        Assert.Equal(expectedX, x, precision: 3);
    }

    [Fact]
    public void CalculateRangeX_sorts_and_clamps_bounds()
    {
        var range = PlayerTimelineLayoutCalculator.CalculateRangeX(90, 10, 100, 20, 480);

        Assert.Equal(68, range.StartX, precision: 3);
        Assert.Equal(452, range.EndX, precision: 3);
        Assert.Equal(384, range.Width, precision: 3);
    }

    [Fact]
    public void CalculatePointX_returns_offset_when_geometry_is_invalid()
    {
        var x = PlayerTimelineLayoutCalculator.CalculatePointX(50, 0, 20, 480);

        Assert.Equal(20, x, precision: 3);
    }
}
