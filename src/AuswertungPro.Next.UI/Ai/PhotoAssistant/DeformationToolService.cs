using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AuswertungPro.Next.UI.Ai.PhotoAssistant;

/// <summary>
/// Werkzeug "Deformation" (BAA) — 16-Punkt-Ringkreis.
///
/// Ist-Polygon = 16 Stuetzpunkte gleichmaessig auf dem Sollkreis (alle 22.5°).
/// Pro Punkt ein Radius-Faktor in [0.2, 1.0]: 1.0 = auf Sollkreis, 0.2 = stark eingedrueckt.
///
/// Querschnitt-Berechnung (Polygonflaeche im polaren Trapezmodell):
///   istFlaeche = Sigma_{i=0..15} 0.5 * r[i] * r[(i+1) mod 16] * sin(2*pi/16)
///   sollFlaeche = pi
///   querschnittProzent = istFlaeche / sollFlaeche * 100
/// </summary>
public static class DeformationToolService
{
    /// <summary>Anzahl Stuetzpunkte (alle 22.5°).</summary>
    public const int PointCount = 16;

    /// <summary>Minimaler Radius-Faktor (verhindert Polygon-Zentrum-Kollaps).</summary>
    public const double MinRadius = 0.2;

    /// <summary>Maximaler Radius-Faktor (= Sollkreis).</summary>
    public const double MaxRadius = 1.0;

    /// <summary>
    /// Berechnet den Querschnitt in Prozent (100 = perfekter Kreis).
    /// </summary>
    public static double ComputeQuerschnittPercent(IReadOnlyList<double> radii)
    {
        if (radii is null) throw new ArgumentNullException(nameof(radii));
        if (radii.Count != PointCount)
            throw new ArgumentException($"Genau {PointCount} Radien erwartet, erhalten: {radii.Count}.");

        var step = 2.0 * Math.PI / PointCount;
        var sinStep = Math.Sin(step);

        double area = 0;
        for (var i = 0; i < PointCount; i++)
        {
            var a = ClampRadius(radii[i]);
            var b = ClampRadius(radii[(i + 1) % PointCount]);
            area += 0.5 * a * b * sinStep;
        }

        var sollFlaeche = Math.PI;
        var prozent = area / sollFlaeche * 100.0;
        return prozent;
    }

    /// <summary>
    /// Liefert die 16 Stuetzpunkt-Positionen in Bildraum-Koordinaten (Pixel).
    /// Index 0 = 12h (oben), 4 = 3h (rechts), 8 = 6h (unten), 12 = 9h (links).
    /// </summary>
    public static IReadOnlyList<Point> ComputePoints(
        Point center, double baseRadius, IReadOnlyList<double> radii)
    {
        if (radii is null) throw new ArgumentNullException(nameof(radii));
        if (radii.Count != PointCount)
            throw new ArgumentException($"Genau {PointCount} Radien erwartet, erhalten: {radii.Count}.");

        var pts = new Point[PointCount];
        for (var i = 0; i < PointCount; i++)
        {
            // 12h = oben, also bei Winkel -90°. Im Uhrzeigersinn weiter.
            var angle = (i * 360.0 / PointCount - 90.0) * Math.PI / 180.0;
            var r = ClampRadius(radii[i]) * baseRadius;
            pts[i] = new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r);
        }
        return pts;
    }

    /// <summary>Erzeugt ein Default-Radien-Array (alle 1.0 = perfekter Kreis).</summary>
    public static double[] CreateDefaultRadii()
    {
        var arr = new double[PointCount];
        for (var i = 0; i < PointCount; i++) arr[i] = 1.0;
        return arr;
    }

    private static double ClampRadius(double r)
    {
        if (r < MinRadius) return MinRadius;
        if (r > MaxRadius) return MaxRadius;
        return r;
    }
}
