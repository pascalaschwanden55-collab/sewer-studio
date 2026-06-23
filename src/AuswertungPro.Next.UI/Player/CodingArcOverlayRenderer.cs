using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public static class CodingArcOverlayRenderer
{
    public static bool Render(
        Canvas canvas,
        NormalizedPoint start,
        NormalizedPoint end,
        NormalizedPoint center,
        Brush stroke,
        System.Windows.Media.Effects.Effect? effect,
        string tag,
        bool dashed,
        Func<NormalizedPoint, Point> toPixel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(stroke);
        ArgumentNullException.ThrowIfNull(toPixel);

        var centerPx = toPixel(center);
        var startPx = toPixel(start);
        var endPx = toPixel(end);

        double radius = Math.Sqrt(Math.Pow(startPx.X - centerPx.X, 2) + Math.Pow(startPx.Y - centerPx.Y, 2));
        if (radius < 3)
            return false;

        double startAngle = Math.Atan2(startPx.X - centerPx.X, -(startPx.Y - centerPx.Y));
        double endAngle = Math.Atan2(endPx.X - centerPx.X, -(endPx.Y - centerPx.Y));
        double angleDiff = endAngle - startAngle;
        if (angleDiff < 0)
            angleDiff += 2 * Math.PI;

        var arcEnd = new Point(
            centerPx.X + radius * Math.Sin(endAngle),
            centerPx.Y - radius * Math.Cos(endAngle));

        var figure = new PathFigure { StartPoint = startPx, IsClosed = false };
        figure.Segments.Add(new ArcSegment(
            arcEnd,
            new Size(radius, radius),
            0,
            angleDiff > Math.PI,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = 3,
            Effect = effect,
            Tag = tag
        };
        if (dashed)
            path.StrokeDashArray = new DoubleCollection { 4, 2 };

        canvas.Children.Add(path);
        return true;
    }
}
