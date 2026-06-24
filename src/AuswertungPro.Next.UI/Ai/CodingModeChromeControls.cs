using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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

    public static void HideCodingSurface(
        Popup overlayPopup,
        Canvas overlayCanvas,
        FrameworkElement sidePanel,
        ColumnDefinition sidePanelColumn,
        FrameworkElement toolbar,
        FrameworkElement timelinePanel,
        FrameworkElement calibrationHint,
        FrameworkElement measurementPanel)
    {
        ArgumentNullException.ThrowIfNull(overlayPopup);
        ArgumentNullException.ThrowIfNull(overlayCanvas);
        ArgumentNullException.ThrowIfNull(sidePanel);
        ArgumentNullException.ThrowIfNull(sidePanelColumn);
        ArgumentNullException.ThrowIfNull(toolbar);
        ArgumentNullException.ThrowIfNull(timelinePanel);
        ArgumentNullException.ThrowIfNull(calibrationHint);
        ArgumentNullException.ThrowIfNull(measurementPanel);

        if (overlayCanvas.IsMouseCaptured)
            overlayCanvas.ReleaseMouseCapture();

        overlayPopup.IsOpen = false;
        overlayCanvas.Children.Clear();
        overlayCanvas.IsHitTestVisible = false;
        overlayCanvas.Cursor = Cursors.Arrow;
        sidePanel.Visibility = Visibility.Collapsed;
        sidePanelColumn.Width = new GridLength(0);
        toolbar.Visibility = Visibility.Collapsed;
        timelinePanel.Visibility = Visibility.Collapsed;
        calibrationHint.Visibility = Visibility.Collapsed;
        measurementPanel.Visibility = Visibility.Collapsed;
    }
}
