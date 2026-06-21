using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void AddDotMarker(
        Point pos,
        double radius,
        Brush fill,
        string tag,
        System.Windows.Media.Effects.DropShadowEffect effect)
    {
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
        Canvas.SetLeft(dot, pos.X - radius);
        Canvas.SetTop(dot, pos.Y - radius);
        CodingOverlayCanvas.Children.Add(dot);
    }
}
