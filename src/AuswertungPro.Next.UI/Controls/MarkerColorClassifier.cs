namespace AuswertungPro.Next.UI.Controls;

internal enum MarkerColorKind
{
    Green,
    Yellow,
    Red,
    Rejected,
    Manual
}

internal static class MarkerColorClassifier
{
    public const double GreenThreshold = 0.85;
    public const double YellowThreshold = 0.60;

    public static MarkerColorKind Classify(bool isRejected, double confidence)
    {
        if (isRejected)
            return MarkerColorKind.Rejected;

        if (confidence < 0)
            return MarkerColorKind.Manual;

        if (confidence >= GreenThreshold)
            return MarkerColorKind.Green;

        return confidence >= YellowThreshold
            ? MarkerColorKind.Yellow
            : MarkerColorKind.Red;
    }
}
