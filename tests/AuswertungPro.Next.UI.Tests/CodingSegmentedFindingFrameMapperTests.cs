using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSegmentedFindingFrameMapperTests
{
    [Fact]
    public void Build_maps_quantification_and_dino_bbox_to_live_frame_finding()
    {
        var finding = CodingSegmentedFindingFrameMapper.Build(
            Segmented(dino: new DinoDetectionDto(20, 10, 80, 50, "connection", 0.82, "connection")),
            imageWidth: 100,
            imageHeight: 200);

        Assert.Equal("connection", finding.Label);
        Assert.Equal(5, finding.Severity);
        Assert.Equal("12:00", finding.PositionClock);
        Assert.Equal(12, finding.HeightMm);
        Assert.Equal(8, finding.WidthMm);
        Assert.Equal(40, finding.CrossSectionReductionPercent);
        Assert.Equal(15, finding.ExtentPercent);
        Assert.Equal(0.2, finding.BboxX1);
        Assert.Equal(0.05, finding.BboxY1);
        Assert.Equal(0.8, finding.BboxX2);
        Assert.Equal(0.25, finding.BboxY2);
        Assert.Null(finding.VsaCodeHint);
    }

    [Fact]
    public void Build_leaves_bbox_empty_when_dino_is_missing()
    {
        var finding = CodingSegmentedFindingFrameMapper.Build(
            Segmented(dino: null),
            imageWidth: 100,
            imageHeight: 200);

        Assert.Null(finding.BboxX1);
        Assert.Null(finding.BboxY1);
        Assert.Null(finding.BboxX2);
        Assert.Null(finding.BboxY2);
    }

    private static SegmentedFinding Segmented(DinoDetectionDto? dino)
    {
        var mask = new SamMaskResult(
            "connection",
            0.87,
            [70, 40, 90, 60],
            "mask-rle",
            MaskAreaPixels: 100,
            ImageAreaPixels: 10_000,
            HeightPixels: 40,
            WidthPixels: 60,
            CentroidX: 40,
            CentroidY: 40);

        var quant = new MaskQuantificationService.QuantifiedMask(
            Label: "connection",
            Confidence: 0.87,
            HeightMm: 12,
            WidthMm: 8,
            ExtentPercent: 15,
            CrossSectionReductionPercent: 40,
            IntrusionPercent: null,
            ClockPosition: "oben");

        var proximity = new MetrierungProximityResult(
            MetrierungProximity.Codierbar,
            "test",
            FillRatio: 0,
            DistToVanish: 0,
            OuterRadius: 0,
            WandNaehe: true,
            EnthaeltCenter: false);

        return new SegmentedFinding(dino, mask, quant, proximity);
    }
}
