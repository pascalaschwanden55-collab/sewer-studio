using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AuswertungPro.Next.UI.Player;

public static class CodingOverlayDotMarkerRenderer
{
    public static System.Windows.Shapes.Ellipse Add(
        Canvas canvas,
        Point position,
        double radius,
        Brush fill,
        string tag,
        Effect? effect)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(fill);

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = fill,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Effect = effect,
            Tag = tag
        };
        Canvas.SetLeft(dot, position.X - radius);
        Canvas.SetTop(dot, position.Y - radius);
        canvas.Children.Add(dot);
        return dot;
    }
}
