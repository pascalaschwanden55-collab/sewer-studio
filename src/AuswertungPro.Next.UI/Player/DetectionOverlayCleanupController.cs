using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public static class DetectionOverlayCleanupController
{
    public static void ClearAll(Canvas canvas, FrameworkElement overlay, ItemsControl findingsList)
        => DetectionOverlayCleaner.ClearAll(canvas, overlay, findingsList);

    public static void ClearFindingsAndCanvas(Canvas canvas, ItemsControl findingsList)
        => DetectionOverlayCleaner.ClearFindingsAndCanvas(canvas, findingsList);

    public static void ClearFindings(ItemsControl findingsList)
        => DetectionOverlayCleaner.ClearFindings(findingsList);

    public static void ClearCanvas(Canvas canvas, FrameworkElement overlay, bool hideOverlay)
        => DetectionOverlayCleaner.ClearCanvas(canvas, overlay, hideOverlay);

    public static void ClearVisuals(Canvas canvas, FrameworkElement overlay)
        => DetectionOverlayCleaner.ClearVisuals(canvas, overlay);
}
