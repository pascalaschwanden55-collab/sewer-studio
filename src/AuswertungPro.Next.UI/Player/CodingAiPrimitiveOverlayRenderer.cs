using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingAiPrimitiveOverlayRenderStyle(
    Brush Stroke,
    Effect? Effect,
    string Tag);

public static class CodingAiPrimitiveOverlayRenderer
{
    /// <summary>
    /// Zeichnet KI-Linie, -Strecke oder -Punkt. Die Punkte gehen durch <paramref name="toPixel"/>,
    /// der das tatsaechlich sichtbare Videorechteck kennt (Rand bei 4:3 in 16:9 usw.).
    /// </summary>
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingAiPrimitiveOverlayRenderStyle style)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(toPixel);
        ArgumentNullException.ThrowIfNull(style);

        return overlay.ToolType switch
        {
            OverlayToolType.Line or OverlayToolType.Stretch => RenderLine(canvas, overlay, toPixel, style),
            OverlayToolType.Point => RenderPoint(canvas, overlay, toPixel, style),
            _ => false
        };
    }

    private static bool RenderLine(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingAiPrimitiveOverlayRenderStyle style)
    {
        if (overlay.Points.Count < 2)
            return false;

        var p1 = toPixel(overlay.Points[0]);
        var p2 = toPixel(overlay.Points[1]);
        var line = new System.Windows.Shapes.Line
        {
            X1 = p1.X,
            Y1 = p1.Y,
            X2 = p2.X,
            Y2 = p2.Y,
            Stroke = style.Stroke,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Tag = style.Tag,
            Effect = style.Effect
        };
        canvas.Children.Add(line);
        return true;
    }

    private static bool RenderPoint(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingAiPrimitiveOverlayRenderStyle style)
    {
        if (overlay.Points.Count < 1)
            return false;

        var center = toPixel(overlay.Points[0]);
        var px = center.X;
        var py = center.Y;
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = style.Stroke,
            Opacity = 0.8,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Tag = style.Tag,
            Effect = style.Effect
        };
        Canvas.SetLeft(dot, px - 7);
        Canvas.SetTop(dot, py - 7);
        canvas.Children.Add(dot);
        return true;
    }
}
