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
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        double canvasWidth,
        double canvasHeight,
        CodingAiPrimitiveOverlayRenderStyle style)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(style);

        return overlay.ToolType switch
        {
            OverlayToolType.Line or OverlayToolType.Stretch => RenderLine(canvas, overlay, canvasWidth, canvasHeight, style),
            OverlayToolType.Point => RenderPoint(canvas, overlay, canvasWidth, canvasHeight, style),
            _ => false
        };
    }

    private static bool RenderLine(
        Canvas canvas,
        OverlayGeometry overlay,
        double canvasWidth,
        double canvasHeight,
        CodingAiPrimitiveOverlayRenderStyle style)
    {
        if (overlay.Points.Count < 2)
            return false;

        var line = new System.Windows.Shapes.Line
        {
            X1 = overlay.Points[0].X * canvasWidth,
            Y1 = overlay.Points[0].Y * canvasHeight,
            X2 = overlay.Points[1].X * canvasWidth,
            Y2 = overlay.Points[1].Y * canvasHeight,
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
        double canvasWidth,
        double canvasHeight,
        CodingAiPrimitiveOverlayRenderStyle style)
    {
        if (overlay.Points.Count < 1)
            return false;

        var px = overlay.Points[0].X * canvasWidth;
        var py = overlay.Points[0].Y * canvasHeight;
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
