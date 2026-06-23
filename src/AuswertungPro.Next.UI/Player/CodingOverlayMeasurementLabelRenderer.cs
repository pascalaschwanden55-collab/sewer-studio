using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AuswertungPro.Next.UI.Player;

public static class CodingOverlayMeasurementLabelRenderer
{
    public static TextBlock Add(
        Canvas canvas,
        Point anchor,
        string text,
        Effect? effect,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var label = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
            Padding = new Thickness(5, 2, 5, 2),
            Effect = effect,
            Tag = tag
        };
        Canvas.SetLeft(label, anchor.X + 12);
        Canvas.SetTop(label, anchor.Y - 20);
        canvas.Children.Add(label);
        return label;
    }
}
