using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSegmentedFindingVisibilityTests
{
    [Fact]
    public void BuildVisibleCodingFindings_keeps_codierbar_non_background_mask()
    {
        var visible = CodingSegmentedFindingVisibility.BuildVisibleCodingFindings(
            [Finding("crack", MetrierungProximity.Codierbar)]);

        Assert.Single(visible);
        Assert.Equal("crack", visible[0].Mask.Label);
    }

    [Fact]
    public void BuildVisibleMaskFindings_hides_confirmed_background_mask()
    {
        var visible = CodingSegmentedFindingVisibility.BuildVisibleMaskFindings(
            [Finding("water wall", MetrierungProximity.Codierbar, areaRatio: 0.95, confidence: 0.98, dinoConfidence: 0.32)]);

        Assert.Empty(visible);
    }

    [Fact]
    public void BuildVisibleMaskRenderCandidates_maps_visible_masks_for_renderer()
    {
        var candidate = Assert.Single(CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates(
            [Finding("crack", MetrierungProximity.Codierbar, dinoConfidence: 0.72)]));

        Assert.Equal("crack", candidate.Mask.Label);
        Assert.Equal("crack", candidate.Quant?.Label);
        Assert.Equal(0.72, candidate.DetectionConfidence);
    }

    [Fact]
    public void BuildVisibleMaskRenderCandidates_hides_background_masks()
    {
        var candidates = CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates(
            [Finding("water wall", MetrierungProximity.Codierbar, areaRatio: 0.95, confidence: 0.98, dinoConfidence: 0.32)]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void BuildVisibleCodingFindings_excludes_ahead_findings()
    {
        var visible = CodingSegmentedFindingVisibility.BuildVisibleCodingFindings(
            [Finding("crack", MetrierungProximity.Voraus)]);

        Assert.Empty(visible);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "1 Hintergrundmaske ausgeblendet")]
    [InlineData(2, "2 Hintergrundmasken ausgeblendet")]
    public void BuildOverlaySuppressionText_formats_count(int count, string expected)
    {
        Assert.Equal(expected, CodingSegmentedFindingVisibility.BuildOverlaySuppressionText(count));
    }

    private static SegmentedFinding Finding(
        string label,
        MetrierungProximity proximity,
        double areaRatio = 0.01,
        double confidence = 0.9,
        double? dinoConfidence = 0.7)
    {
        var imageArea = 10_000;
        var maskArea = (int)Math.Round(imageArea * areaRatio);
        var mask = new SamMaskResult(
            Label: label,
            Confidence: confidence,
            Bbox: [0, 0, 100, 100],
            MaskRle: "0",
            MaskAreaPixels: maskArea,
            ImageAreaPixels: imageArea,
            HeightPixels: 100,
            WidthPixels: 100,
            CentroidX: 50,
            CentroidY: 50);
        var quant = new MaskQuantificationService.QuantifiedMask(
            label,
            confidence,
            null,
            null,
            null,
            null,
            null,
            null);
        var dino = dinoConfidence.HasValue
            ? new DinoDetectionDto(0, 0, 100, 100, label, dinoConfidence.Value, label)
            : null;
        var proximityResult = new MetrierungProximityResult(proximity, "", 0, 0, 0, false, false);
        return new SegmentedFinding(dino, mask, quant, proximityResult);
    }
}
