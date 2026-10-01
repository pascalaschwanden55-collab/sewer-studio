using System;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Lineare Meterschaetzung der Mehrmodell-Analyse fuer Bilder ohne uebernommenen OSD-Meter.
/// Rate: angenommene Haltungslaenge je Videodauer (Meter je Sekunde).
/// Entscheid Pascal 01.10.2026: Nach einem uebernommenen (belegten) OSD-Meter zaehlt die
/// Schaetzung mit derselben Rate von diesem Anker aus weiter, statt auf die vom Videoanfang
/// gerechnete Gerade zurueckzuspringen. Der Anker ist derselbe wie bei der 5-m/s-Pruefung
/// (<see cref="MultiModelLaufZustand.LetzterOsdMeter"/>); ein verworfener OSD-Wert ist keiner.
/// Zweiter Entscheid 01.10.2026: Ab zwei belegten OSD-Metern gilt die gemessene Geschwindigkeit
/// zwischen den beiden letzten (<see cref="GemesseneRate"/>), sonst die angenommene Rate.
/// </summary>
internal static class MultiModelMeterSchaetzung
{
    /// <summary>
    /// Mindestabstand der beiden Anker fuer eine gemessene Rate. Jeder OSD-Wert ist auf
    /// <see cref="MultiModelQwenSchritt.OsdMeterRundungM"/> (1 cm) gerundet, ihre Differenz also um
    /// hoechstens 1 cm falsch; die Rate damit um hoechstens 1 cm / Abstand. Ab 1 s sind das
    /// hoechstens 1 cm/s: Die Schaetzung weicht durch die Rundung um hoechstens 1 cm je Sekunde
    /// seit dem Anker ab, also eine Anzeigestufe des OSD je Sekunde (beim gemessenen 0,5 m/s des
    /// Schnappschusses 2 %). Darunter waechst der Fehler umgekehrt zum Abstand (0,1 s: 10 cm/s).
    /// Im Normalbetrieb (Bildabstand 3 s) liegen zwei Anker ohnehin mindestens 3 s auseinander.
    /// </summary>
    internal const double MindestAbstandSek = 1.0;

    /// <summary>Neuer laufender Meterstand (ungerundet) fuer die Bildzeit <paramref name="t"/>.</summary>
    public static double Schaetze(double t, double dauerSek, double haltungslaengeM, double laufenderMeter,
        (double Meter, double ZeitSek)? anker, (double Meter, double ZeitSek)? vorletzterAnker = null)
    {
        if (anker is { } a)
        {
            // Mit Anker zaehlt nur er: Der laufende Meterstand wurde bei der Uebernahme auf den
            // OSD-Wert gesetzt und waechst seither nur ueber diese Schaetzung. Ein hoeherer Wert
            // stammt aus der Zeit vor dem Anker und darf ihn nicht ueberstimmen. Nie unter den Anker.
            var seitAnker = Math.Max(0.0, t - a.ZeitSek);
            if (GemesseneRate(a, vorletzterAnker) is { } rate)
                return a.Meter + seitAnker * rate;
            return a.Meter + seitAnker / Math.Max(dauerSek, 1.0) * haltungslaengeM;
        }

        // Ohne belegten OSD-Meter unveraendert: Gerade ab Videoanfang, nie rueckwaerts.
        var linear = t / Math.Max(dauerSek, 1.0) * haltungslaengeM;
        return Math.Max(laufenderMeter, linear);
    }

    /// <summary>
    /// Gemessene Geschwindigkeit zwischen vorletztem und letztem belegten OSD-Meter, wenn sie
    /// belastbar ist; null = angenommene Rate verwenden (kein vorletzter Anker, Abstand unter
    /// <see cref="MindestAbstandSek"/> oder schneller als die Hoechstgeschwindigkeit der
    /// OSD-Folgepruefung). Rueckwaerts oder Stillstand ergibt 0: Die Schaetzung bleibt am Anker
    /// stehen und springt nicht zurueck.
    /// </summary>
    internal static double? GemesseneRate((double Meter, double ZeitSek) anker, (double Meter, double ZeitSek)? vorletzter)
    {
        if (vorletzter is not { } v)
            return null;
        var abstand = anker.ZeitSek - v.ZeitSek;
        if (abstand < MindestAbstandSek)
            return null;
        var rate = (anker.Meter - v.Meter) / abstand;
        if (rate <= 0.0)
            return 0.0;
        return rate <= MultiModelQwenSchritt.OsdFolgeGrenze.MaxMetersPerSecond ? rate : null;
    }
}
