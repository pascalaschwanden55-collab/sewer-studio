using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows.Preview;

// Vorschau fuer Line/Stretch/Ruler: lime-gestrichelte Gerade. Die drei
// Werkzeuge teilen sich den Code seit jeher; sie werden im Dispatch-
// Dictionary mit derselben Renderer-Instanz unter drei Keys registriert.
internal sealed class LinePreviewRenderer : IPreviewToolRenderer
{
    public void Render(PreviewRenderContext ctx)
    {
        var p1 = ctx.ToPixel(ctx.Start);
        var p2 = ctx.ToPixel(ctx.Current);

        ctx.Canvas.Children.Add(new Line
        {
            X1 = p1.X, Y1 = p1.Y,
            X2 = p2.X, Y2 = p2.Y,
            Stroke = Brushes.Lime,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = ctx.PreviewTag
        });
    }
}
