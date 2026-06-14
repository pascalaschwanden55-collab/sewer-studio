using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class ReferenceDnGeometry
{
    public static Rect BuildCircleRect(
        NormalizedPoint center,
        double normalizedDiameter,
        double canvasWidth,
        double canvasHeight)
    {
        if (normalizedDiameter <= 0 || canvasWidth <= 0 || canvasHeight <= 0)
            return Rect.Empty;

        var diameterPx = normalizedDiameter * Math.Min(canvasWidth, canvasHeight);
        var centerX = center.X * canvasWidth;
        var centerY = center.Y * canvasHeight;
        return new Rect(
            centerX - diameterPx / 2.0,
            centerY - diameterPx / 2.0,
            diameterPx,
            diameterPx);
    }
}
