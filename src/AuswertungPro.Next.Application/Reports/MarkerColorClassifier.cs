namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Klassifikations-Logik fuer Timeline-Marker: Konfidenz + Abgelehnt-Flag → Farbzone.
/// Aus <see cref="AuswertungPro.Next.UI.Controls.PipeGraphTimeline"/> extrahiert (verhaltensneutral),
/// damit unit-testbar ohne WPF-Abhaengigkeit.
/// </summary>
public static class MarkerColorClassifier
{
    /// <summary>Konfidenz-Schwelle fuer Gruen (einschliesslich).</summary>
    public const double ThresholdGreen = 0.85;

    /// <summary>Konfidenz-Schwelle fuer Gelb (einschliesslich, unter Gruen).</summary>
    public const double ThresholdYellow = 0.60;

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

        if (confidence < 0)
            return MarkerZone.Manual; // Kein KI-Kontext → manueller Eintrag

        if (confidence >= ThresholdGreen)
            return MarkerZone.Green;

        if (confidence >= ThresholdYellow)
            return MarkerZone.Yellow;

        return MarkerZone.Red;
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
