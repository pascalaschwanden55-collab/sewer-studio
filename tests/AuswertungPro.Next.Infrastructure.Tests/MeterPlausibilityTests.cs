using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class MeterPlausibilityTests
{
    // Audit R7: OSD-Meter muss auf 0..500 m plausibilisiert werden, sonst laufen fehlgelesene
    // Knotennummern (5+ stellig) als Meterstand in die Timeline.
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(2.64, true)]
    [InlineData(500.0, true)]
    [InlineData(-0.1, false)]
    [InlineData(500.1, false)]
    [InlineData(81162.0, false)]   // fehlgelesene Knotennummer
    public void IsPlausible_BoundsZeroToFiveHundred(double meter, bool expected)
        => Assert.Equal(expected, MeterPlausibility.IsPlausible(meter));

    [Fact]
    public void Sanitize_NullsImplausibleAndNull_KeepsPlausible()
    {
        Assert.Equal(45.3, MeterPlausibility.Sanitize(45.3));
        Assert.Null(MeterPlausibility.Sanitize(81162.0));   // Knotennummer raus
        Assert.Null(MeterPlausibility.Sanitize(-5.0));
        Assert.Null(MeterPlausibility.Sanitize(null));
    }
}
