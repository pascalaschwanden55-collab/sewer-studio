using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingSegmentedFindingFrameMapper
{
    public static LiveFrameFinding Build(
        SegmentedFinding segmented,
        double imageWidth,
        double imageHeight)
    {
        var quant = segmented.Quant;
        var dino = segmented.Dino;

        return new LiveFrameFinding(
            Label: quant.Label,
            Severity: QuantificationSeverityPolicy.Estimate(
                quant.CrossSectionReductionPercent,
                quant.IntrusionPercent,
                quant.HeightMm,
                quant.ExtentPercent),
            PositionClock: VsaCodeResolver.NormalizeClock(quant.ClockPosition),
            ExtentPercent: quant.ExtentPercent,
            VsaCodeHint: null,
            HeightMm: quant.HeightMm,
            WidthMm: quant.WidthMm,
            IntrusionPercent: quant.IntrusionPercent,
            CrossSectionReductionPercent: quant.CrossSectionReductionPercent,
            DiameterReductionMm: null,
            BboxX1: dino != null && imageWidth > 0 ? dino.X1 / imageWidth : null,
            BboxY1: dino != null && imageHeight > 0 ? dino.Y1 / imageHeight : null,
            BboxX2: dino != null && imageWidth > 0 ? dino.X2 / imageWidth : null,
            BboxY2: dino != null && imageHeight > 0 ? dino.Y2 / imageHeight : null);
    }
}
