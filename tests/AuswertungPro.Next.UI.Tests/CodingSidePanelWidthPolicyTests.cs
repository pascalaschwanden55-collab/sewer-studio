using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSidePanelWidthPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(-1, 0)]
    public void Resolve_returns_default_width_when_window_width_is_unusable(
        double actualWidth,
        double fallbackWidth)
    {
        var width = CodingSidePanelWidthPolicy.Resolve(actualWidth, fallbackWidth);

        Assert.Equal(760, width);
    }

    [Fact]
    public void Resolve_uses_fallback_width_when_actual_width_is_missing()
    {
        var width = CodingSidePanelWidthPolicy.Resolve(actualWidth: 0, fallbackWidth: 1800);

        Assert.Equal(828, width);
    }

    [Theory]
    [InlineData(1200, 760)]
    [InlineData(1800, 828)]
    [InlineData(2200, 840)]
    public void Resolve_clamps_percentage_width(
        double actualWidth,
        double expected)
    {
        var width = CodingSidePanelWidthPolicy.Resolve(actualWidth, fallbackWidth: 0);

        Assert.Equal(expected, width);
    }
}
