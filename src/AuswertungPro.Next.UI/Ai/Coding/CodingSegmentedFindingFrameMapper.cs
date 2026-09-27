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
        var origin = segmented.Origin;

        return new LiveFrameFinding(
            Label: quant.Label,
            Severity: QuantificationSeverityPolicy.Estimate(
                quant.CrossSectionReductionPercent,
                quant.IntrusionPercent,
                quant.HeightMm,
                quant.ExtentPercent),
            PositionClock: VsaCodeResolver.NormalizeClock(quant.ClockPosition),
            ExtentPercent: quant.ExtentPercent,
            VsaCodeHint: origin?.HasYolo == true ? origin.VsaMainCode : null,
            HeightMm: quant.HeightMm,
            WidthMm: quant.WidthMm,
            IntrusionPercent: quant.IntrusionPercent,
            CrossSectionReductionPercent: quant.CrossSectionReductionPercent,
            DiameterReductionMm: null,
            BboxX1: imageWidth > 0 ? (origin?.X1 ?? dino?.X1) / imageWidth : null,
            BboxY1: imageHeight > 0 ? (origin?.Y1 ?? dino?.Y1) / imageHeight : null,
            BboxX2: imageWidth > 0 ? (origin?.X2 ?? dino?.X2) / imageWidth : null,
            BboxY2: imageHeight > 0 ? (origin?.Y2 ?? dino?.Y2) / imageHeight : null);
    }
}
