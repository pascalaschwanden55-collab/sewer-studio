using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows.Preview;

// Vorschau fuer das Ellipse-Werkzeug: lila gestrichelte Ellipse mit
// halbtransparentem Fill, von Start-Punkt zu Current aufgespannt
// (gleiche BoundingBox wie das Rectangle-Werkzeug, nur als Ellipse).
internal sealed class EllipsePreviewRenderer : IPreviewToolRenderer
{
    public void Render(PreviewRenderContext ctx)
    {
        var p1 = ctx.ToPixel(ctx.Start);
        var p2 = ctx.ToPixel(ctx.Current);

        var ellipse = new Ellipse
        {
            Width = Math.Abs(p2.X - p1.X),
            Height = Math.Abs(p2.Y - p1.Y),
            Stroke = Brushes.MediumPurple,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(30, 147, 112, 219)),
            Tag = ctx.PreviewTag
        };
        Canvas.SetLeft(ellipse, Math.Min(p1.X, p2.X));
        Canvas.SetTop(ellipse, Math.Min(p1.Y, p2.Y));
        ctx.Canvas.Children.Add(ellipse);
    }
}
