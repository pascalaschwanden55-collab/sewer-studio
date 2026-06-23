using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public static class CodingSchemaOverlayRenderer
{
    public static System.Windows.Shapes.Ellipse AddPipeReference(
        Canvas canvas,
        NormalizedPoint centerNorm,
        double radiusNorm,
        double canvasWidth,
        double canvasHeight,
        Brush stroke,
        Effect? effect,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(stroke);

        var center = new Point(centerNorm.X * canvasWidth, centerNorm.Y * canvasHeight);
        var radiusPx = radiusNorm * Math.Min(canvasWidth, canvasHeight);
        var pipe = new System.Windows.Shapes.Ellipse
        {
            Width = radiusPx * 2,
            Height = radiusPx * 2,
            Stroke = stroke,
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Effect = effect,
            Tag = tag
        };
        Canvas.SetLeft(pipe, center.X - radiusPx);
        Canvas.SetTop(pipe, center.Y - radiusPx);
        canvas.Children.Add(pipe);
        return pipe;
    }

    public static TextBlock AddLabel(
        Canvas canvas,
        Point anchor,
        string text,
        Brush foreground,
        Effect? effect,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(foreground);

        var label = new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            Background = new SolidColorBrush(Color.FromArgb(205, 17, 19, 24)),
            Padding = new Thickness(6, 3, 6, 3),
            Effect = effect,
            Tag = tag
        };
        Canvas.SetLeft(label, anchor.X + 12);
        Canvas.SetTop(label, anchor.Y - 20);
        canvas.Children.Add(label);
        return label;
    }
}
