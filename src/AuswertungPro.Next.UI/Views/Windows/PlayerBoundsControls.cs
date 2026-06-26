using System;
using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerBoundsControls
{
    public static Rect EnsureVisibleOnScreen(Window window)
        => EnsureVisibleOnScreen(window, SystemParameters.WorkArea);

    public static Rect EnsureVisibleOnScreen(Window window, Rect workArea)
    {
        ArgumentNullException.ThrowIfNull(window);

        var bounds = PlayerWindowBoundsPolicy.ClampToWorkArea(
            new Rect(window.Left, window.Top, window.Width, window.Height),
            workArea);

        ApplyBounds(window, bounds);
        return bounds;
    }

    public static void ApplyBounds(Window window, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
    }
}
