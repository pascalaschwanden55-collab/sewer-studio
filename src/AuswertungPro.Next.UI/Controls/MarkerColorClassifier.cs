using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Controls;

// Public: wird auch vom HaltungSchadensbandBuilder (Schadensband der Haltungsansicht) geliefert.
public enum MarkerColorKind
{
    Green,
    Yellow,
    Red,
    Rejected,
    Manual
}

internal static class MarkerColorClassifier
{
    public const double GreenThreshold = ConfidenceBandClassifier.HighThreshold;
    public const double YellowThreshold = ConfidenceBandClassifier.MediumThreshold;

    public static MarkerColorKind Classify(bool isRejected, double confidence)
    {
        if (isRejected)
            return MarkerColorKind.Rejected;

        return ConfidenceBandClassifier.Classify(confidence) switch
        {
            ConfidenceBand.Missing => MarkerColorKind.Manual,
            ConfidenceBand.High => MarkerColorKind.Green,
            ConfidenceBand.Medium => MarkerColorKind.Yellow,
            _ => MarkerColorKind.Red
        };
    }
}
