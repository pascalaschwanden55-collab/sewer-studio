using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public static class CodingOverlayCanvasCleaner
{
    public static void ClearTransient(Canvas canvas, bool clearManualOverlay)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var remove = canvas.Children
            .OfType<FrameworkElement>()
            .Where(el => CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(el.Tag, clearManualOverlay))
            .ToList();

        foreach (var element in remove)
            canvas.Children.Remove(element);
    }
}
