using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public static class CodingLateralCircleOverlayRenderer
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

        if (overlay.Points.Count < 2)
            return false;

        var stroke = isPreview
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x69, 0xB4))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xFF));
        var fill = new SolidColorBrush(Color.FromArgb(30, 0xFF, 0x00, 0xFF));

        var center = toPixel(overlay.Points[0]);
        var edge = toPixel(overlay.Points[1]);
        var radius = Math.Sqrt(Math.Pow(edge.X - center.X, 2) + Math.Pow(edge.Y - center.Y, 2));

        if (radius < 3)
            return false;

        var circle = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = stroke,
            StrokeThickness = 2.5,
            Fill = fill,
            Effect = effect,
            Tag = tag
        };
        if (isPreview)
            circle.StrokeDashArray = new DoubleCollection { 4, 2 };
        Canvas.SetLeft(circle, center.X - radius);
        Canvas.SetTop(circle, center.Y - radius);
        canvas.Children.Add(circle);

        CodingOverlayDotMarkerRenderer.Add(canvas, center, 5, stroke, tag, effect);

        var radiusLine = new System.Windows.Shapes.Line
        {
            X1 = center.X,
            Y1 = center.Y,
            X2 = edge.X,
            Y2 = edge.Y,
            Stroke = stroke,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Effect = effect,
            Tag = tag
        };
        canvas.Children.Add(radiusLine);

        var labelText = BuildLabelText(overlay);
        if (!string.IsNullOrWhiteSpace(labelText))
        {
            var label = new TextBlock
            {
                Text = labelText,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = stroke,
                Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
                Padding = new Thickness(6, 3, 6, 3),
                Effect = effect,
                Tag = labelTag
            };
            Canvas.SetLeft(label, center.X + radius + 8);
            Canvas.SetTop(label, center.Y - 12);
            canvas.Children.Add(label);
        }

        return true;
    }

    private static string BuildLabelText(OverlayGeometry overlay)
    {
        var parts = new List<string>();
        if (overlay.Q1Mm.HasValue)
            parts.Add($"DN {overlay.Q1Mm.Value:F0}");
        if (overlay.DnRatioPercent.HasValue)
            parts.Add($"({overlay.DnRatioPercent.Value:F0}% v. Haupt-DN)");
        return string.Join(" ", parts);
    }
}
