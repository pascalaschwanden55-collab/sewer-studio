namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Heuristische Schwere-Schaetzung (1..5) fuer eine quantifizierte SAM-Maske.
/// Aus <c>MultiModelAnalysisService.Helpers.cs</c> migriert (Audit 2026-05-13 M1).
/// </summary>
/// <remarks>
/// Schwellwerte sind bewusst konservativ; ein Wert ueber 50% Querschnitts-
/// reduktion ist nach VSA-KEK ein Sofortmassnahmen-Fall (5). Die Reihenfolge
/// der Bedingungen ist semantisch relevant — strengster Treffer gewinnt.
/// </remarks>
public static class SeverityEstimator
{
    /// <summary>
    /// Schaetzt eine VSA-Severity (1..5) aus den physischen Massen einer Maske.
    /// </summary>
    public static int Estimate(MaskQuantificationService.QuantifiedMask q)
    {
        if (q.CrossSectionReductionPercent is > 50) return 5;
        if (q.CrossSectionReductionPercent is > 25) return 4;
        if (q.ExtentPercent is > 50) return 4;
        if (q.HeightMm is > 50) return 3;
        if (q.ExtentPercent is > 25) return 3;
        if (q.HeightMm is > 10) return 2;
        return 1;
    }
}
