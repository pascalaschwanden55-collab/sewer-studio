using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Geometrie-Basis fuer Karten-Features: Laenge, Punkt und Richtung entlang einer Polyline.
/// Pure Statik ohne Mapsui-Typen — testbar (PolylineMathTests).
/// </summary>
public static class PolylineMath
{
    public static double Laenge(IReadOnlyList<(double X, double Y)> punkte)
    {
        if (punkte.Count < 2)
            return 0d;

        var summe = 0d;
        for (var i = 1; i < punkte.Count; i++)
            summe += Distanz(punkte[i - 1], punkte[i]);
        return summe;
    }

    /// <summary>Punkt bei einer Distanz ab Linienanfang; klemmt auf [0, Laenge]. null bei &lt;2 Punkten.</summary>
    public static (double X, double Y)? PunktBeiDistanz(IReadOnlyList<(double X, double Y)> punkte, double distanz)
    {
        if (punkte.Count < 2)
            return null;

        if (distanz <= 0d)
            return punkte[0];

        var rest = distanz;
        for (var i = 1; i < punkte.Count; i++)
        {
            var segment = Distanz(punkte[i - 1], punkte[i]);
            if (segment <= 0d)
                continue;

            if (rest <= segment)
            {
                var t = rest / segment;
                return (
                    punkte[i - 1].X + (punkte[i].X - punkte[i - 1].X) * t,
                    punkte[i - 1].Y + (punkte[i].Y - punkte[i - 1].Y) * t);
            }

            rest -= segment;
        }

        return punkte[^1]; // Ueberlaenge: ans Ende klemmen
    }

    public static (double X, double Y)? PunktBeiAnteil(IReadOnlyList<(double X, double Y)> punkte, double anteil)
        => PunktBeiDistanz(punkte, Laenge(punkte) * Math.Clamp(anteil, 0d, 1d));

    /// <summary>Peilung (Grad, atan2-Konvention: 0 = +X, 90 = +Y) des Segments am gegebenen Anteil.</summary>
    public static double? RichtungGradBeiAnteil(IReadOnlyList<(double X, double Y)> punkte, double anteil)
    {
        if (punkte.Count < 2)
            return null;

        var ziel = Laenge(punkte) * Math.Clamp(anteil, 0d, 1d);
        var gelaufen = 0d;
        for (var i = 1; i < punkte.Count; i++)
        {
            var segment = Distanz(punkte[i - 1], punkte[i]);
            if (segment <= 0d)
                continue;

            if (ziel <= gelaufen + segment)
                return RichtungGrad(punkte[i - 1], punkte[i]);
            gelaufen += segment;
        }

        return RichtungGrad(punkte[^2], punkte[^1]);
    }

    /// <summary>Minimale Distanz eines Punkts zur Polyline (Lot aufs Segment, an den Enden geklemmt).</summary>
    public static double DistanzZuPunkt(IReadOnlyList<(double X, double Y)> punkte, (double X, double Y) p)
    {
        if (punkte.Count == 0)
            return double.PositiveInfinity;
        if (punkte.Count == 1)
            return Distanz(punkte[0], p);

        var min = double.PositiveInfinity;
        for (var i = 1; i < punkte.Count; i++)
        {
            var a = punkte[i - 1];
            var b = punkte[i];
            var abX = b.X - a.X;
            var abY = b.Y - a.Y;
            var laengeQuadrat = abX * abX + abY * abY;

            double t = laengeQuadrat <= 0d
                ? 0d
                : Math.Clamp(((p.X - a.X) * abX + (p.Y - a.Y) * abY) / laengeQuadrat, 0d, 1d);

            var lot = (a.X + abX * t, a.Y + abY * t);
            min = Math.Min(min, Distanz(lot, p));
        }

        return min;
    }

    private static double RichtungGrad((double X, double Y) von, (double X, double Y) nach)
        => Math.Atan2(nach.Y - von.Y, nach.X - von.X) * 180d / Math.PI;

    private static double Distanz((double X, double Y) a, (double X, double Y) b)
        => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
