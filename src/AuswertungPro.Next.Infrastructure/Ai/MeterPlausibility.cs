namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Plausibilitaet von OSD-Meterstaenden. Kanallaengen liegen bei 0..500 m; groessere Werte sind
/// fast immer fehlgelesene Knotennummern (5+ stellig) oder Halluzinationen. Spiegelt die bereits
/// in LiveDetectionService genutzte 0..500-Pruefung, damit alle Pfade konsistent sind. (Audit R7)
/// </summary>
internal static class MeterPlausibility
{
    public const double MaxMeter = 500.0;

    /// <summary>True, wenn der Meter im plausiblen Bereich 0..500 m liegt.</summary>
    public static bool IsPlausible(double meter) => meter >= 0 && meter <= MaxMeter;

    /// <summary>Gibt den Meter nur zurueck, wenn plausibel; unplausible/fehlende Werte -> null.</summary>
    public static double? Sanitize(double? meter) =>
        meter is { } m && IsPlausible(m) ? m : null;
}
