using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows.Preview;

// Vorschau fuer das Arc-Werkzeug: zwei gelbe gestrichelte Linien vom
// Bild-Mittelpunkt (0.5, 0.5) zu Start- bzw. Current-Punkt — markiert
// den vom User gewaehlten Bogen-Sektor.
internal sealed class ArcPreviewRenderer : IPreviewToolRenderer
{
    public void Render(PreviewRenderContext ctx)
    {
        var center = ctx.ToPixel(new NormalizedPoint(0.5, 0.5));
        var p1 = ctx.ToPixel(ctx.Start);
        var p2 = ctx.ToPixel(ctx.Current);

        ctx.Canvas.Children.Add(new Line
        {
            X1 = center.X, Y1 = center.Y,
            X2 = p1.X, Y2 = p1.Y,
            Stroke = Brushes.Yellow,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Tag = ctx.PreviewTag
        });
        ctx.Canvas.Children.Add(new Line
        {
            X1 = center.X, Y1 = center.Y,
            X2 = p2.X, Y2 = p2.Y,
            Stroke = Brushes.Yellow,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Tag = ctx.PreviewTag
        });
    }
}
