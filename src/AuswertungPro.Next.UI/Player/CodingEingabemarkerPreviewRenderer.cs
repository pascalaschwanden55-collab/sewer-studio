using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Player;

public static class CodingEingabemarkerPreviewRenderer
{
    public static System.Windows.Shapes.Rectangle Create(Canvas canvas, Point start)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var rect = new System.Windows.Shapes.Rectangle
        {
            Stroke = Brushes.Lime,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0))
        };
        Canvas.SetLeft(rect, start.X);
        Canvas.SetTop(rect, start.Y);
        rect.Width = 0;
        rect.Height = 0;
        canvas.Children.Add(rect);
        return rect;
    }

    public static void Update(System.Windows.Shapes.Rectangle rect, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(rect);

        Canvas.SetLeft(rect, bounds.X);
        Canvas.SetTop(rect, bounds.Y);
        rect.Width = bounds.Width;
        rect.Height = bounds.Height;
    }

    public static System.Windows.Shapes.Rectangle? Clear(
        Canvas canvas,
        System.Windows.Shapes.Rectangle? previewRect)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (previewRect is not null)
            canvas.Children.Remove(previewRect);

        return null;
    }
}
