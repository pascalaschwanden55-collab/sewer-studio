using System;
using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerApplicationControls
{
    public static Window? CurrentMainWindow()
        => CurrentMainWindow(() => System.Windows.Application.Current?.MainWindow);

    public static Window? CurrentMainWindow(Func<Window?> resolveMainWindow)
    {
        ArgumentNullException.ThrowIfNull(resolveMainWindow);

        return resolveMainWindow();
    }
}
