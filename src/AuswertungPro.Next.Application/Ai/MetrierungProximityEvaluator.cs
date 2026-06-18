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

        // Isotrope Distanz in Einheiten "Anteil der Bildbreite" — konsistent mit pipeR.
        // NormalizedDiameter (und damit pipeR) wird aus der horizontalen Kalibrierlinie als
        // Breitenanteil bestimmt. Deshalb die Hoehe durch das Seitenverhaeltnis TEILEN,
        // nicht die Breite multiplizieren (sonst ist distToVanish um den Faktor Aspect verfaelscht).
        double aspect = i.ImageAspect > 0 ? i.ImageAspect : 1.0;
        double Dist(double ax, double ay, double bx, double by)
        {
            double dx = ax - bx;
            double dy = (ay - by) / aspect;
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

        // Fachregel des Inspekteurs (vom User bestaetigt 2026-06-16):
        // Codieren erst, wenn das Ereignis zwischen DN-Kreis und Bildrand liegt — also
        // den DN-Kreis (Rohrradius, outerR=1.0) nach AUSSEN ueberschreitet. Solange der
        // Befund ganz INNERHALB des DN-Kreises liegt (Richtung Tunnel/Fluchtpunkt), ist er
        // noch zu weit voraus: nur merken, nicht protokollieren. Erst der Nahbereich liefert
        // die korrekte Distanz/Metrierung.

        // 1) Querschnittsfuellend nah: gross UND echte Wandnaehe -> Codierbar.
        //    (grosse Muffe direkt vor der Kamera, fuellt den Querschnitt)
        if (fillRatio >= t.FillNear && wandnaehe)
            return Result(MetrierungProximity.Codierbar, "querschnittsfuellend mit Wandnaehe");

        // 2) Befund ueberschreitet den DN-Kreis nach aussen (reicht in den Ring
        //    DN-Kreis..Bildrand) -> nah genug, Codierbar. Das ist die zentrale Regel.
        if (outerR >= 1.0 - t.WallTolerance)
            return Result(MetrierungProximity.Codierbar, "ueberschreitet DN-Kreis nach aussen (Nahbereich)");

        // 3) Sonst: Befund liegt komplett im DN-Kreis (Richtung Fluchtpunkt) -> noch zu weit
        //    voraus. Wird gemerkt, aber nicht codiert (Distanz waere falsch).
        return Result(MetrierungProximity.Voraus, "innerhalb DN-Kreis, noch zu weit voraus (nur merken)");
    }
}
