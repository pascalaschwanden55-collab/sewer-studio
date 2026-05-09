using System;
using System.Collections.Generic;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

// OverlayToolService Geometry-Builders: Erzeugen aus 2-Punkt-/Multi-Punkt-
// Eingaben die OverlayGeometry mit korrekter Quantifizierung
// (Linie/Bogen/Rechteck/Punkt/Strecke/Lineal/Wasserstand/Pipe-Bend/Lateral-
// Circle/Ellipse). Plus Hilfsfunktionen CircleSegmentPercent + Snap-Pipe-
// BendAngle. Aus dem Hauptdatei extrahiert (Slice 33).
public sealed partial class OverlayToolService
{
    private OverlayGeometry BuildLineGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lengthNorm = Math.Sqrt(dx * dx + dy * dy);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Line,
            Points = new List<NormalizedPoint> { start, end }
        };

        geo.ClockFrom = PointToClockHour(start);
        geo.ClockTo = PointToClockHour(end);
        geo.Q1Mm = NormLengthToMm(lengthNorm);

        return geo;
    }

    /// <summary>
    /// Normierte Laenge (0.0–1.0) in mm umrechnen.
    /// Kalibriert: ueber DN und Referenzlinie. Sonst: Fallback DN300.
    /// </summary>
    private double NormLengthToMm(double normLength)
    {
        if (_calibration != null)
            return _calibration.NormToMm(normLength);
        return normLength * 500; // Fallback ~DN300
    }

    private OverlayGeometry BuildArcGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Arc,
            Points = new List<NormalizedPoint> { start, end }
        };

        double clockFrom = PointToClockHour(start);
        double clockTo = PointToClockHour(end);
        geo.ClockFrom = clockFrom;
        geo.ClockTo = clockTo;

        // Bogenwinkel berechnen (im Uhrzeigersinn von → bis)
        double fromDeg = clockFrom * 30.0;
        double toDeg = clockTo * 30.0;
        double arc = toDeg - fromDeg;
        if (arc < 0) arc += 360;
        geo.ArcDegrees = arc;

        return geo;
    }

    private OverlayGeometry BuildRectangleGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = new List<NormalizedPoint>
            {
                start,
                new(end.X, start.Y),
                end,
                new(start.X, end.Y)
            }
        };

        double widthNorm = Math.Abs(end.X - start.X);
        double heightNorm = Math.Abs(end.Y - start.Y);

        geo.Q1Mm = NormLengthToMm(heightNorm);  // Hoehe
        geo.Q2Mm = NormLengthToMm(widthNorm);   // Breite

        var center = new NormalizedPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        geo.ClockFrom = PointToClockHour(center);

        return geo;
    }

    private OverlayGeometry BuildPointGeometry(NormalizedPoint point)
    {
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Point,
            Points = new List<NormalizedPoint> { point }
        };

        geo.ClockFrom = PointToClockHour(point);
        return geo;
    }

    private OverlayGeometry BuildStretchGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Stretch,
            Points = new List<NormalizedPoint> { start, end }
        };

        geo.ClockFrom = PointToClockHour(start);
        geo.ClockTo = PointToClockHour(end);

        return geo;
    }

    /// <summary>
    /// Lineal: Wie Linie aber mit ToolType=Ruler (fuer Tick-Mark-Rendering).
    /// </summary>
    private OverlayGeometry BuildRulerGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        double dx = end.X - start.X, dy = end.Y - start.Y;
        double lengthNorm = Math.Sqrt(dx * dx + dy * dy);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Ruler,
            Points = new List<NormalizedPoint> { start, end },
            Q1Mm = NormLengthToMm(lengthNorm),
            ClockFrom = PointToClockHour(start),
            ClockTo = PointToClockHour(end)
        };

        return geo;
    }

    // --- Neue Werkzeuge ---

    /// <summary>
    /// Level-Werkzeug: Horizontale Linie → Kreissegment-Prozentsatz.
    /// User zieht Linie auf Hoehe der Ablagerung/Wasseroberflaeche.
    /// </summary>
    private OverlayGeometry BuildLevelGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        // Horizontale Linie: Y = Mittelwert der beiden Punkte
        double levelY = (start.Y + end.Y) / 2.0;

        // Rohr-Geometrie aus Kalibrierung oder Fallback
        double pipeRadius = (_calibration?.NormalizedDiameter ?? 0.7) / 2.0;
        double pipeCenterY = _calibration?.PipeCenter.Y ?? 0.5;
        double sohle = pipeCenterY + pipeRadius;   // 6 Uhr (unten)
        double scheitel = pipeCenterY - pipeRadius; // 12 Uhr (oben)

        double hRatio;
        if (_activeLevelMode == LevelMode.Obstacle)
        {
            // Hindernis: von Scheitel (oben) nach unten messen
            double h = levelY - scheitel;
            hRatio = Math.Clamp(h / (pipeRadius * 2.0), 0, 1);
        }
        else
        {
            // Ablagerung/Wasser: von Sohle (unten) nach oben messen
            double h = sohle - levelY;
            hRatio = Math.Clamp(h / (pipeRadius * 2.0), 0, 1);
        }

        double fillPercent = CircleSegmentPercent(hRatio);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            Points = new List<NormalizedPoint>
            {
                new(Math.Min(start.X, end.X), levelY),
                new(Math.Max(start.X, end.X), levelY)
            },
            FillPercent = Math.Round(fillPercent, 1),
            LevelSubMode = _activeLevelMode,
            ClockFrom = _calibration?.PointToClockHour(new NormalizedPoint(0.5, levelY))
                       ?? PointToClockHour(new NormalizedPoint(0.5, levelY))
        };

        return geo;
    }

    /// <summary>
    /// Berechnet den Querschnitts-Prozentsatz eines Kreissegments.
    /// hRatio: Fuellhoehe relativ zum Durchmesser (0.0 = leer, 1.0 = voll).
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
    /// PipeBend: 4 Punkte → Biegewinkel zwischen zwei Rohrachsen.
    /// a1→a2 = Achse vor dem Bogen, b1→b2 = Achse nach dem Bogen.
    /// </summary>
    private OverlayGeometry BuildPipeBendGeometry(
        NormalizedPoint a1, NormalizedPoint a2,
        NormalizedPoint b1, NormalizedPoint b2)
    {
        // Richtungsvektoren
        double vx1 = a2.X - a1.X, vy1 = a2.Y - a1.Y;
        double vx2 = b2.X - b1.X, vy2 = b2.Y - b1.Y;

        // Laengen
        double len1 = Math.Sqrt(vx1 * vx1 + vy1 * vy1);
        double len2 = Math.Sqrt(vx2 * vx2 + vy2 * vy2);

        double? angleDeg = null;
        if (len1 > 1e-8 && len2 > 1e-8)
        {
            double dot = vx1 * vx2 + vy1 * vy2;
            double cosAngle = Math.Clamp(dot / (len1 * len2), -1, 1);
            angleDeg = Math.Acos(cosAngle) * 180.0 / Math.PI;
        }

        if (angleDeg.HasValue && _pipeBendSnapEnabled)
            angleDeg = SnapPipeBendAngle(angleDeg.Value);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            Points = new List<NormalizedPoint> { a1, a2, b1, b2 },
            ArcDegrees = angleDeg.HasValue ? Math.Round(angleDeg.Value, 1) : null
        };

        return geo;
    }

    private static double SnapPipeBendAngle(double angleDeg)
    {
        // Typische Bogenwinkel nach VSA/EN 13508-2 (vollstaendige Liste)
        ReadOnlySpan<double> standards = stackalloc double[] { 15, 30, 45, 60, 90, 120, 135, 150 };
        // Nur snappen wenn der Winkel innerhalb ±5° eines Standardwerts liegt
        const double snapTolerance = 5.0;
        double best = angleDeg; // Kein Snap als Default
        double bestDelta = snapTolerance;
        for (int i = 0; i < standards.Length; i++)
        {
            double delta = Math.Abs(angleDeg - standards[i]);
            if (delta < bestDelta)
            {
                best = standards[i];
                bestDelta = delta;
            }
        }
        return best;
    }

    private static double DistanceSquared(NormalizedPoint p1, NormalizedPoint p2)
    {
        double dx = p1.X - p2.X;
        double dy = p1.Y - p2.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// LateralCircle: 3 Punkte am Rand → Umkreis (circumscribed circle).
    /// Berechnet Mittelpunkt + Radius → Durchmesser in mm → DnRatioPercent.
    /// </summary>
    private OverlayGeometry BuildLateralCircleGeometry(
        NormalizedPoint p1, NormalizedPoint p2, NormalizedPoint p3)
    {
        // Umkreis aus 3 Punkten
        double ax = p1.X, ay = p1.Y;
        double bx = p2.X, by = p2.Y;
        double cx = p3.X, cy = p3.Y;

        double D = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

        NormalizedPoint center;
        double radiusNorm;

        if (Math.Abs(D) < 1e-10)
        {
            // Punkte sind kollinear — Fallback: Mittelpunkt der laengsten Strecke
            double d1 = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            double d2 = Math.Sqrt((cx - bx) * (cx - bx) + (cy - by) * (cy - by));
            double d3 = Math.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy));

            // Die zwei Punkte mit dem groessten Abstand bilden den Durchmesser
            if (d1 >= d2 && d1 >= d3)
                center = new NormalizedPoint((ax + bx) / 2.0, (ay + by) / 2.0);
            else if (d2 >= d1 && d2 >= d3)
                center = new NormalizedPoint((bx + cx) / 2.0, (by + cy) / 2.0);
            else
                center = new NormalizedPoint((ax + cx) / 2.0, (ay + cy) / 2.0);
            radiusNorm = Math.Max(d1, Math.Max(d2, d3)) / 2.0;
        }
        else
        {
            double ux = ((ax * ax + ay * ay) * (by - cy) +
                         (bx * bx + by * by) * (cy - ay) +
                         (cx * cx + cy * cy) * (ay - by)) / D;
            double uy = ((ax * ax + ay * ay) * (cx - bx) +
                         (bx * bx + by * by) * (ax - cx) +
                         (cx * cx + cy * cy) * (bx - ax)) / D;
            center = new NormalizedPoint(ux, uy);
            radiusNorm = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
        }

        double diameterMm = NormLengthToMm(radiusNorm * 2);

        double? dnRatio = null;
        if (_calibration?.NominalDiameterMm > 0)
            dnRatio = Math.Round((diameterMm / _calibration.NominalDiameterMm) * 100.0, 1);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.LateralCircle,
            Points = new List<NormalizedPoint> { p1, p2, p3 },
            Q1Mm = Math.Round(diameterMm, 0),
            DnRatioPercent = dnRatio,
            ClockFrom = PointToClockHour(center)
        };

        return geo;
    }

    // --- Ellipse- und Freihand-Werkzeuge ---

    /// <summary>
    /// Ellipse/Kreis: Ecke-zu-Ecke Drag (wie Rechteck). Berechnet Center + Radien.
    /// </summary>
    private OverlayGeometry BuildEllipseGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        double widthNorm = Math.Abs(end.X - start.X);
        double heightNorm = Math.Abs(end.Y - start.Y);

        var center = new NormalizedPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Ellipse,
            Points = new List<NormalizedPoint> { start, end },
            Q1Mm = NormLengthToMm(heightNorm),       // Hoehe in mm
            Q2Mm = NormLengthToMm(widthNorm),         // Breite in mm
            EllipseRadiusXMm = NormLengthToMm(widthNorm / 2),
            EllipseRadiusYMm = NormLengthToMm(heightNorm / 2),
            ClockFrom = PointToClockHour(center)
        };

        return geo;
    }
}
