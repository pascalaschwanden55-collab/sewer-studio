using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine Naehe-Pruefung: entscheidet, ob ein Befund nah genug zum Metrieren ist.
/// Bezug ist der Fluchtpunkt (Rohrmitte). Distanzen in Einheiten des Rohrradius
/// (1.0 = an der Rohrwand). Konservativ: was nicht klar nah ist, gilt als "Voraus".
/// Regeln siehe Spec 2026-06-03-metrierung-naehe-gate.
/// </summary>
public static class MetrierungProximityEvaluator
{
    public static MetrierungProximityResult Evaluate(MetrierungProximityInput i, MetrierungProximityThresholds t)
    {
        double pipeR = i.PipeRadiusNorm > 0 ? i.PipeRadiusNorm : 0.5;

        double cx = (i.X1 + i.X2) / 2.0;
        double cy = (i.Y1 + i.Y2) / 2.0;
        double fillRatio = Math.Max(0.0, i.Y2 - i.Y1);

        double Dist(double ax, double ay, double bx, double by)
        {
            double dx = (ax - bx) * i.ImageAspect;
            double dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        double distToVanish = Dist(cx, cy, i.VanishX, i.VanishY) / pipeR;

        // groesste Eckendistanz zum Fluchtpunkt (in Rohrradius) -> wie weit reicht die Box nach aussen
        double outerR = 0.0;
        var corners = new[] { (i.X1, i.Y1), (i.X2, i.Y1), (i.X2, i.Y2), (i.X1, i.Y2) };
        foreach (var (px, py) in corners)
            outerR = Math.Max(outerR, Dist(px, py, i.VanishX, i.VanishY) / pipeR);

        bool enthaeltCenter = i.X1 <= i.VanishX && i.VanishX <= i.X2
                           && i.Y1 <= i.VanishY && i.VanishY <= i.Y2;

        bool touchesBorder = i.X1 <= t.WallTolerance || i.Y1 <= t.WallTolerance
                          || i.X2 >= 1.0 - t.WallTolerance || i.Y2 >= 1.0 - t.WallTolerance;
        bool reachesWall = outerR >= 1.0 - t.WallTolerance;
        bool wandnaehe = touchesBorder || reachesWall;

        MetrierungProximityResult Result(MetrierungProximity d, string reason)
            => new(d, reason, fillRatio, distToVanish, outerR, wandnaehe, enthaeltCenter);

        // 1) Tunnel-Fehlmaske: zentral am Fluchtpunkt, keine Wandnaehe -> Voraus.
        if (enthaeltCenter && distToVanish < t.CenterNear && !wandnaehe)
            return Result(MetrierungProximity.Voraus, "zentral am Fluchtpunkt ohne Wandnaehe");

        // 2) Querschnittsfuellend nah: gross UND Wandnaehe -> Codierbar.
        if (fillRatio >= t.FillNear && wandnaehe)
            return Result(MetrierungProximity.Codierbar, "querschnittsfuellend mit Wandnaehe");

        // 3) Wandschaden nah: deutlich ausserhalb des Fluchtpunktbereichs -> Codierbar.
        if (distToVanish >= t.RadialOutside)
            return Result(MetrierungProximity.Codierbar, "ausserhalb Fluchtpunktbereich (Wandnaehe)");

        // 4) Konservativer Default.
        return Result(MetrierungProximity.Voraus, "nicht eindeutig nah (konservativ)");
    }
}
