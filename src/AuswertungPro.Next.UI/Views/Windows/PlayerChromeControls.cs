using System;
using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerChromeControls
{
    public static void EnableFocusable(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Focusable = true;
    }

    public static bool IsMinimized(Window? window)
        => window?.WindowState == WindowState.Minimized;

    public static void RestoreNormal(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.WindowState = WindowState.Normal;
    }
}
