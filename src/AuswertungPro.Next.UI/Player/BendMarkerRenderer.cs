using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Player;

public static class BendMarkerRenderer
{
    public static void Show(Canvas canvas, double vanishX, double vanishY, Rect contentRect)
    {
        var centerX = contentRect.X + vanishX * contentRect.Width;
        var centerY = contentRect.Y + vanishY * contentRect.Height;
        var radius = Math.Max(24, Math.Min(contentRect.Width, contentRect.Height) * 0.10);

        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(40, 0x22, 0xC5, 0x5E)),
            IsHitTestVisible = false,
            Tag = OverlayTags.BendMarker
        };
        Canvas.SetLeft(ring, centerX - radius);
        Canvas.SetTop(ring, centerY - radius);
        canvas.Children.Add(ring);

        var label = new TextBlock
        {
            Text = "Bogen erkannt",
            Foreground = new SolidColorBrush(Colors.White),
            Background = new SolidColorBrush(Color.FromArgb(200, 0x22, 0xC5, 0x5E)),
            Padding = new Thickness(4, 1, 4, 1),
            FontSize = 12,
            IsHitTestVisible = false,
            Tag = OverlayTags.BendMarker
        };
        Canvas.SetLeft(label, centerX - radius);
        Canvas.SetTop(label, Math.Max(0, centerY - radius - 20));
        canvas.Children.Add(label);
    }

    public static void Clear(Canvas canvas)
    {
        for (var i = canvas.Children.Count - 1; i >= 0; i--)
        {
            if (canvas.Children[i] is FrameworkElement { Tag: string tag }
                && tag == OverlayTags.BendMarker)
            {
                canvas.Children.RemoveAt(i);
            }
        }
    }
}
