using System.Windows;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingEingabemarkerGeometryPolicy
{
    private const double MinimumNormalizedSelectionSize = 0.02;

    public static Rect BuildPreviewRect(Point start, Point end)
        => new(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));

    public static Rect? BuildNormalizedSelection(Point start, Point end, Size canvasSize)
    {
        if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
            return null;

        var x1 = Math.Min(start.X, end.X) / canvasSize.Width;
        var y1 = Math.Min(start.Y, end.Y) / canvasSize.Height;
        var x2 = Math.Max(start.X, end.X) / canvasSize.Width;
        var y2 = Math.Max(start.Y, end.Y) / canvasSize.Height;

        var width = x2 - x1;
        var height = y2 - y1;
        if (width < MinimumNormalizedSelectionSize || height < MinimumNormalizedSelectionSize)
            return null;

        return new Rect(x1, y1, width, height);
    }
}
