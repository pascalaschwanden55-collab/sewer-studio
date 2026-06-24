using System;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class LiveDetectionToggleControls
{
    public static void Uncheck(ToggleButton toggle)
    {
        ArgumentNullException.ThrowIfNull(toggle);

        toggle.IsChecked = false;
    }
}
