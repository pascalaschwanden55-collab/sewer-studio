using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.DataPage;

internal sealed class ListenEinfuegelinie(UIElement zeile, bool danach, Brush farbe) : Adorner(zeile)
{
    protected override void OnRender(DrawingContext drawingContext)
    {
        var y = danach ? AdornedElement.RenderSize.Height - 1 : 1;
        drawingContext.DrawLine(new Pen(farbe, 3), new Point(0, y), new Point(AdornedElement.RenderSize.Width, y));
    }
}
