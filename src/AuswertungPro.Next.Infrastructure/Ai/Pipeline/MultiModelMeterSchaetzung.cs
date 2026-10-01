using System;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Lineare Meterschaetzung der Mehrmodell-Analyse fuer Bilder ohne uebernommenen OSD-Meter.
/// Rate: angenommene Haltungslaenge je Videodauer (Meter je Sekunde).
/// Entscheid Pascal 01.10.2026: Nach einem uebernommenen (belegten) OSD-Meter zaehlt die
/// Schaetzung mit derselben Rate von diesem Anker aus weiter, statt auf die vom Videoanfang
/// gerechnete Gerade zurueckzuspringen. Der Anker ist derselbe wie bei der 5-m/s-Pruefung
/// (<see cref="MultiModelLaufZustand.LetzterOsdMeter"/>); ein verworfener OSD-Wert ist keiner.
/// </summary>
internal static class MultiModelMeterSchaetzung
{
    /// <summary>Neuer laufender Meterstand (ungerundet) fuer die Bildzeit <paramref name="t"/>.</summary>
    public static double Schaetze(double t, double dauerSek, double haltungslaengeM, double laufenderMeter,
        (double Meter, double ZeitSek)? anker)
    {
        if (anker is { } a)
        {
            // Mit Anker zaehlt nur er: Der laufende Meterstand wurde bei der Uebernahme auf den
            // OSD-Wert gesetzt und waechst seither nur ueber diese Schaetzung. Ein hoeherer Wert
            // stammt aus der Zeit vor dem Anker und darf ihn nicht ueberstimmen. Nie unter den Anker.
            return a.Meter + Math.Max(0.0, t - a.ZeitSek) / Math.Max(dauerSek, 1.0) * haltungslaengeM;
        }

        // Ohne belegten OSD-Meter unveraendert: Gerade ab Videoanfang, nie rueckwaerts.
        var linear = t / Math.Max(dauerSek, 1.0) * haltungslaengeM;
        return Math.Max(laufenderMeter, linear);
    }
}
