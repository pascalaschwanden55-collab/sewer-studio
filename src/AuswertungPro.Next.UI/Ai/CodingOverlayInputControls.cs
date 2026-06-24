using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOverlayInputControls
{
    public static void ApplyActiveToolSelection(
        TextBlock activeToolLabel,
        ButtonBase createEventButton,
        string labelText)
    {
        ArgumentNullException.ThrowIfNull(activeToolLabel);
        ArgumentNullException.ThrowIfNull(createEventButton);

        activeToolLabel.Text = labelText;
        createEventButton.IsEnabled = false;
    }

    public static void SetCreateEventEnabled(ButtonBase createEventButton, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(createEventButton);

        createEventButton.IsEnabled = isEnabled;
    }

    public static void SuspendCanvas(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        if (overlayCanvas.IsMouseCaptured)
            overlayCanvas.ReleaseMouseCapture();

        overlayCanvas.IsHitTestVisible = false;
        overlayCanvas.Visibility = Visibility.Hidden;
        overlayCanvas.Cursor = Cursors.Arrow;
    }

    public static void ResumeCanvas(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        overlayCanvas.Visibility = Visibility.Visible;
        overlayCanvas.IsHitTestVisible = true;
    }
}
