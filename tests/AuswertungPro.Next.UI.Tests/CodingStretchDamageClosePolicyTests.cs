using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStretchDamageClosePolicyTests
{
    [Theory]
    [InlineData(5.00, 5.00)]
    [InlineData(5.00, 5.01)]
    public void CanClose_returns_false_when_current_meter_is_not_after_start_tolerance(double startMeter, double currentMeter)
    {
        Assert.False(CodingStretchDamageClosePolicy.CanClose(startMeter, currentMeter));
    }

    [Fact]
    public void CanClose_returns_true_when_current_meter_is_after_start_tolerance()
    {
        Assert.True(CodingStretchDamageClosePolicy.CanClose(startMeter: 5.00, currentMeter: 5.02));
    }

    [Fact]
    public void BuildClosedStatusText_formats_code_and_meter_range()
    {
        var text = CodingStretchDamageClosePolicy.BuildClosedStatusText("BAJ", startMeter: 2.3, endMeter: 8.5);

        Assert.Equal("Streckenschaden geschlossen: BAJ 2.30m - 8.50m", text);
    }
}
