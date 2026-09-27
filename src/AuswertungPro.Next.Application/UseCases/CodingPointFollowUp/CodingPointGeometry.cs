using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Konservativer Bildvergleich, keine behauptete Objekt- oder Kameraprojektion.</summary>
public static class CodingPointGeometry
{
    public const double MinimumIou = 0.3;
    public const double MaximumCenterDistance = 0.15;

    public static bool HasBox(OverlayGeometry? overlay) => TryBox(overlay, out _);

    public static bool Matches(OverlayGeometry? first, OverlayGeometry? second)
    {
        if (!TryBox(first, out var a) || !TryBox(second, out var b)) return false;
        var intersection = Math.Max(0, Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1))
            * Math.Max(0, Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1));
        var union = (a.X2 - a.X1) * (a.Y2 - a.Y1) + (b.X2 - b.X1) * (b.Y2 - b.Y1) - intersection;
        var dx = (a.X1 + a.X2 - b.X1 - b.X2) / 2;
        var dy = (a.Y1 + a.Y2 - b.Y1 - b.Y2) / 2;
        return intersection / union >= MinimumIou
            && Math.Sqrt(dx * dx + dy * dy) <= MaximumCenterDistance;
    }

    public static OverlayGeometry? FromFinding(LiveFrameFinding finding)
    {
        if (finding.BboxX1 is not { } x1 || finding.BboxY1 is not { } y1
            || finding.BboxX2 is not { } x2 || finding.BboxY2 is not { } y2) return null;
        return new OverlayGeometry { ToolType = OverlayToolType.Rectangle,
            Points = [new(x1, y1), new(x2, y1), new(x2, y2), new(x1, y2)] };
    }

    private static bool TryBox(OverlayGeometry? overlay, out (double X1, double Y1, double X2, double Y2) box)
    {
        box = default;
        if (overlay?.ToolType != OverlayToolType.Rectangle || overlay.Points.Count != 4) return false;
        var p = overlay.Points;
        if (p.Any(v => !double.IsFinite(v.X) || !double.IsFinite(v.Y) || v.X < 0 || v.X > 1 || v.Y < 0 || v.Y > 1)
            || p[0].X != p[3].X || p[1].X != p[2].X || p[0].Y != p[1].Y || p[2].Y != p[3].Y
            || p[2].X <= p[0].X || p[2].Y <= p[0].Y) return false;
        box = (p[0].X, p[0].Y, p[2].X, p[2].Y);
        return true;
    }
}
