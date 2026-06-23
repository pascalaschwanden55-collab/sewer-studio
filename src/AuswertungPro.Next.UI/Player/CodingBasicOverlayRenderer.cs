using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingBasicOverlayRenderStyle(
    bool IsPreview,
    Brush Stroke,
    Brush Fill,
    Effect? Effect,
    string Tag);

public static class CodingBasicOverlayRenderer
{
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingBasicOverlayRenderStyle style)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(toPixel);
        ArgumentNullException.ThrowIfNull(style);

        return overlay.ToolType switch
        {
            OverlayToolType.Line or OverlayToolType.Stretch => RenderLine(canvas, overlay, toPixel, style),
            OverlayToolType.Rectangle => RenderRectangle(canvas, overlay, toPixel, style),
            OverlayToolType.Point => RenderPoint(canvas, overlay, toPixel, style),
            OverlayToolType.Ellipse => RenderEllipse(canvas, overlay, toPixel, style),
            OverlayToolType.Freehand => RenderFreehand(canvas, overlay, toPixel, style),
            _ => false
        };
    }

    private static bool RenderLine(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingBasicOverlayRenderStyle style)
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
            StrokeThickness = 3,
            Effect = style.Effect,
            Tag = style.Tag
        };
        if (style.IsPreview)
            line.StrokeDashArray = new DoubleCollection { 4, 2 };

        canvas.Children.Add(line);
        return true;
    }

    private static bool RenderRectangle(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingBasicOverlayRenderStyle style)
    {
        if (overlay.Points.Count < 4)
            return false;

        var pix = overlay.Points.Select(toPixel).ToList();
        double minX = pix.Min(p => p.X);
        double maxX = pix.Max(p => p.X);
        double minY = pix.Min(p => p.Y);
        double maxY = pix.Max(p => p.Y);

        var rect = new Rectangle
        {
            Width = Math.Max(1, maxX - minX),
            Height = Math.Max(1, maxY - minY),
            Stroke = style.Stroke,
            StrokeThickness = 3,
            Fill = style.Fill,
            Effect = style.Effect,
            Tag = style.Tag
        };
        if (style.IsPreview)
            rect.StrokeDashArray = new DoubleCollection { 4, 2 };

        Canvas.SetLeft(rect, minX);
        Canvas.SetTop(rect, minY);
        canvas.Children.Add(rect);
        return true;
    }

    private static bool RenderPoint(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingBasicOverlayRenderStyle style)
    {
        if (overlay.Points.Count < 1)
            return false;

        var p = toPixel(overlay.Points[0]);
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = style.Stroke,
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Effect = style.Effect,
            Tag = style.Tag
        };
        Canvas.SetLeft(dot, p.X - 8);
        Canvas.SetTop(dot, p.Y - 8);
        canvas.Children.Add(dot);
        return true;
    }

    private static bool RenderEllipse(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingBasicOverlayRenderStyle style)
    {
        if (overlay.Points.Count < 2)
            return false;

        var ep1 = toPixel(overlay.Points[0]);
        var ep2 = toPixel(overlay.Points[1]);
        var elli = new System.Windows.Shapes.Ellipse
        {
            Width = Math.Max(1, Math.Abs(ep2.X - ep1.X)),
            Height = Math.Max(1, Math.Abs(ep2.Y - ep1.Y)),
            Stroke = style.IsPreview ? Brushes.MediumPurple : new SolidColorBrush(Color.FromRgb(147, 112, 219)),
            StrokeThickness = style.IsPreview ? 2 : 2.5,
            Fill = new SolidColorBrush(Color.FromArgb(30, 147, 112, 219)),
            Effect = style.Effect,
            Tag = style.Tag
        };
        if (style.IsPreview)
            elli.StrokeDashArray = new DoubleCollection { 4, 2 };

        Canvas.SetLeft(elli, Math.Min(ep1.X, ep2.X));
        Canvas.SetTop(elli, Math.Min(ep1.Y, ep2.Y));
        canvas.Children.Add(elli);
        return true;
    }

    private static bool RenderFreehand(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        CodingBasicOverlayRenderStyle style)
    {
        if (overlay.Points.Count < 3)
            return false;

        var poly = new System.Windows.Shapes.Polygon
        {
            Stroke = style.IsPreview ? Brushes.HotPink : new SolidColorBrush(Color.FromRgb(255, 105, 180)),
            StrokeThickness = style.IsPreview ? 2 : 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = new SolidColorBrush(Color.FromArgb(25, 255, 105, 180)),
            Effect = style.Effect,
            Tag = style.Tag
        };
        if (style.IsPreview)
            poly.StrokeDashArray = new DoubleCollection { 3, 2 };

        foreach (var pt in overlay.Points)
            poly.Points.Add(toPixel(pt));

        canvas.Children.Add(poly);
        return true;
    }
}
