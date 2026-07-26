using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelFindingSummaryTests
{
    [Fact]
    public void Build_counts_visible_codierbar_and_ahead_findings()
    {
        var summary = CodingMultiModelFindingSummary.Build(
            [
                Finding("crack", MetrierungProximity.Codierbar),
                Finding("root ahead", MetrierungProximity.Voraus)
            ],
            Result(12, 34, 56));

        Assert.Equal(2, summary.TotalCount);
        Assert.Equal(1, summary.CodierbarCount);
        Assert.Equal(1, summary.VorausCount);
        Assert.Single(summary.VisibleCodierbar);
        Assert.False(summary.HasNoSegmentedFindings);
        Assert.False(summary.HasOnlyAheadFindings);
        Assert.Equal("1 Befunde erkannt (1 voraus ignoriert)", summary.DetectedStatusText);
        Assert.Equal("YOLO 12ms | DINO 34ms | SAM 56ms", summary.TimingText);
    }

    [Fact]
    public void Build_reports_only_ahead_findings()
    {
        var summary = CodingMultiModelFindingSummary.Build(
            [Finding("root ahead", MetrierungProximity.Voraus)],
            Result());

        Assert.True(summary.HasOnlyAheadFindings);
        Assert.Empty(summary.VisibleCodierbar);
    }

    [Fact]
    public void Build_reports_suppressed_background_masks_in_timing()
    {
        var summary = CodingMultiModelFindingSummary.Build(
            [
                Finding("water wall", MetrierungProximity.Codierbar, areaRatio: 0.95, confidence: 0.98, dinoConfidence: 0.32),
                Finding("crack", MetrierungProximity.Codierbar)
            ],
            Result(1.4, 2.5, 3.6));

        Assert.Equal(1, summary.SuppressedBackgroundCount);
        Assert.Equal("1 Hintergrundmaske ausgeblendet", summary.OverlaySuppressionText);
        Assert.Single(summary.VisibleCodierbar);
        Assert.Equal("YOLO 1ms | DINO 2ms | SAM 4ms | 1 Hintergrundmaske ausgeblendet", summary.TimingText);
    }

    [Fact]
    public void Build_marks_empty_segmented_results()
    {
        var summary = CodingMultiModelFindingSummary.Build([], Result());

        Assert.True(summary.HasNoSegmentedFindings);
        Assert.False(summary.HasOnlyAheadFindings);
        Assert.Empty(summary.VisibleCodierbar);
    }

    private static SingleFrameResult Result(double yolo = 0, double dino = 0, double sam = 0)
        => new(
            true,
            Array.Empty<DinoDetectionDto>(),
            null,
            Array.Empty<MaskQuantificationService.QuantifiedMask>(),
            yolo,
            dino,
            sam,
            null);

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
