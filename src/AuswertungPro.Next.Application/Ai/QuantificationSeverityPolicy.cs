namespace AuswertungPro.Next.Application.Ai;

public static class QuantificationSeverityPolicy
{
    public static int Estimate(
        double? crossSectionReductionPercent,
        double? intrusionPercent,
        double? heightMm,
        double? extentPercent = null)
    {
        if (crossSectionReductionPercent is > 30)
            return 5;
        if (crossSectionReductionPercent is > 15)
            return 4;
        if (crossSectionReductionPercent is > 5)
            return 3;

        if (intrusionPercent is > 20)
            return 4;
        if (intrusionPercent is > 10)
            return 3;

        if (extentPercent is > 50)
            return 4;
        if (extentPercent is > 25)
            return 3;

        if (heightMm is > 50)
            return 3;
        if (heightMm is > 20)
            return 2;

        return 2;
    }
}
