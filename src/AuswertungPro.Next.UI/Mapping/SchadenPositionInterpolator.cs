using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Meterstand eines Schadens -> Kartenpunkt entlang der Haltungslinie.
/// WebMercator ist auf 47 Grad Nord ~1.5-fach verzerrt — deshalb wird ueber den
/// ANTEIL an der Soll-Laenge (Haltungslaenge in echten Metern) interpoliert.
/// </summary>
public static class SchadenPositionInterpolator
{
    public static (double X, double Y)? Interpoliere(
        IReadOnlyList<(double X, double Y)> punkte,
        double meter,
        double? sollLaengeMeter)
    {
        if (punkte.Count < 2)
            return null;

        var istLaenge = PolylineMath.Laenge(punkte);
        if (istLaenge <= 0d)
            return null;

        double distanz;
        if (sollLaengeMeter is > 0d)
        {
            var anteil = Math.Clamp(meter / sollLaengeMeter.Value, 0d, 1d);
            distanz = istLaenge * anteil;
        }
        else
        {
            // Fallback ohne Soll-Laenge: Meter direkt als Mercator-Distanz klemmen.
            distanz = Math.Clamp(meter, 0d, istLaenge);
        }

        return PolylineMath.PunktBeiDistanz(punkte, distanz);
    }
}
