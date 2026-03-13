using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Erzeugt OverlayGeometry aus KI-Findings (BBox → Zeichnung).
/// Wird verwendet wenn die Multi-Model-Pipeline (DINO/SAM) BBox-Daten liefert.
/// </summary>
public static class AiOverlayGeometryBuilder
{
    /// <summary>
    /// Erzeugt eine OverlayGeometry aus einem LiveFrameFinding.
    /// Ohne BBox-Daten (Ollama-only Pfad) wird null zurueckgegeben.
    /// </summary>
    public static OverlayGeometry? BuildFromFinding(
        LiveFrameFinding finding, PipeCalibration? calibration)
    {
        if (!finding.HasBbox) return null;

        var toolType = InferToolType(finding);
        return toolType switch
        {
            OverlayToolType.Line => BuildLine(finding, calibration),
            OverlayToolType.Rectangle => BuildRect(finding, calibration),
            OverlayToolType.Arc => BuildArc(finding, calibration),
            OverlayToolType.Point => BuildPoint(finding, calibration),
            _ => BuildRect(finding, calibration) // Fallback: BBox als Rechteck
        };
    }

    /// <summary>
    /// Werkzeugtyp anhand des VSA-Codes / Labels ableiten.
    /// </summary>
    private static OverlayToolType InferToolType(LiveFrameFinding finding)
    {
        var code = (finding.VsaCodeHint ?? "").ToUpperInvariant();
        var label = (finding.Label ?? "").ToLowerInvariant();

        // Risse → Linie (Laenge/Breite)
        if (code.StartsWith("BAA") || code.StartsWith("BAB") || code.StartsWith("BAC") ||
            label.Contains("riss") || label.Contains("crack"))
            return OverlayToolType.Line;

        // Umfangsschaeden → Bogen
        if (code.StartsWith("BAF") || code.StartsWith("BAG") ||
            label.Contains("umfang") || label.Contains("circumferential"))
            return OverlayToolType.Arc;

        // Punktschaeden (Loch, Einzelstelle)
        if (code.StartsWith("BAJ") ||
            label.Contains("loch") || label.Contains("hole") || label.Contains("punkt"))
            return OverlayToolType.Point;

        // Default: Flaechenschaden → Rechteck
        return OverlayToolType.Rectangle;
    }

    private static OverlayGeometry BuildLine(LiveFrameFinding f, PipeCalibration? cal)
    {
        // Linie von BBox-Mitte-Links nach BBox-Mitte-Rechts
        double y = (f.BboxY1Norm!.Value + f.BboxY2Norm!.Value) / 2.0;
        var start = new NormalizedPoint(f.BboxX1Norm!.Value, y);
        var end = new NormalizedPoint(f.BboxX2Norm!.Value, y);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Line,
            Points = new List<NormalizedPoint> { start, end }
        };

        SetClockPositions(geo, start, end, cal);

        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lengthNorm = Math.Sqrt(dx * dx + dy * dy);
        geo.Q1Mm = cal != null ? cal.NormToMm(lengthNorm) : lengthNorm * 500;

        return geo;
    }

    private static OverlayGeometry BuildRect(LiveFrameFinding f, PipeCalibration? cal)
    {
        var p1 = new NormalizedPoint(f.BboxX1Norm!.Value, f.BboxY1Norm!.Value);
        var p2 = new NormalizedPoint(f.BboxX2Norm!.Value, f.BboxY1Norm!.Value);
        var p3 = new NormalizedPoint(f.BboxX2Norm!.Value, f.BboxY2Norm!.Value);
        var p4 = new NormalizedPoint(f.BboxX1Norm!.Value, f.BboxY2Norm!.Value);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = new List<NormalizedPoint> { p1, p2, p3, p4 }
        };

        double widthNorm = Math.Abs(f.BboxX2Norm!.Value - f.BboxX1Norm!.Value);
        double heightNorm = Math.Abs(f.BboxY2Norm!.Value - f.BboxY1Norm!.Value);

        geo.Q1Mm = cal != null ? cal.NormToMm(heightNorm) : heightNorm * 500;
        geo.Q2Mm = cal != null ? cal.NormToMm(widthNorm) : widthNorm * 500;

        // Uhrposition am Zentroid oder BBox-Mitte
        var center = new NormalizedPoint(
            f.CentroidXNorm ?? (f.BboxX1Norm!.Value + f.BboxX2Norm!.Value) / 2.0,
            f.CentroidYNorm ?? (f.BboxY1Norm!.Value + f.BboxY2Norm!.Value) / 2.0);
        geo.ClockFrom = PointToClockHour(center, cal);

        return geo;
    }

    private static OverlayGeometry BuildArc(LiveFrameFinding f, PipeCalibration? cal)
    {
        // Bogen: BBox-Oberkante links → BBox-Oberkante rechts
        var start = new NormalizedPoint(f.BboxX1Norm!.Value, f.BboxY1Norm!.Value);
        var end = new NormalizedPoint(f.BboxX2Norm!.Value, f.BboxY1Norm!.Value);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Arc,
            Points = new List<NormalizedPoint> { start, end }
        };

        SetClockPositions(geo, start, end, cal);

        // Bogenwinkel aus Clock-Positionen
        if (geo.ClockFrom.HasValue && geo.ClockTo.HasValue)
        {
            double fromDeg = geo.ClockFrom.Value * 30.0;
            double toDeg = geo.ClockTo.Value * 30.0;
            double arc = toDeg - fromDeg;
            if (arc < 0) arc += 360;
            geo.ArcDegrees = arc;
        }

        return geo;
    }

    private static OverlayGeometry BuildPoint(LiveFrameFinding f, PipeCalibration? cal)
    {
        var point = new NormalizedPoint(
            f.CentroidXNorm ?? (f.BboxX1Norm!.Value + f.BboxX2Norm!.Value) / 2.0,
            f.CentroidYNorm ?? (f.BboxY1Norm!.Value + f.BboxY2Norm!.Value) / 2.0);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Point,
            Points = new List<NormalizedPoint> { point }
        };

        geo.ClockFrom = PointToClockHour(point, cal);
        return geo;
    }

    // --- Hilfsmethoden ---

    private static void SetClockPositions(OverlayGeometry geo, NormalizedPoint start, NormalizedPoint end, PipeCalibration? cal)
    {
        geo.ClockFrom = PointToClockHour(start, cal);
        geo.ClockTo = PointToClockHour(end, cal);
    }

    private static double PointToClockHour(NormalizedPoint point, PipeCalibration? cal)
    {
        if (cal != null)
            return cal.PointToClockHour(point);

        // Fallback: Bildmitte als Rohrmitte
        var fallback = new PipeCalibration { NominalDiameterMm = 300 };
        return fallback.PointToClockHour(point);
    }
}
