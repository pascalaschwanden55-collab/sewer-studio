using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingModeChromeControls
{
    public static void ShowCodingSurface(
        Popup overlayPopup,
        Canvas overlayCanvas,
        FrameworkElement sidePanel,
        ColumnDefinition sidePanelColumn,
        FrameworkElement toolbar,
        double sidePanelWidth)
    {
        ArgumentNullException.ThrowIfNull(overlayPopup);
        ArgumentNullException.ThrowIfNull(overlayCanvas);
        ArgumentNullException.ThrowIfNull(sidePanel);
        ArgumentNullException.ThrowIfNull(sidePanelColumn);
        ArgumentNullException.ThrowIfNull(toolbar);

        overlayPopup.IsOpen = true;
        overlayCanvas.IsHitTestVisible = true;
        sidePanel.Visibility = Visibility.Visible;
        sidePanelColumn.Width = new GridLength(sidePanelWidth);
        toolbar.Visibility = Visibility.Visible;
    }
}
