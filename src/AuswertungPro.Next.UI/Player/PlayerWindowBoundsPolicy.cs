using System.Windows;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerWindowBoundsPolicy
{
    public static Rect ClampToWorkArea(Rect window, Rect workArea)
    {
        var width = window.Width;
        var height = window.Height;
        var left = window.Left;
        var top = window.Top;

        if (width > workArea.Width)
            width = workArea.Width - 20;
        if (height > workArea.Height)
            height = workArea.Height - 20;
        if (left < workArea.Left)
            left = workArea.Left;
        if (top < workArea.Top)
            top = workArea.Top;
        if (left + width > workArea.Right)
            left = workArea.Right - width;
        if (top + height > workArea.Bottom)
            top = workArea.Bottom - height;

        return new Rect(left, top, width, height);
    }
}
