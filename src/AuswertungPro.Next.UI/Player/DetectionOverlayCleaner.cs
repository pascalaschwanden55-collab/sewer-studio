using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public static class DetectionOverlayCleaner
{
    public static void ClearAll(Canvas canvas, FrameworkElement overlay, ItemsControl findingsList)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(findingsList);

        ClearVisuals(canvas, overlay);
        findingsList.ItemsSource = null;
    }

    public static void ClearVisuals(Canvas canvas, FrameworkElement overlay)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);

        canvas.Children.Clear();
        overlay.Visibility = Visibility.Collapsed;
    }
}
