namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Meterdistanz-Gewichtung fuer KB-Treffer: Treffer nahe dem aktuellen Meterstand werden
/// hoeher gewichtet als weit entfernte. Formel: max(0.35, 1 - min(1, |dist| / 12)).
/// Identisch in OllamaProtocolAiService und FullProtocolGenerationService verwendet.
/// </summary>
internal static class KbMeterWeighting
{
    /// <summary>
    /// Berechnet den Metergewicht-Faktor fuer einen KB-Treffer.
    /// </summary>
    /// <param name="queryMeter">Aktueller Meterstand der Anfrage.</param>
    /// <param name="sampleMid">Mittelpunkt des KB-Samples (MeterStart + MeterEnd) / 2.</param>
    /// <returns>Gewicht im Bereich [0.35, 1.0].</returns>
    public static double Weight(double queryMeter, double sampleMid)
        => Math.Max(0.35, 1.0 - Math.Min(1.0, Math.Abs(queryMeter - sampleMid) / 12.0));
}
