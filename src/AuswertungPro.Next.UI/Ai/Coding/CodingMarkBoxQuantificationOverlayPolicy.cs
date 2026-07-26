using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingMarkBoxQuantificationOverlayPolicy
{
    public static void Apply(
        OverlayGeometry overlay,
        MaskQuantificationService.QuantifiedMask quantification)
    {
        if (quantification.HeightMm.HasValue)
            overlay.Q1Mm = quantification.HeightMm.Value;

        if (quantification.WidthMm.HasValue)
            overlay.Q2Mm = quantification.WidthMm.Value;

        var crossSectionPercent = quantification.CrossSectionReductionPercent
                                  ?? quantification.ExtentPercent;
        if (crossSectionPercent.HasValue)
            overlay.FillPercent = crossSectionPercent.Value;

        if (!string.IsNullOrEmpty(quantification.ClockPosition)
            && double.TryParse(
                quantification.ClockPosition,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var clockPosition))
        {
            overlay.ClockFrom = clockPosition;
        }
    }
}
