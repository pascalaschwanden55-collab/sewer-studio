using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine Berechnungslogik fuer die PhotoMeasurement-Werkzeuge.
/// Kein UI, kein WPF — vollstaendig testbar.
/// </summary>
public static class PhotoMeasurementGeometryService
{
    // ═══════════════════════════════════════════════════════════════════
    // Letterbox-Geometrie
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet das tatsaechlich gerenderte Bild-Rechteck innerhalb eines Stretch=Uniform-Controls.
    /// Gibt (OffsetX, OffsetY, RenderedW, RenderedH) zurueck.
    /// Liefert (0, 0, controlW, controlH) wenn Eingaben ungueltg sind.
    /// </summary>
    public static (double OffsetX, double OffsetY, double RenderedW, double RenderedH)
        LetterboxRect(double controlW, double controlH, double imgW, double imgH)
    {
        if (controlW <= 0 || controlH <= 0 || imgW <= 0 || imgH <= 0)
            return (0, 0, controlW, controlH);

        double scaleX = controlW / imgW;
        double scaleY = controlH / imgH;
        double scale = Math.Min(scaleX, scaleY);

        double renderedW = imgW * scale;
        double renderedH = imgH * scale;
        double offsetX = (controlW - renderedW) / 2.0;
        double offsetY = (controlH - renderedH) / 2.0;

        return (offsetX, offsetY, renderedW, renderedH);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Deformations-Messung (4-Punkt-Klick)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sortiert vier unsortierte Klickpunkte nach Uhrlage relativ zum Rohrmittelpunkt.
    /// Gibt (top, bottom, right, left) zurueck — top = naechster an 12 Uhr (0°),
    /// bottom = naechster an 6 Uhr (180°), right = 3 Uhr (90°), left = 9 Uhr (270°).
    /// </summary>
    public static (NormalizedPoint Top, NormalizedPoint Bottom, NormalizedPoint Right, NormalizedPoint Left)
        SortDeformationPoints(IReadOnlyList<NormalizedPoint> points, double centerX, double centerY)
    {
        if (points.Count < 4)
            throw new ArgumentException("Es werden genau 4 Punkte benoetigt.", nameof(points));

        var pool = new List<NormalizedPoint>(points);

        NormalizedPoint FindClosestToAngle(double targetDeg)
        {
            NormalizedPoint best = pool[0];
            double bestDelta = double.MaxValue;
            foreach (var p in pool)
            {
                double dx = p.X - centerX, dy = p.Y - centerY;
                // 0° = 12 Uhr (Scheitel, Y negativ), im Uhrzeigersinn
                double deg = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
                if (deg < 0) deg += 360;
                double delta = Math.Abs(deg - targetDeg);
                if (delta > 180) delta = 360 - delta;
                if (delta < bestDelta) { bestDelta = delta; best = p; }
            }
            pool.Remove(best);
            return best;
        }

        var top    = FindClosestToAngle(0);    // 12 Uhr
        var bottom = FindClosestToAngle(180);  // 6 Uhr
        var right  = FindClosestToAngle(90);   // 3 Uhr
        var left   = pool[0];                  // letzter verbleibender = 9 Uhr

        return (top, bottom, right, left);
    }

    /// <summary>
    /// Berechnet den Deformationsprozentsatz aus den sortierten Messpunkten.
    /// Formel: ((dMax - dMin) / dNominal) * 100.
    /// </summary>
    /// <param name="top">12-Uhr-Punkt</param>
    /// <param name="bottom">6-Uhr-Punkt</param>
    /// <param name="left">9-Uhr-Punkt</param>
    /// <param name="right">3-Uhr-Punkt</param>
    /// <param name="imageAspect">Seitenverhaeltnis (Breite/Hoehe) des Fotos fuer Aspect-Korrektur</param>
    /// <param name="nominalDiameter">Normierter Rohrdurchmesser (0–1); 0 = aus Messung ableiten</param>
    /// <returns>Deformationsprozentsatz (0–100+)</returns>
    public static double DeformationPercent(
        NormalizedPoint top, NormalizedPoint bottom,
        NormalizedPoint left, NormalizedPoint right,
        double imageAspect, double nominalDiameter)
    {
        double dVertNorm  = PipeCalibration.AspectCorrectedDistance(top, bottom, imageAspect);
        double dHorizNorm = PipeCalibration.AspectCorrectedDistance(left, right, imageAspect);

        double dMax = Math.Max(dVertNorm, dHorizNorm);
        double dMin = Math.Min(dVertNorm, dHorizNorm);
        double dNominal = nominalDiameter > 0
            ? nominalDiameter
            : Math.Max(dVertNorm, dHorizNorm);

        return dNominal > 0 ? ((dMax - dMin) / dNominal) * 100.0 : 0;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Querschnittsverminderung (Shoelace-Polygon)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet die Polygon-Flaeche in Pixel-Einheiten mittels Shoelace-Formel.
    /// Punkte sind normiert (0–1); renderWidth/renderHeight skalieren auf Pixel-Raum
    /// (Aspect-Ratio-korrekte Flaeche im Display-Koordinatensystem).
    /// </summary>
    public static double ShoelaceAreaPx(
        IReadOnlyList<NormalizedPoint> points, double renderWidth, double renderHeight)
    {
        if (points.Count < 3) return 0;

        double area = 0;
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            var curr = points[i];
            var next = points[(i + 1) % n];
            double cx = curr.X * renderWidth,  cy = curr.Y * renderHeight;
            double nx = next.X * renderWidth,  ny = next.Y * renderHeight;
            area += cx * ny - nx * cy;
        }
        return Math.Abs(area) / 2.0;
    }

    /// <summary>
    /// Berechnet die Querschnittsverminderung in Prozent.
    /// pipeAreaPx = PI * pipeRadiusPx^2.
    /// </summary>
    public static double CrossSectionPercent(double polygonAreaPx, double pipeRadiusPx)
    {
        double pipeArea = Math.PI * pipeRadiusPx * pipeRadiusPx;
        return pipeArea > 0 ? (polygonAreaPx / pipeArea) * 100.0 : 0;
    }

    /// <summary>
    /// Berechnet den Rohr-Radius in Pixel-Einheiten aus normierten Werten.
    /// Nutzt denselben Referenz-Massstab wie das PhotoMeasurementWindow (Min der Render-Dimensionen).
    /// </summary>
    public static double PipeRadiusPx(double normalizedDiameter, double renderWidth, double renderHeight)
    {
        double pipeRadius = (normalizedDiameter > 0 ? normalizedDiameter : 0.7) / 2.0;
        return pipeRadius * Math.Min(renderWidth, renderHeight);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Winkel-Werkzeuge (Abzweig / Bogen)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wandelt eine Winkelposition in Grad (0–360, 0° = 12 Uhr) in Uhrlage (0.0–12.0) um.
    /// Formel: positionDeg / 30.
    /// </summary>
    /// <summary>
    /// Berechnet die manuelle Rohrkalibrierung aus einer gezogenen Referenzlinie.
    /// </summary>
    /// <summary>
    /// Baut die fachliche Querschnittsverminderung aus einem Freihand-Polygon.
    /// </summary>
    public static PhotoMeasurementCrossSectionGeometry? BuildCrossSectionGeometry(
        IReadOnlyList<NormalizedPoint> points,
        double renderWidth,
        double renderHeight,
        double normalizedDiameter)
    {
        if (points.Count < 3 || renderWidth <= 0 || renderHeight <= 0)
            return null;

        double polygonAreaPx = ShoelaceAreaPx(points, renderWidth, renderHeight);
        double pipeRadiusPx = PipeRadiusPx(normalizedDiameter, renderWidth, renderHeight);
        double reductionPercent = CrossSectionPercent(polygonAreaPx, pipeRadiusPx);

        var geometry = new OverlayGeometry
        {
            ToolType = OverlayToolType.CrossSection,
            Points = points.Select(p => new NormalizedPoint(p.X, p.Y)).ToList(),
            FillPercent = Math.Round(reductionPercent, 1)
        };

        return new PhotoMeasurementCrossSectionGeometry(
            Geometry: geometry,
            ReductionPercent: reductionPercent,
            LabelPoint: new NormalizedPoint(points.Average(p => p.X), points.Average(p => p.Y)),
            PolygonAreaPx: polygonAreaPx,
            PipeRadiusPx: pipeRadiusPx);
    }

    public static PhotoMeasurementCalibrationGeometry? BuildCalibrationGeometry(
        NormalizedPoint start,
        NormalizedPoint end,
        double imageAspect,
        double minNormalizedLength = 0.01)
    {
        double normalizedDiameter = PipeCalibration.AspectCorrectedDistance(start, end, imageAspect);
        if (normalizedDiameter < minNormalizedLength)
            return null;

        return new PhotoMeasurementCalibrationGeometry(
            NormalizedDiameter: normalizedDiameter,
            PipeCenter: new NormalizedPoint(
                (start.X + end.X) / 2.0,
                (start.Y + end.Y) / 2.0));
    }

    /// <summary>
    /// Baut die fachliche Rechteck-Geometrie fuer eine Foto-Markierung.
    /// </summary>
    public static OverlayGeometry? BuildMarkRectangleGeometry(
        NormalizedPoint start,
        NormalizedPoint end,
        double minNormalizedSize = 0.01)
    {
        double minX = Math.Min(start.X, end.X), maxX = Math.Max(start.X, end.X);
        double minY = Math.Min(start.Y, end.Y), maxY = Math.Max(start.Y, end.Y);
        if (maxX - minX < minNormalizedSize || maxY - minY < minNormalizedSize)
            return null;

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = new List<NormalizedPoint>
            {
                new(minX, minY), new(maxX, minY),
                new(maxX, maxY), new(minX, maxY)
            }
        };
    }

    /// <summary>
    /// Baut die fachliche Linien-/Lineal-Geometrie inklusive Millimeterwert.
    /// </summary>
    public static PhotoMeasurementLineGeometry? BuildLineGeometry(
        OverlayToolType toolType,
        NormalizedPoint start,
        NormalizedPoint end,
        PipeCalibration calibration,
        double imageAspect,
        double minNormalizedLength = 0.005)
    {
        ArgumentNullException.ThrowIfNull(calibration);

        if (toolType is not (OverlayToolType.Line or OverlayToolType.Ruler))
            throw new ArgumentOutOfRangeException(nameof(toolType), toolType, "Only Line and Ruler are supported.");

        double normalizedLength = PipeCalibration.AspectCorrectedDistance(start, end, imageAspect);
        if (normalizedLength < minNormalizedLength)
            return null;

        double millimeters = calibration.NormToMm(normalizedLength);
        var geometry = new OverlayGeometry
        {
            ToolType = toolType,
            Points = new List<NormalizedPoint> { start, end },
            Q1Mm = Math.Round(millimeters, 1),
            ClockFrom = calibration.PointToClockHour(start),
            ClockTo = calibration.PointToClockHour(end)
        };

        return new PhotoMeasurementLineGeometry(geometry, millimeters, normalizedLength);
    }

    /// <summary>
    /// Plant die UI-freie Geometrie fuer das Level-/Fuellstand-Overlay.
    /// </summary>
    public static PhotoMeasurementLevelOverlayPlan? BuildLevelOverlayPlan(
        OverlayGeometry geometry,
        PipeCalibration calibration,
        double renderedX,
        double renderedY,
        double renderedWidth,
        double renderedHeight,
        double cameraHeightPercent)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(calibration);

        if (geometry.Points.Count < 2 || renderedWidth <= 0 || renderedHeight <= 0)
            return null;

        double refSize = Math.Min(renderedWidth, renderedHeight);
        double normalizedDiameter = calibration.NormalizedDiameter;
        double pipeRadius = (normalizedDiameter / 2.0) * refSize;

        double cameraRatio = (cameraHeightPercent - 50.0) / 100.0;
        double normalizedCenterX = calibration.PipeCenter.X;
        double normalizedCenterY = calibration.PipeCenter.Y + cameraRatio * (normalizedDiameter / 2.0) * 0.3;

        var center = ToCanvasPoint(renderedX, renderedY, renderedWidth, renderedHeight, normalizedCenterX, normalizedCenterY);
        var levelPoint = geometry.Points[0];
        double levelY = ToCanvasPoint(renderedX, renderedY, renderedWidth, renderedHeight, levelPoint.X, levelPoint.Y).Y;

        PhotoMeasurementCanvasRect fillRect;
        if (geometry.LevelSubMode == LevelMode.Obstacle)
        {
            double height = Math.Max(0, levelY - (center.Y - pipeRadius));
            fillRect = new PhotoMeasurementCanvasRect(
                X: center.X - pipeRadius,
                Y: center.Y - pipeRadius,
                Width: pipeRadius * 2,
                Height: height);
        }
        else
        {
            double height = Math.Max(0, (center.Y + pipeRadius) - levelY);
            fillRect = new PhotoMeasurementCanvasRect(
                X: center.X - pipeRadius,
                Y: levelY,
                Width: pipeRadius * 2,
                Height: height);
        }

        double relativeY = levelY - center.Y;
        double chordHalf = Math.Sqrt(Math.Max(0, pipeRadius * pipeRadius - relativeY * relativeY));

        return new PhotoMeasurementLevelOverlayPlan(
            Center: center,
            PipeRadius: pipeRadius,
            FillRect: fillRect,
            LineStart: new PhotoMeasurementCanvasPoint(center.X - chordHalf, levelY),
            LineEnd: new PhotoMeasurementCanvasPoint(center.X + chordHalf, levelY),
            LabelPosition: new PhotoMeasurementCanvasPoint(center.X, levelY - 18),
            LevelY: levelY,
            ChordHalf: chordHalf);
    }

    private static PhotoMeasurementCanvasPoint ToCanvasPoint(
        double renderedX,
        double renderedY,
        double renderedWidth,
        double renderedHeight,
        double normalizedX,
        double normalizedY)
        => new(renderedX + normalizedX * renderedWidth, renderedY + normalizedY * renderedHeight);

    public static double PositionDegToClockHour(double positionDeg)
        => positionDeg / 30.0;

    /// <summary>
    /// Wandelt Uhrlage (Grad, 0° = 12 Uhr) in einen WPF-kompatiblen Radiant-Wert um,
    /// bei dem 0° = 3-Uhr-Richtung (Math.Cos/Sin-Konvention).
    /// Formel: (positionDeg - 90) * PI / 180.
    /// </summary>
    public static double PositionDegToRadians(double positionDeg)
        => (positionDeg - 90.0) * Math.PI / 180.0;

    /// <summary>
    /// Baut die fachliche Winkel-Geometrie fuer Abzweig/Bogen.
    /// Die UI nutzt das Ergebnis nur noch zum Zeichnen.
    /// </summary>
    public static PhotoMeasurementAngleGeometry BuildAngleGeometry(
        OverlayToolType toolType,
        NormalizedPoint pipeCenter,
        double normalizedDiameter,
        double positionDeg,
        double angleDeg)
    {
        if (toolType is not (OverlayToolType.LateralCircle or OverlayToolType.PipeBend))
            throw new ArgumentOutOfRangeException(nameof(toolType), toolType, "Only LateralCircle and PipeBend are supported.");

        double positionRad = PositionDegToRadians(positionDeg);
        double clockHour = PositionDegToClockHour(positionDeg);
        double radiusNorm = normalizedDiameter / 2.0;
        var edgePoint = new NormalizedPoint(
            pipeCenter.X + Math.Cos(positionRad) * radiusNorm,
            pipeCenter.Y + Math.Sin(positionRad) * radiusNorm);

        var geometry = new OverlayGeometry
        {
            ToolType = toolType,
            ArcDegrees = Math.Round(angleDeg, 1),
            ClockFrom = Math.Round(clockHour, 1),
            Points = new List<NormalizedPoint> { pipeCenter, edgePoint }
        };

        return new PhotoMeasurementAngleGeometry(geometry, positionRad, clockHour, edgePoint);
    }

    /// <summary>
    /// Reine Zeichengeometrie fuer die Abzweig-Vorschau.
    /// Enthaelt keine WPF-Typen, damit die Berechnung testbar bleibt.
    /// </summary>
    public static PhotoMeasurementLateralOverlayPlan BuildLateralOverlayPlan(
        double centerX,
        double centerY,
        double pipeRadiusPx,
        double positionRad,
        double angleDeg)
    {
        double openingRadius = pipeRadiusPx * 0.15;
        double openingX = centerX + Math.Cos(positionRad) * pipeRadiusPx;
        double openingY = centerY + Math.Sin(positionRad) * pipeRadiusPx;
        double halfAngleRad = (angleDeg / 2.0) * Math.PI / 180.0;
        double armLength = pipeRadiusPx * 0.6;

        var arm1End = new PhotoMeasurementCanvasPoint(
            openingX + Math.Cos(positionRad - halfAngleRad) * armLength,
            openingY + Math.Sin(positionRad - halfAngleRad) * armLength);
        var arm2End = new PhotoMeasurementCanvasPoint(
            openingX + Math.Cos(positionRad + halfAngleRad) * armLength,
            openingY + Math.Sin(positionRad + halfAngleRad) * armLength);
        var label = new PhotoMeasurementCanvasPoint(
            openingX + Math.Cos(positionRad) * (armLength * 0.5),
            openingY + Math.Sin(positionRad) * (armLength * 0.5) - 14);

        return new PhotoMeasurementLateralOverlayPlan(
            OpeningCenter: new PhotoMeasurementCanvasPoint(openingX, openingY),
            OpeningRadius: openingRadius,
            Arm1End: arm1End,
            Arm2End: arm2End,
            ArcRadius: armLength * 0.4,
            ArcStartRad: positionRad - halfAngleRad,
            ArcEndRad: positionRad + halfAngleRad,
            LabelPosition: label);
    }

    /// <summary>
    /// Reine Zeichengeometrie fuer die Bogen-Vorschau.
    /// </summary>
    public static PhotoMeasurementBendOverlayPlan BuildBendOverlayPlan(
        double centerX,
        double centerY,
        double pipeRadiusPx,
        double positionRad,
        double angleDeg,
        int ringCount = 8,
        int axisSegmentCount = 20)
    {
        ringCount = Math.Max(2, ringCount);
        axisSegmentCount = Math.Max(1, axisSegmentCount);

        double halfAngleRad = (angleDeg / 2.0) * Math.PI / 180.0;
        double bendRadius = 3.5 * pipeRadiusPx;
        double arcCenterX = centerX + Math.Cos(positionRad + Math.PI / 2) * bendRadius;
        double arcCenterY = centerY + Math.Sin(positionRad + Math.PI / 2) * bendRadius;

        var rings = new List<PhotoMeasurementBendRing>(ringCount);
        for (int i = 0; i < ringCount; i++)
        {
            double t = (double)i / (ringCount - 1);
            double ringAngle = positionRad + Math.PI / 2 - halfAngleRad + t * 2 * halfAngleRad;
            double ringX = arcCenterX - Math.Cos(ringAngle) * bendRadius;
            double ringY = arcCenterY - Math.Sin(ringAngle) * bendRadius;
            double perspectiveScale = 1.0 - 0.3 * Math.Abs(t - 0.5) * 2;
            rings.Add(new PhotoMeasurementBendRing(
                Center: new PhotoMeasurementCanvasPoint(ringX, ringY),
                RadiusX: pipeRadiusPx * 0.9 * perspectiveScale,
                RadiusY: pipeRadiusPx * 0.3 * perspectiveScale,
                PerspectiveScale: perspectiveScale));
        }

        var axisPoints = new List<PhotoMeasurementCanvasPoint>(axisSegmentCount + 1);
        for (int i = 0; i <= axisSegmentCount; i++)
        {
            double t = (double)i / axisSegmentCount;
            double angle = positionRad + Math.PI / 2 - halfAngleRad + t * 2 * halfAngleRad;
            axisPoints.Add(new PhotoMeasurementCanvasPoint(
                arcCenterX - Math.Cos(angle) * bendRadius,
                arcCenterY - Math.Sin(angle) * bendRadius));
        }

        return new PhotoMeasurementBendOverlayPlan(
            ArcCenter: new PhotoMeasurementCanvasPoint(arcCenterX, arcCenterY),
            BendRadius: bendRadius,
            HalfAngleRad: halfAngleRad,
            Rings: rings,
            AxisPoints: axisPoints);
    }
}

public sealed record PhotoMeasurementAngleGeometry(
    OverlayGeometry Geometry,
    double PositionRad,
    double ClockHour,
    NormalizedPoint EdgePoint);

public sealed record PhotoMeasurementCalibrationGeometry(
    double NormalizedDiameter,
    NormalizedPoint PipeCenter);

public sealed record PhotoMeasurementCrossSectionGeometry(
    OverlayGeometry Geometry,
    double ReductionPercent,
    NormalizedPoint LabelPoint,
    double PolygonAreaPx,
    double PipeRadiusPx);

public sealed record PhotoMeasurementLineGeometry(
    OverlayGeometry Geometry,
    double Millimeters,
    double NormalizedLength);

public sealed record PhotoMeasurementCanvasPoint(double X, double Y);

public sealed record PhotoMeasurementCanvasRect(double X, double Y, double Width, double Height);

public sealed record PhotoMeasurementLevelOverlayPlan(
    PhotoMeasurementCanvasPoint Center,
    double PipeRadius,
    PhotoMeasurementCanvasRect FillRect,
    PhotoMeasurementCanvasPoint LineStart,
    PhotoMeasurementCanvasPoint LineEnd,
    PhotoMeasurementCanvasPoint LabelPosition,
    double LevelY,
    double ChordHalf);

public sealed record PhotoMeasurementLateralOverlayPlan(
    PhotoMeasurementCanvasPoint OpeningCenter,
    double OpeningRadius,
    PhotoMeasurementCanvasPoint Arm1End,
    PhotoMeasurementCanvasPoint Arm2End,
    double ArcRadius,
    double ArcStartRad,
    double ArcEndRad,
    PhotoMeasurementCanvasPoint LabelPosition);

public sealed record PhotoMeasurementBendRing(
    PhotoMeasurementCanvasPoint Center,
    double RadiusX,
    double RadiusY,
    double PerspectiveScale);

public sealed record PhotoMeasurementBendOverlayPlan(
    PhotoMeasurementCanvasPoint ArcCenter,
    double BendRadius,
    double HalfAngleRad,
    IReadOnlyList<PhotoMeasurementBendRing> Rings,
    IReadOnlyList<PhotoMeasurementCanvasPoint> AxisPoints);
