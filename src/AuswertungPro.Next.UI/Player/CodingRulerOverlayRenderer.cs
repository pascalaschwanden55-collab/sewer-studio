using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public static class CodingRulerOverlayRenderer
{
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        Effect? effect,
        string tag,
        string labelTag,
        Func<NormalizedPoint, Point> toPixel,
        NormalizedPoint? labelAnchor)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(toPixel);

        if (overlay.Points.Count < 2)
            return false;

        var totalMm = overlay.Q1Mm ?? 0;
        if (totalMm <= 0)
            return false;

        var stroke = Brushes.White;
        var p1 = toPixel(overlay.Points[0]);
        var p2 = toPixel(overlay.Points[1]);

        var mainLine = new System.Windows.Shapes.Line
        {
            X1 = p1.X,
            Y1 = p1.Y,
            X2 = p2.X,
            Y2 = p2.Y,
            Stroke = stroke,
            StrokeThickness = 2.5,
            Effect = effect,
            Tag = tag
        };
        if (isPreview)
            mainLine.StrokeDashArray = new DoubleCollection { 4, 2 };
        canvas.Children.Add(mainLine);

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var lineLength = Math.Sqrt(dx * dx + dy * dy);
        if (lineLength < 10)
            return true;

        var normX = -dy / lineLength;
        var normY = dx / lineLength;
        var tickInterval = TickInterval(totalMm);
        var tickCount = (int)(totalMm / tickInterval);
        for (var i = 0; i <= tickCount; i++)
        {
            var t = (i * tickInterval) / totalMm;
            if (t > 1.0)
                break;

            var tx = p1.X + dx * t;
            var ty = p1.Y + dy * t;
            var isMajor = i % 5 == 0;
            var tickLength = isMajor ? 10 : 5;

            var tick = new System.Windows.Shapes.Line
            {
                X1 = tx - normX * tickLength,
                Y1 = ty - normY * tickLength,
                X2 = tx + normX * tickLength,
                Y2 = ty + normY * tickLength,
                Stroke = stroke,
                StrokeThickness = isMajor ? 1.5 : 1,
                Effect = effect,
                Tag = tag
            };
            canvas.Children.Add(tick);

            if (isMajor && i > 0)
            {
                var tickLabel = new TextBlock
                {
                    Text = $"{(int)(i * tickInterval)}",
                    FontSize = 9,
                    Foreground = stroke,
                    Tag = tag
                };
                Canvas.SetLeft(tickLabel, tx + normX * 14 - 8);
                Canvas.SetTop(tickLabel, ty + normY * 14 - 6);
                canvas.Children.Add(tickLabel);
            }
        }

        foreach (var point in new[] { p1, p2 })
        {
            var endTick = new System.Windows.Shapes.Line
            {
                X1 = point.X - normX * 12,
                Y1 = point.Y - normY * 12,
                X2 = point.X + normX * 12,
                Y2 = point.Y + normY * 12,
                Stroke = stroke,
                StrokeThickness = 2,
                Effect = effect,
                Tag = tag
            };
            canvas.Children.Add(endTick);
        }

        var anchorPoint = labelAnchor is not null
            ? toPixel(labelAnchor)
            : new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
        var totalLabel = new TextBlock
        {
            Text = $"{totalMm:F1} mm",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = stroke,
            Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
            Padding = new Thickness(6, 3, 6, 3),
            Effect = effect,
            Tag = labelTag
        };
        Canvas.SetLeft(totalLabel, anchorPoint.X + 12);
        Canvas.SetTop(totalLabel, anchorPoint.Y - 20);
        canvas.Children.Add(totalLabel);
        return true;
    }

    private static double TickInterval(double totalMm)
    {
        if (totalMm > 500)
            return 100;
        if (totalMm > 200)
            return 50;
        if (totalMm > 50)
            return 10;
        return 5;
    }
}
