using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public static class CodingBendMarkerOverlayController
{
    public static void Show(Canvas canvas, double vanishX, double vanishY, Rect contentRect)
        => BendMarkerRenderer.Show(canvas, vanishX, vanishY, contentRect);

    public static void Clear(Canvas canvas)
        => BendMarkerRenderer.Clear(canvas);
}
