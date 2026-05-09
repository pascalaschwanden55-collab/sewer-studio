using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows.Preview;

// Vorschau fuer das Freehand-Werkzeug: Polyline aus den bisher
// gesammelten Punkten von ctx.CurrentOverlay. Erfordert mindestens
// zwei Punkte; sonst kein Render. Start/Current werden ignoriert,
// weil Freehand seine Punkte schon im OverlayGeometry gesammelt hat.
internal sealed class FreehandPreviewRenderer : IPreviewToolRenderer
{
    public void Render(PreviewRenderContext ctx)
    {
        var preview = ctx.CurrentOverlay;
        if (preview == null || preview.Points.Count < 2) return;

        var polyline = new Polyline
        {
            Stroke = Brushes.HotPink,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Tag = ctx.PreviewTag
        };
        foreach (var pt in preview.Points)
        {
            var px = ctx.ToPixel(pt);
            polyline.Points.Add(new Point(px.X, px.Y));
        }
        ctx.Canvas.Children.Add(polyline);
    }
}
