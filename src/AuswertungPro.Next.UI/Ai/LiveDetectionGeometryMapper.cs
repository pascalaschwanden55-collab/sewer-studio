using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class LiveDetectionGeometryMapper
{
    public static int? ParseClockHour(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var match = Regex.Match(raw, @"\b(?<h>1[0-2]|0?[1-9])\b");
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            return null;

        if (hour == 0)
            return 12;
        if (hour > 12)
            hour %= 12;
        return hour == 0 ? 12 : hour;
    }

    public static Geometry BuildRingSectorGeometry(
        double cx,
        double cy,
        double innerR,
        double outerR,
        double startDeg,
        double sweepDeg)
    {
        var startRad = DegToRad(startDeg);
        var endRad = DegToRad(startDeg + sweepDeg);
        var large = sweepDeg > 180;

        var p1 = new Point(cx + Math.Cos(startRad) * outerR, cy + Math.Sin(startRad) * outerR);
        var p2 = new Point(cx + Math.Cos(endRad) * outerR, cy + Math.Sin(endRad) * outerR);
        var p3 = new Point(cx + Math.Cos(endRad) * innerR, cy + Math.Sin(endRad) * innerR);
        var p4 = new Point(cx + Math.Cos(startRad) * innerR, cy + Math.Sin(startRad) * innerR);

        var fig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, large, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(p3, true));
        fig.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, large, SweepDirection.Counterclockwise, true));
        return new PathGeometry(new[] { fig });
    }

    public static double DegToRad(double deg) => deg * Math.PI / 180.0;

    public static string? EstimateClockFromOverlayCenter(OverlayGeometry overlay)
    {
        if (overlay.Points.Count == 0)
            return null;

        var avgX = overlay.Points.Average(p => p.X);
        var avgY = overlay.Points.Average(p => p.Y);
        var angleDeg = Math.Atan2(avgY - 0.5, avgX - 0.5) * 180.0 / Math.PI;
        var clockAngle = (angleDeg + 90 + 360) % 360;
        var hour = (int)Math.Round(clockAngle / 30.0) % 12;
        if (hour == 0)
            hour = 12;
        return hour.ToString(CultureInfo.InvariantCulture);
    }

    public static bool BoxContainsVanishingPoint(OverlayGeometry? overlay, double vanishX, double vanishY)
    {
        if (overlay == null || overlay.Points.Count < 2)
            return false;

        double minX = overlay.Points.Min(p => p.X), maxX = overlay.Points.Max(p => p.X);
        double minY = overlay.Points.Min(p => p.Y), maxY = overlay.Points.Max(p => p.Y);
        const double tol = 0.05;
        return vanishX >= minX - tol && vanishX <= maxX + tol
            && vanishY >= minY - tol && vanishY <= maxY + tol;
    }

    public static NormalizedBoundingBox BBoxFromClockPosition(LiveFrameFinding finding)
    {
        double clockHour = 6;
        if (double.TryParse(finding.PositionClock, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            clockHour = parsed;

        double angleDeg = clockHour / 12.0 * 360.0 - 90.0;
        double angleRad = angleDeg * Math.PI / 180.0;

        double extent = (finding.ExtentPercent ?? 15) / 100.0;
        double boxSize = Math.Clamp(extent * 0.6, 0.08, 0.40);

        double cx = 0.5 + 0.35 * Math.Cos(angleRad);
        double cy = 0.5 + 0.35 * Math.Sin(angleRad);

        return new NormalizedBoundingBox
        {
            XCenter = Math.Clamp(cx, 0, 1),
            YCenter = Math.Clamp(cy, 0, 1),
            Width = Math.Clamp(boxSize, 0.08, 0.40),
            Height = Math.Clamp(boxSize, 0.08, 0.40)
        };
    }

    public static string ClickToClockPosition(Point click, Size canvasSize)
    {
        var cx = canvasSize.Width / 2.0;
        var cy = canvasSize.Height / 2.0;
        var dx = click.X - cx;
        var dy = click.Y - cy;

        var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        var clockAngle = (angleDeg + 90 + 360) % 360;
        var hour = (int)Math.Round(clockAngle / 30.0) % 12;
        if (hour == 0)
            hour = 12;

        return hour.ToString(CultureInfo.InvariantCulture);
    }
}
