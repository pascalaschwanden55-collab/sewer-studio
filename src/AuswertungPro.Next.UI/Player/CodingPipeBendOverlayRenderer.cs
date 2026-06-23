using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public static class CodingPipeBendOverlayRenderer
{
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        Effect? effect,
        string tag,
        string labelTag,
        Func<NormalizedPoint, Point> toPixel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(toPixel);

        var stroke = isPreview
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));

        if (overlay.Points.Count == 2)
        {
            RenderTwoPointPreview(canvas, overlay, stroke, effect, tag, toPixel);
            return true;
        }

        if (overlay.Points.Count < 3)
            return false;

        RenderBend(canvas, overlay, isPreview, stroke, effect, tag, labelTag, toPixel);
        return true;
    }

    private static void RenderTwoPointPreview(
        Canvas canvas,
        OverlayGeometry overlay,
        Brush stroke,
        Effect? effect,
        string tag,
        Func<NormalizedPoint, Point> toPixel)
    {
        var a = toPixel(overlay.Points[0]);
        var b = toPixel(overlay.Points[1]);
        var line = new System.Windows.Shapes.Line
        {
            X1 = a.X,
            Y1 = a.Y,
            X2 = b.X,
            Y2 = b.Y,
            Stroke = stroke,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Effect = effect,
            Tag = tag
        };
        canvas.Children.Add(line);

        CodingOverlayDotMarkerRenderer.Add(canvas, a, 6, stroke, tag, effect);
        CodingOverlayDotMarkerRenderer.Add(canvas, b, 6, stroke, tag, effect);
    }

    private static void RenderBend(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        Brush stroke,
        Effect? effect,
        string tag,
        string labelTag,
        Func<NormalizedPoint, Point> toPixel)
    {
        var p1 = toPixel(overlay.Points[0]);
        var vertex = toPixel(overlay.Points[1]);
        var p3 = toPixel(overlay.Points[2]);

        AddLeg(canvas, p1, vertex, stroke, effect, tag, isPreview);
        AddLeg(canvas, vertex, p3, stroke, effect, tag, isPreview);

        CodingOverlayDotMarkerRenderer.Add(canvas, p1, 6, stroke, tag, effect);
        CodingOverlayDotMarkerRenderer.Add(canvas, vertex, 8, stroke, tag, effect);
        CodingOverlayDotMarkerRenderer.Add(canvas, p3, 6, stroke, tag, effect);

        AddAngleArc(canvas, p1, vertex, p3, stroke, effect, tag);
        AddAngleLabel(canvas, overlay, vertex, stroke, effect, labelTag);
    }

    private static void AddLeg(
        Canvas canvas,
        Point from,
        Point to,
        Brush stroke,
        Effect? effect,
        string tag,
        bool dashed)
    {
        var line = new System.Windows.Shapes.Line
        {
            X1 = from.X,
            Y1 = from.Y,
            X2 = to.X,
            Y2 = to.Y,
            Stroke = stroke,
            StrokeThickness = 3,
            Effect = effect,
            Tag = tag
        };
        if (dashed)
            line.StrokeDashArray = new DoubleCollection { 4, 2 };
        canvas.Children.Add(line);
    }

    private static void AddAngleArc(
        Canvas canvas,
        Point p1,
        Point vertex,
        Point p3,
        Brush stroke,
        Effect? effect,
        string tag)
    {
        const double arcRadius = 30;
        var angle1 = Math.Atan2(p1.Y - vertex.Y, p1.X - vertex.X);
        var angle2 = Math.Atan2(p3.Y - vertex.Y, p3.X - vertex.X);

        var angleDiff = angle2 - angle1;
        if (angleDiff > Math.PI)
            angleDiff -= 2 * Math.PI;
        if (angleDiff < -Math.PI)
            angleDiff += 2 * Math.PI;

        var arcStart = new Point(
            vertex.X + arcRadius * Math.Cos(angle1),
            vertex.Y + arcRadius * Math.Sin(angle1));
        var arcEnd = new Point(
            vertex.X + arcRadius * Math.Cos(angle2),
            vertex.Y + arcRadius * Math.Sin(angle2));

        var arcFigure = new PathFigure { StartPoint = arcStart, IsClosed = false };
        arcFigure.Segments.Add(new ArcSegment(
            arcEnd,
            new Size(arcRadius, arcRadius),
            0,
            Math.Abs(angleDiff) > Math.PI,
            angleDiff > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
            true));

        var arcGeometry = new PathGeometry();
        arcGeometry.Figures.Add(arcFigure);
        var arcPath = new System.Windows.Shapes.Path
        {
            Data = arcGeometry,
            Stroke = stroke,
            StrokeThickness = 2,
            Effect = effect,
            Tag = tag
        };
        canvas.Children.Add(arcPath);
    }

    private static void AddAngleLabel(
        Canvas canvas,
        OverlayGeometry overlay,
        Point vertex,
        Brush stroke,
        Effect? effect,
        string labelTag)
    {
        if (!overlay.ArcDegrees.HasValue)
            return;

        var label = new TextBlock
        {
            Text = $"{overlay.ArcDegrees.Value:F1}\u00B0",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = stroke,
            Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
            Padding = new Thickness(6, 3, 6, 3),
            Effect = effect,
            Tag = labelTag
        };
        Canvas.SetLeft(label, vertex.X + 14);
        Canvas.SetTop(label, vertex.Y - 24);
        canvas.Children.Add(label);
    }
}
