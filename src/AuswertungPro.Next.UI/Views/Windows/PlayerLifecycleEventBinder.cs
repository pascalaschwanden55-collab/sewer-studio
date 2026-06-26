using System;
using System.ComponentModel;
using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerLifecycleEventBinder
{
    public static void Bind(
        Window window,
        RoutedEventHandler ensureVisibleOnLoaded,
        EventHandler deactivated,
        EventHandler activated,
        CancelEventHandler closing,
        RoutedEventHandler loaded,
        EventHandler closed)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(ensureVisibleOnLoaded);
        ArgumentNullException.ThrowIfNull(deactivated);
        ArgumentNullException.ThrowIfNull(activated);
        ArgumentNullException.ThrowIfNull(closing);
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(closed);

        window.Loaded += ensureVisibleOnLoaded;
        window.Deactivated += deactivated;
        window.Activated += activated;
        window.Closing += closing;
        window.Loaded += loaded;
        window.Closed += closed;
    }
}
