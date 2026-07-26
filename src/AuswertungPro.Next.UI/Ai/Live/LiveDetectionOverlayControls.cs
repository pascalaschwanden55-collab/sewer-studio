using System;
using System.Windows;

namespace AuswertungPro.Next.UI.Ai.Live;

public static class LiveDetectionOverlayControls
{
    public static void Show(UIElement overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        overlay.Visibility = Visibility.Visible;
    }
}
