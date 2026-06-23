using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingResultMeterReadingPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(12.34)]
    [InlineData(500)]
    public void TryAccept_accepts_plausible_osd_meter(double meter)
    {
        var result = new LiveDetection(
            TimestampSeconds: 7.5,
            Findings: [],
            MeterReading: meter,
            Error: null);

        var accepted = CodingResultMeterReadingPolicy.TryAccept(result, out var reading);

        Assert.True(accepted);
        Assert.Equal(meter, reading.Meter);
        Assert.Equal(7.5, reading.TimestampSeconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-0.01)]
    [InlineData(500.01)]
    public void TryAccept_rejects_missing_or_implausible_osd_meter(double? meter)
    {
        var result = new LiveDetection(
            TimestampSeconds: 7.5,
            Findings: [],
            MeterReading: meter,
            Error: null);

        var accepted = CodingResultMeterReadingPolicy.TryAccept(result, out var reading);

        Assert.False(accepted);
        Assert.Equal(default, reading);
    }
}
