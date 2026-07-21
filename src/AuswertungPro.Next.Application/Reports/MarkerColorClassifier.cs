using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Klassifikations-Logik fuer Timeline-Marker: Konfidenz + Abgelehnt-Flag → Farbzone.
/// Aus <see cref="AuswertungPro.Next.UI.Controls.PipeGraphTimeline"/> extrahiert (verhaltensneutral),
/// damit unit-testbar ohne WPF-Abhaengigkeit.
/// </summary>
public static class MarkerColorClassifier
{
    /// <summary>Konfidenz-Schwelle fuer Gruen (einschliesslich).</summary>
    public const double ThresholdGreen = ConfidenceBandClassifier.HighThreshold;

    /// <summary>Konfidenz-Schwelle fuer Gelb (einschliesslich, unter Gruen).</summary>
    public const double ThresholdYellow = ConfidenceBandClassifier.MediumThreshold;

    /// <summary>
    /// Klassifiziert einen Marker anhand von Konfidenz und Abgelehnt-Status in eine Farbzone.
    /// </summary>
    /// <param name="confidence">
    /// Konfidenzwert 0..1 aus der KI-Pipeline, oder negativ wenn kein KI-Kontext vorhanden (= manueller Eintrag).
    /// </param>
    /// <param name="isRejected">True, wenn der Befund explizit abgelehnt wurde.</param>
    /// <returns>Die zugehoerige <see cref="MarkerZone"/>.</returns>
    public static MarkerZone Classify(double confidence, bool isRejected)
    {
        if (isRejected)
            return MarkerZone.Rejected;

        return ConfidenceBandClassifier.Classify(confidence) switch
        {
            ConfidenceBand.Missing => MarkerZone.Manual,
            ConfidenceBand.High => MarkerZone.Green,
            ConfidenceBand.Medium => MarkerZone.Yellow,
            _ => MarkerZone.Red
        };
    }
}

/// <summary>Farbzone eines Timeline-Markers gemaess QualityGate-Schwellen.</summary>
public enum MarkerZone
{
    /// <summary>Hohe Konfidenz (>= 0.85).</summary>
    Green,

    /// <summary>Mittlere Konfidenz (>= 0.60 und &lt; 0.85).</summary>
    Yellow,

    /// <summary>Niedrige Konfidenz (&lt; 0.60).</summary>
    Red,

    /// <summary>Befund abgelehnt.</summary>
    Rejected,

    /// <summary>Kein KI-Kontext vorhanden (manuell erfasst).</summary>
    Manual
}
