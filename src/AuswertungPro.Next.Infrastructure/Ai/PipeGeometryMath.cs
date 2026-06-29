using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Reine, kalibrierungsfreie Mathe-Hilfsmethoden fuer Rohrgeometrie.
/// Alle Methoden sind pure static (kein Zustand, kein IO).
/// Einzige Quelle fuer Kreissegment-, Bogen- und Umkreis-Berechnungen.
/// </summary>
public static class PipeGeometryMath
{
    // ═══════════════════════════════════════════════════════════════════
    // 1. Kreissegment-Prozentsatz
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet den Querschnitts-Prozentsatz eines Kreissegments.
    /// hRatio: Fuellhoehe relativ zum Durchmesser (0.0 = leer, 1.0 = voll).
    /// Ergebnis: 0.0–100.0 (Flaechenanteil in Prozent).
    /// </summary>
    public static double CircleSegmentPercent(double hRatio)
    {
        hRatio = Math.Clamp(hRatio, 0, 1);
        if (hRatio <= 0) return 0;
        if (hRatio >= 1) return 100;

        // Kreissegment-Formel mit R=0.5, h=hRatio*2R = hRatio
        double R = 0.5;
        double h = hRatio; // 0..1 entspricht 0..2R
        double cosArg = Math.Clamp((R - h) / R, -1, 1);
        double area = R * R * Math.Acos(cosArg) - (R - h) * Math.Sqrt(Math.Max(0, 2 * R * h - h * h));
        double fullArea = Math.PI * R * R;
        return area / fullArea * 100.0;
    }

    /// <summary>
    /// Umkehrfunktion von CircleSegmentPercent: Findet hRatio fuer gewuenschten %-Wert.
    /// Bisektions-Suche (50 Iterationen, Genauigkeit ~1e-15).
    /// </summary>
    public static double InverseCircleSegmentPercent(double targetPercent)
    {
        targetPercent = Math.Clamp(targetPercent, 0, 100);
        if (targetPercent <= 0) return 0;
        if (targetPercent >= 100) return 1;

        double lo = 0, hi = 1;
        for (int i = 0; i < 50; i++)
        {
            double mid = (lo + hi) / 2.0;
            double pct = CircleSegmentPercent(mid);
            if (Math.Abs(pct - targetPercent) < 1e-6)
                return mid; // Fruehes Abbrechen bei ausreichender Genauigkeit
            if (pct < targetPercent)
                lo = mid;
            else
                hi = mid;
        }
        return (lo + hi) / 2.0;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. Bogen-Winkel
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Snappt einen Bogenwinkel auf den naechsten VSA-Standardwinkel (15, 30, 45, 90 Grad).
    /// </summary>
    public static double SnapPipeBendAngle(double angleDeg)
    {
        // Typische Bogenwinkel nach VSA-Kontext.
        ReadOnlySpan<double> standards = stackalloc double[] { 15, 30, 45, 90 };
        double best = standards[0];
        double bestDelta = Math.Abs(angleDeg - best);
        for (int i = 1; i < standards.Length; i++)
        {
            double candidate = standards[i];
            double delta = Math.Abs(angleDeg - candidate);
            if (delta < bestDelta)
            {
                best = candidate;
                bestDelta = delta;
            }
        }
        return best;
    }

    /// <summary>
    /// Berechnet den Winkel zwischen zwei Richtungsvektoren (Dot-Produkt-Methode).
    /// a1→a2 = Achse vor dem Bogen, b1→b2 = Achse nach dem Bogen.
    /// Gibt null zurueck wenn einer der Vektoren (nahezu) null-laengig ist.
    /// </summary>
    public static double? BendAngleDeg(
        NormalizedPoint a1, NormalizedPoint a2,
        NormalizedPoint b1, NormalizedPoint b2)
    {
        double vx1 = a2.X - a1.X, vy1 = a2.Y - a1.Y;
        double vx2 = b2.X - b1.X, vy2 = b2.Y - b1.Y;
        double len1 = Math.Sqrt(vx1 * vx1 + vy1 * vy1);
        double len2 = Math.Sqrt(vx2 * vx2 + vy2 * vy2);
        if (len1 <= 1e-8 || len2 <= 1e-8) return null;
        double dot = vx1 * vx2 + vy1 * vy2;
        double cosAngle = Math.Clamp(dot / (len1 * len2), -1, 1);
        return Math.Acos(cosAngle) * 180.0 / Math.PI;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Quadratische Distanz zwischen zwei normierten Punkten (kein Sqrt → schnell).
    /// </summary>
    public static double DistanceSquared(NormalizedPoint p1, NormalizedPoint p2)
    {
        double dx = p1.X - p2.X;
        double dy = p1.Y - p2.Y;
        return dx * dx + dy * dy;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. Umkreis aus 3 Punkten (Circumcircle)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet den Umkreis (Mittelpunkt + normierter Radius) aus 3 Punkten.
    /// Kollineare Punkte → Fallback: Mittelpunkt der 3 Punkte, Radius = halbe Max-Distanz.
    /// </summary>
    public static (NormalizedPoint Center, double Radius) Circumcircle(
        NormalizedPoint p1, NormalizedPoint p2, NormalizedPoint p3)
    {
        double ax = p1.X, ay = p1.Y;
        double bx = p2.X, by = p2.Y;
        double cx = p3.X, cy = p3.Y;

        double D = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

        if (Math.Abs(D) < 1e-10)
        {
            // Punkte sind kollinear — Fallback: Mittelpunkt, halbe Max-Distanz
            var center = new NormalizedPoint((ax + bx + cx) / 3.0, (ay + by + cy) / 3.0);
            double d1 = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            double d2 = Math.Sqrt((cx - bx) * (cx - bx) + (cy - by) * (cy - by));
            double d3 = Math.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy));
            double radius = Math.Max(d1, Math.Max(d2, d3)) / 2.0;
            return (center, radius);
        }

        double ux = ((ax * ax + ay * ay) * (by - cy) +
                     (bx * bx + by * by) * (cy - ay) +
                     (cx * cx + cy * cy) * (ay - by)) / D;
        double uy = ((ax * ax + ay * ay) * (cx - bx) +
                     (bx * bx + by * by) * (ax - cx) +
                     (cx * cx + cy * cy) * (bx - ax)) / D;
        var u = new NormalizedPoint(ux, uy);
        double r = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
        return (u, r);
    }
}
