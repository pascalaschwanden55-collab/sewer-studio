using System;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Schätzt den Zeitstempel im Video anhand eines linearen Meter-zu-Zeit-Verhältnisses.
/// Reine statische Klasse ohne I/O oder Seiteneffekte.
/// </summary>
internal static class VideoTimeEstimator
{
    /// <summary>
    /// Schätzt den Video-Zeitstempel (in Sekunden) für einen gegebenen Meterstand.
    /// Die Schätzung basiert auf einer linearen Interpolation: t = meter/maxMeter * duration.
    /// Das Ergebnis wird auf [0, duration - 0.1] begrenzt, damit kein Frame
    /// am exakten Ende des Videos angefragt wird.
    /// </summary>
    /// <param name="meter">Gesuchter Meterstand.</param>
    /// <param name="maxMeter">Maximaler Meterstand der Haltung als Skalierungsgrundlage.</param>
    /// <param name="duration">Videodauer in Sekunden.</param>
    /// <returns>Geschätzter Zeitstempel in Sekunden.</returns>
    public static double EstimateTime(double meter, double maxMeter, double duration)
    {
        if (maxMeter <= 0) return 0;
        return Math.Clamp(meter / maxMeter * duration, 0, duration - 0.1);
    }
}
