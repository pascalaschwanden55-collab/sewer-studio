using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingQuantificationSeverityPolicy
{
    public static int Estimate(MaskQuantificationService.QuantifiedMask quantification)
    {
        if (quantification.CrossSectionReductionPercent is > 30)
            return 5;
        if (quantification.CrossSectionReductionPercent is > 15)
            return 4;
        if (quantification.CrossSectionReductionPercent is > 5)
            return 3;

        if (quantification.IntrusionPercent is > 20)
            return 4;
        if (quantification.IntrusionPercent is > 10)
            return 3;

        if (quantification.HeightMm is > 50)
            return 3;
        if (quantification.HeightMm is > 20)
            return 2;

        return 2;
    }
}
