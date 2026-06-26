using System;
using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerClipboardControls
{
    public static bool TryCopyWindowToClipboard(Window window)
        => TryCopyWindowToClipboard(window, WindowClipboardCaptureService.TryCopyWindowToClipboard);

    public static bool TryCopyWindowToClipboard(Window window, Func<Window, bool> capture)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(capture);

        return capture(window);
    }
}
