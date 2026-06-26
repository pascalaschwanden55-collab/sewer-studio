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

    public static Size GetCanvasSize(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        return new Size(overlayCanvas.Width, overlayCanvas.Height);
    }

    public static void SetCanvasSize(Canvas overlayCanvas, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        overlayCanvas.Width = width;
        overlayCanvas.Height = height;
    }

    public static Size GetCanvasActualSize(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        return new Size(overlayCanvas.ActualWidth, overlayCanvas.ActualHeight);
    }

    public static bool IsCanvasMouseCaptured(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        return overlayCanvas.IsMouseCaptured;
    }

    public static bool IsPopupOpen(Popup popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        return popup.IsOpen;
    }

    public static void OpenPopup(Popup popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        popup.IsOpen = true;
    }

    public static void ClosePopup(Popup popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        popup.IsOpen = false;
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

    public static void EnableDrawingCanvas(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        overlayCanvas.IsHitTestVisible = true;
        overlayCanvas.Cursor = Cursors.Cross;
    }

    public static void DisableDrawingCanvas(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        overlayCanvas.IsHitTestVisible = false;
        overlayCanvas.Cursor = Cursors.Arrow;
    }

    public static void CaptureCanvasMouse(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        overlayCanvas.CaptureMouse();
    }

    public static void ReleaseCanvasMouse(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        overlayCanvas.ReleaseMouseCapture();
    }

    public static void ResetCanvasCursor(Canvas overlayCanvas)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        overlayCanvas.Cursor = Cursors.Arrow;
    }

    public static void ApplyCanvasCursor(Canvas overlayCanvas, bool useCrossCursor)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);

        overlayCanvas.Cursor = useCrossCursor ? Cursors.Cross : Cursors.Arrow;
    }
}
