using System;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerToggleButtonControls
{
    public static bool IsChecked(ToggleButton toggle)
    {
        ArgumentNullException.ThrowIfNull(toggle);

        return toggle.IsChecked == true;
    }

    public static void Uncheck(ToggleButton toggle)
    {
        ArgumentNullException.ThrowIfNull(toggle);

        toggle.IsChecked = false;
    }
}
