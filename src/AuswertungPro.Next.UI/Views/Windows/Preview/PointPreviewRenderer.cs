using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows.Preview;

// Vorschau fuer das Point-Werkzeug: kleiner roter Kreis (12px) mit
// weisser Outline am Start-Punkt. Current wird ignoriert — Point ist
// kein Drag-Werkzeug.
internal sealed class PointPreviewRenderer : IPreviewToolRenderer
{
    public void Render(PreviewRenderContext ctx)
    {
        var p1 = ctx.ToPixel(ctx.Start);
        var dot = new Ellipse
        {
            Width = 12, Height = 12,
            Fill = Brushes.Red,
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Tag = ctx.PreviewTag
        };
        Canvas.SetLeft(dot, p1.X - 6);
        Canvas.SetTop(dot, p1.Y - 6);
        ctx.Canvas.Children.Add(dot);
    }
}
