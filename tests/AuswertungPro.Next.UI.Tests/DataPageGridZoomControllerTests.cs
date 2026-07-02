using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageGridZoomControllerTests
{
    [Fact]
    public void Resolve_returns_idle_without_control_modifier()
    {
        var result = DataPageGridZoomController.Resolve(
            currentZoom: 1.0,
            wheelDelta: 120,
            hasControlModifier: false);

        Assert.False(result.Handled);
        Assert.Equal(1.0, result.NextZoom);
    }

    [Theory]
    [InlineData(1.00, 120, 1.05)]
    [InlineData(1.00, -120, 0.95)]
    [InlineData(2.00, 120, 2.00)]
    [InlineData(0.50, -120, 0.50)]
    public void Resolve_applies_step_and_clamps_to_allowed_range(double current, int delta, double expected)
    {
        var result = DataPageGridZoomController.Resolve(
            currentZoom: current,
            wheelDelta: delta,
            hasControlModifier: true);

        Assert.Equal(expected, result.NextZoom, precision: 3);
        Assert.Equal(Math.Abs(expected - current) >= 0.001, result.Handled);
    }
}
