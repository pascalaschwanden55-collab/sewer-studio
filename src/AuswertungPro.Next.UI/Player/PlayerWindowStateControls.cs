using System;
using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerWindowStateControls
{
    public static void Track(Window window)
        => Track(window, WindowStateManager.Track);

    public static void Track(Window window, Action<Window> trackWindow)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(trackWindow);

        trackWindow(window);
    }
}
