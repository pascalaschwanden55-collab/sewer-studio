using System;
using System.Windows;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerSurfaceEventBinder
{
    public static void Bind(
        FrameworkElement damageMarkerSurface,
        FrameworkElement heatmapSurface,
        UIElement detectionSurface,
        FrameworkElement videoSurface,
        Window window,
        SizeChangedEventHandler damageMarkerSizeChanged,
        SizeChangedEventHandler heatmapSizeChanged,
        MouseButtonEventHandler detectionMouseLeftButtonDown,
        SizeChangedEventHandler videoSizeChanged,
        SizeChangedEventHandler windowSizeChanged,
        EventHandler windowLocationChanged)
    {
        ArgumentNullException.ThrowIfNull(damageMarkerSurface);
        ArgumentNullException.ThrowIfNull(heatmapSurface);
        ArgumentNullException.ThrowIfNull(detectionSurface);
        ArgumentNullException.ThrowIfNull(videoSurface);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(damageMarkerSizeChanged);
        ArgumentNullException.ThrowIfNull(heatmapSizeChanged);
        ArgumentNullException.ThrowIfNull(detectionMouseLeftButtonDown);
        ArgumentNullException.ThrowIfNull(videoSizeChanged);
        ArgumentNullException.ThrowIfNull(windowSizeChanged);
        ArgumentNullException.ThrowIfNull(windowLocationChanged);

        damageMarkerSurface.SizeChanged += damageMarkerSizeChanged;
        heatmapSurface.SizeChanged += heatmapSizeChanged;
        detectionSurface.MouseLeftButtonDown += detectionMouseLeftButtonDown;
        videoSurface.SizeChanged += videoSizeChanged;
        window.SizeChanged += windowSizeChanged;
        window.LocationChanged += windowLocationChanged;
    }
}
