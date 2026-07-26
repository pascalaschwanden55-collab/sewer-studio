using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Ai.Live;

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
        => RingSectorGeometry.Build(cx, cy, innerR, outerR, startDeg, sweepDeg);

    internal static double ClockHourToAngleDegrees(int hour)
    {
        var normalized = ((hour % 12) + 12) % 12;
        return -90 + normalized * 30;
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

    public static NormalizedBoundingBox BBoxFromOverlay(OverlayGeometry overlay)
        => NormalizedBoundingBox.FromPoints(overlay.Points);

    public static Rect? BBoxToCanvasRect(LiveFrameFinding finding, double canvasWidth, double canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
            return null;

        if (!finding.BboxX1.HasValue || !finding.BboxY1.HasValue
            || !finding.BboxX2.HasValue || !finding.BboxY2.HasValue)
            return null;

        var px1 = finding.BboxX1.Value * canvasWidth;
        var py1 = finding.BboxY1.Value * canvasHeight;
        var px2 = finding.BboxX2.Value * canvasWidth;
        var py2 = finding.BboxY2.Value * canvasHeight;

        return new Rect(
            Math.Min(px1, px2),
            Math.Min(py1, py2),
            Math.Max(1, Math.Abs(px2 - px1)),
            Math.Max(1, Math.Abs(py2 - py1)));
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
