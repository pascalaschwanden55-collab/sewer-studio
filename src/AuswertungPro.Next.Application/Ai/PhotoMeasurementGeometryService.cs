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

    /// <summary>
    /// Baut die fachliche Deformations-Geometrie aus vier Messpunkten.
    /// </summary>
    public static PhotoMeasurementDeformationGeometry? BuildDeformationGeometry(
        IReadOnlyList<NormalizedPoint> points,
        PipeCalibration calibration,
        double imageAspect)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(calibration);

        if (points.Count < 4)
            return null;

        var (top, bottom, right, left) = SortDeformationPoints(
            points,
            calibration.PipeCenter.X,
            calibration.PipeCenter.Y);

        double deformationPercent = DeformationPercent(
            top,
            bottom,
            left,
            right,
            imageAspect,
            calibration.NormalizedDiameter);
        double verticalDistance = PipeCalibration.AspectCorrectedDistance(top, bottom, imageAspect);
        double horizontalDistance = PipeCalibration.AspectCorrectedDistance(left, right, imageAspect);

        var geometry = new OverlayGeometry
        {
            ToolType = OverlayToolType.Ellipse,
            Points = points.Select(p => new NormalizedPoint(p.X, p.Y)).ToList(),
            FillPercent = Math.Round(deformationPercent, 1)
        };

        return new PhotoMeasurementDeformationGeometry(
            Geometry: geometry,
            DeformationPercent: deformationPercent,
            VerticalDistance: verticalDistance,
            HorizontalDistance: horizontalDistance,
            Top: top,
            Bottom: bottom,
            Right: right,
            Left: left);
    }

    /// <summary>
    /// Baut den fachlichen Plan fuer eine 4-Punkt-Deformationsmessung.
    /// </summary>
    public static PhotoMeasurementDeformationPlan? BuildDeformationPlan(
        IReadOnlyList<NormalizedPoint> points,
        PipeCalibration calibration,
        double imageAspect,
        double renderedX,
        double renderedY,
        double renderedWidth,
        double renderedHeight)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(calibration);

        if (renderedWidth <= 0 || renderedHeight <= 0)
            return null;

        var deformation = BuildDeformationGeometry(
            points,
            calibration,
            imageAspect);
        if (deformation is null)
            return null;

        var topCanvas = ToCanvasPoint(
            renderedX,
            renderedY,
            renderedWidth,
            renderedHeight,
            deformation.Top.X,
            deformation.Top.Y);
        var bottomCanvas = ToCanvasPoint(
            renderedX,
            renderedY,
            renderedWidth,
            renderedHeight,
            deformation.Bottom.X,
            deformation.Bottom.Y);
        var leftCanvas = ToCanvasPoint(
            renderedX,
            renderedY,
            renderedWidth,
            renderedHeight,
            deformation.Left.X,
            deformation.Left.Y);
        var rightCanvas = ToCanvasPoint(
            renderedX,
            renderedY,
            renderedWidth,
            renderedHeight,
            deformation.Right.X,
            deformation.Right.Y);

        return new PhotoMeasurementDeformationPlan(
            Geometry: deformation.Geometry,
            DeformationPercent: deformation.DeformationPercent,
            VerticalDistance: deformation.VerticalDistance,
            HorizontalDistance: deformation.HorizontalDistance,
            Top: topCanvas,
            Bottom: bottomCanvas,
            Left: leftCanvas,
            Right: rightCanvas,
            LabelPosition: new PhotoMeasurementCanvasPoint(
                (topCanvas.X + bottomCanvas.X) / 2.0,
                ((topCanvas.Y + bottomCanvas.Y) / 2.0) - 20.0));
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

    /// <summary>
    /// Plant Rohrkreis, Mittelpunkt und Fadenkreuz fuer die Foto-Messwerkzeuge.
    /// </summary>
    public static PhotoMeasurementPipeCirclePlan? BuildPipeCirclePlan(
        PipeCalibration calibration,
        double renderedX,
        double renderedY,
        double renderedWidth,
        double renderedHeight,
        double crosshairHalfLength = 6.0)
    {
        ArgumentNullException.ThrowIfNull(calibration);

        if (renderedWidth <= 0 || renderedHeight <= 0)
            return null;

        double radius = PipeRadiusPx(calibration.NormalizedDiameter, renderedWidth, renderedHeight);
        var center = ToCanvasPoint(
            renderedX,
            renderedY,
            renderedWidth,
            renderedHeight,
            calibration.PipeCenter.X,
            calibration.PipeCenter.Y);

        return new PhotoMeasurementPipeCirclePlan(
            Center: center,
            Radius: radius,
            HorizontalStart: new PhotoMeasurementCanvasPoint(center.X - crosshairHalfLength, center.Y),
            HorizontalEnd: new PhotoMeasurementCanvasPoint(center.X + crosshairHalfLength, center.Y),
            VerticalStart: new PhotoMeasurementCanvasPoint(center.X, center.Y - crosshairHalfLength),
            VerticalEnd: new PhotoMeasurementCanvasPoint(center.X, center.Y + crosshairHalfLength));
    }

    /// <summary>
    /// Prueft, ob ein Canvas-Punkt nahe genug am Rohrmittelpunkt liegt, um den Kreis zu verschieben.
    /// </summary>
    public static bool IsInsidePipeCenterHitArea(
        PhotoMeasurementPipeCirclePlan plan,
        double canvasX,
        double canvasY,
        double hitRadiusFactor = 0.2)
    {
        ArgumentNullException.ThrowIfNull(plan);

        double dx = canvasX - plan.Center.X;
        double dy = canvasY - plan.Center.Y;
        double hitRadius = Math.Max(0, plan.Radius * hitRadiusFactor);
        return Math.Sqrt(dx * dx + dy * dy) < hitRadius;
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

        // Ohne Kalibrierung gibt es KEINEN Prozentwert. Der Querschnitt bezieht
        // sich auf die Rohrflaeche, und die ist ohne Referenz unbekannt. Frueher
        // sprang hier PipeRadiusPx mit seinem Zeichen-Standard 0,7 ein - also der
        // Annahme, das Rohr fuelle 70 % des Bildes - und lieferte einen Wert, der
        // ueber PhotoMeasurementResultMapper ins Q1-Feld des Protokolls wanderte.
        // Bei einem Rohr, das tatsaechlich 50 % fuellt, erschien eine echte
        // 30-Prozent-Verlegung dadurch als 15 %; bei 90 % als 50 %. Aus dieser
        // Zahl folgt bei BBC/BBA/BBB/BAI die Schadensstufe (Audit 2026-08-17).
        //
        // Das Polygon selbst wird weiter geliefert: Der Mensch soll seine
        // Markierung sehen: nur die erfundene Zahl entfaellt.
        bool istKalibriert = normalizedDiameter > 0;
        double? pipeRadiusPx = istKalibriert
            ? PipeRadiusPx(normalizedDiameter, renderWidth, renderHeight)
            : null;
        double? reductionPercent = pipeRadiusPx is { } radius
            ? CrossSectionPercent(polygonAreaPx, radius)
            : null;

        var geometry = new OverlayGeometry
        {
            ToolType = OverlayToolType.CrossSection,
            Points = points.Select(p => new NormalizedPoint(p.X, p.Y)).ToList(),
            FillPercent = reductionPercent is { } prozent ? Math.Round(prozent, 1) : null
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
        if (!double.IsFinite(start.X)
            || !double.IsFinite(start.Y)
            || !double.IsFinite(end.X)
            || !double.IsFinite(end.Y))
        {
            return null;
        }

        // Die Maus kann mit Capture ausserhalb des sichtbaren Fotos losgelassen
        // werden. Die SAM-Box muss trotzdem vollstaendig im Bild bleiben.
        double minX = Math.Clamp(Math.Min(start.X, end.X), 0.0, 1.0);
        double maxX = Math.Clamp(Math.Max(start.X, end.X), 0.0, 1.0);
        double minY = Math.Clamp(Math.Min(start.Y, end.Y), 0.0, 1.0);
        double maxY = Math.Clamp(Math.Max(start.Y, end.Y), 0.0, 1.0);
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
        => PhotoMeasurementAnglePlanBuilder.PositionDegToClockHour(positionDeg);

    /// <summary>
    /// Wandelt Uhrlage (Grad, 0° = 12 Uhr) in einen WPF-kompatiblen Radiant-Wert um,
    /// bei dem 0° = 3-Uhr-Richtung (Math.Cos/Sin-Konvention).
    /// Formel: (positionDeg - 90) * PI / 180.
    /// </summary>
    public static double PositionDegToRadians(double positionDeg)
        => PhotoMeasurementAnglePlanBuilder.PositionDegToRadians(positionDeg);

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
        => PhotoMeasurementAnglePlanBuilder.BuildAngleGeometry(
            toolType,
            pipeCenter,
            normalizedDiameter,
            positionDeg,
            angleDeg);

    /// <summary>
    /// Baut den vollstaendigen, UI-freien Zeichenplan fuer Abzweig- und Bogen-Overlays.
    /// </summary>
    public static PhotoMeasurementAngleOverlayPlan? BuildAngleOverlayPlan(
        OverlayToolType toolType,
        PipeCalibration calibration,
        double positionDeg,
        double angleDeg,
        double renderedX,
        double renderedY,
        double renderedWidth,
        double renderedHeight)
        => PhotoMeasurementAnglePlanBuilder.BuildAngleOverlayPlan(
            toolType,
            calibration,
            positionDeg,
            angleDeg,
            renderedX,
            renderedY,
            renderedWidth,
            renderedHeight);

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
        => PhotoMeasurementAnglePlanBuilder.BuildLateralOverlayPlan(
            centerX,
            centerY,
            pipeRadiusPx,
            positionRad,
            angleDeg);

    /// <summary>
    /// Plant einen Kreisbogen ohne WPF-Typen.
    /// </summary>
    public static PhotoMeasurementArcPlan BuildArcPlan(
        double centerX,
        double centerY,
        double radius,
        double startRad,
        double endRad)
        => PhotoMeasurementAnglePlanBuilder.BuildArcPlan(
            centerX,
            centerY,
            radius,
            startRad,
            endRad);

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
        => PhotoMeasurementAnglePlanBuilder.BuildBendOverlayPlan(
            centerX,
            centerY,
            pipeRadiusPx,
            positionRad,
            angleDeg,
            ringCount,
            axisSegmentCount);
}

public sealed record PhotoMeasurementAngleGeometry(
    OverlayGeometry Geometry,
    double PositionRad,
    double ClockHour,
    NormalizedPoint EdgePoint);

public sealed record PhotoMeasurementAngleOverlayPlan(
    OverlayGeometry Geometry,
    double ClockHour,
    double PositionRad,
    PhotoMeasurementCanvasPoint Center,
    double PipeRadius,
    PhotoMeasurementLateralOverlayPlan? Lateral,
    PhotoMeasurementBendOverlayPlan? Bend);

public sealed record PhotoMeasurementDeformationGeometry(
    OverlayGeometry Geometry,
    double DeformationPercent,
    double VerticalDistance,
    double HorizontalDistance,
    NormalizedPoint Top,
    NormalizedPoint Bottom,
    NormalizedPoint Right,
    NormalizedPoint Left);

public sealed record PhotoMeasurementCalibrationGeometry(
    double NormalizedDiameter,
    NormalizedPoint PipeCenter);

/// <summary>
/// Ergebnis der Querschnittsmessung. <see cref="ReductionPercent"/> und
/// <see cref="PipeRadiusPx"/> sind null, wenn keine Kalibrierung vorliegt —
/// dann ist die Verminderung nicht messbar und darf auch nicht geschaetzt werden.
/// </summary>
public sealed record PhotoMeasurementCrossSectionGeometry(
    OverlayGeometry Geometry,
    double? ReductionPercent,
    NormalizedPoint LabelPoint,
    double PolygonAreaPx,
    double? PipeRadiusPx);

public sealed record PhotoMeasurementLineGeometry(
    OverlayGeometry Geometry,
    double Millimeters,
    double NormalizedLength);

public sealed record PhotoMeasurementCanvasPoint(double X, double Y);

public sealed record PhotoMeasurementCanvasRect(double X, double Y, double Width, double Height);

public sealed record PhotoMeasurementDeformationPlan(
    OverlayGeometry Geometry,
    double DeformationPercent,
    double VerticalDistance,
    double HorizontalDistance,
    PhotoMeasurementCanvasPoint Top,
    PhotoMeasurementCanvasPoint Bottom,
    PhotoMeasurementCanvasPoint Left,
    PhotoMeasurementCanvasPoint Right,
    PhotoMeasurementCanvasPoint LabelPosition);

public sealed record PhotoMeasurementPipeCirclePlan(
    PhotoMeasurementCanvasPoint Center,
    double Radius,
    PhotoMeasurementCanvasPoint HorizontalStart,
    PhotoMeasurementCanvasPoint HorizontalEnd,
    PhotoMeasurementCanvasPoint VerticalStart,
    PhotoMeasurementCanvasPoint VerticalEnd);

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

public sealed record PhotoMeasurementArcPlan(
    PhotoMeasurementCanvasPoint Start,
    PhotoMeasurementCanvasPoint End,
    double Radius,
    bool IsLargeArc,
    bool IsClockwise);

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
