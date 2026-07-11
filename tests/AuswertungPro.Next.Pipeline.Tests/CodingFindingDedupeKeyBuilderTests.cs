using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingFindingDedupeKeyBuilderTests
{
    [Fact]
    public void Build_uses_rounded_bbox_center_when_bbox_is_complete()
    {
        var finding = Finding(
            positionClock: "12 Uhr",
            bboxX1: 0.74,
            bboxY1: 0.44,
            bboxX2: 0.86,
            bboxY2: 0.56);

        var expected = $"BCAEB@{0.8:F1},{0.5:F1}";

        Assert.Equal(expected, CodingFindingDedupeKeyBuilder.Build("BCAEB", finding));
    }

    [Fact]
    public void Build_uses_normalized_clock_when_bbox_is_missing()
    {
        var finding = Finding(positionClock: "rechts");

        Assert.Equal("BCAEB@3:00", CodingFindingDedupeKeyBuilder.Build("BCAEB", finding));
    }

    [Fact]
    public void Build_uses_unknown_marker_when_bbox_and_clock_are_missing()
    {
        var finding = Finding(positionClock: null);

        Assert.Equal("BAB@?", CodingFindingDedupeKeyBuilder.Build("BAB", finding));
    }

    private static LiveFrameFinding Finding(
        string? positionClock,
        double? bboxX1 = null,
        double? bboxY1 = null,
        double? bboxX2 = null,
        double? bboxY2 = null)
        => new(
            Label: "crack",
            Severity: 2,
            PositionClock: positionClock,
            ExtentPercent: null,
            VsaCodeHint: null,
            HeightMm: null,
            WidthMm: null,
            IntrusionPercent: null,
            CrossSectionReductionPercent: null,
            DiameterReductionMm: null,
            BboxX1: bboxX1,
            BboxY1: bboxY1,
            BboxX2: bboxX2,
            BboxY2: bboxY2);
}
