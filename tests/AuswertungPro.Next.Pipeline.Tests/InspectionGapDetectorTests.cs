using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class InspectionGapDetectorTests
{
    [Fact]
    public void DetectUnknownGaps_returns_gap_between_abort_and_counter_inspection_start()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BDCAD", MeterStart = 10 },
            new ProtocolEntry { Code = "BDBD", MeterStart = 17 },
            new ProtocolEntry { Code = "BCD", MeterStart = 37 }
        };

        var gap = Assert.Single(InspectionGapDetector.DetectUnknownGaps(entries, 37));

        Assert.Equal(10, gap.StartMeter);
        Assert.Equal(17, gap.EndMeter);
        Assert.Equal(7, gap.Length);
    }

    [Fact]
    public void DetectUnknownGaps_returns_no_gap_when_counter_starts_at_abort_meter()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BDCAD", MeterStart = 13.9 },
            new ProtocolEntry { Code = "BDBD", MeterStart = 13.9 },
            new ProtocolEntry { Code = "BCD", MeterStart = 37 }
        };

        Assert.Empty(InspectionGapDetector.DetectUnknownGaps(entries, 37));
    }

    [Fact]
    public void DetectUnknownGaps_marks_remaining_length_when_abort_has_no_counterpart()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BDCAD", MeterStart = 12 }
        };

        var gap = Assert.Single(InspectionGapDetector.DetectUnknownGaps(entries, 37));

        Assert.Equal(12, gap.StartMeter);
        Assert.Equal(37, gap.EndMeter);
    }
}
