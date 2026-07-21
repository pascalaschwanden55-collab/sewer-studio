namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Ordnet eine KI-Konfidenz einem neutralen Anzeigebereich zu.
/// Fachliche Zustaende wie "abgelehnt" bleiben bei den jeweiligen Verbrauchern.
/// </summary>
public static class ConfidenceBandClassifier
{
    public const double HighThreshold = 0.85;
    public const double MediumThreshold = 0.60;

    public static ConfidenceBand Classify(double confidence)
    {
        if (confidence < 0)
            return ConfidenceBand.Missing;

        if (confidence >= HighThreshold)
            return ConfidenceBand.High;

        return confidence >= MediumThreshold
            ? ConfidenceBand.Medium
            : ConfidenceBand.Low;
    }
}

public enum ConfidenceBand
{
    Missing,
    Low,
    Medium,
    High
}
