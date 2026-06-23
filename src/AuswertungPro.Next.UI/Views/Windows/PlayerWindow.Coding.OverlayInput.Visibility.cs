using System.Windows;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void SuspendCodingOverlayInput()
    {
        _codingOverlaySuspendDepth++;
        if (_codingOverlaySuspendDepth > 1)
            return;

        if (CodingOverlayCanvas.IsMouseCaptured)
            CodingOverlayCanvas.ReleaseMouseCapture();
        _codingSchemaManager.EndDrag();
        _codingOverlayService?.CancelDraw();
        _codingOverlayWasOpenBeforeSuspend = CodingOverlayPopup.IsOpen;
        CodingOverlayCanvas.IsHitTestVisible = false;
        // Das Popup ist ein eigenes transparentes Top-Level-HWND und liegt grafisch
        // UEBER eigenen Dialogen (Loeschen-Bestaetigung, VsaCodeExplorer). IsHitTestVisible=false
        // nimmt nur die Maus weg, der #01000000-Schleier + Kreise bleiben sichtbar und stoeren.
        // Canvas-Inhalt zusaetzlich ausblenden (NICHT Popup.IsOpen togglen -> kein HWND-Flicker,
        // depth-gezaehlt reentrant-sicher, kein Doppel-Redraw). Resume macht es wieder sichtbar.
        CodingOverlayCanvas.Visibility = Visibility.Hidden;
        CodingOverlayCanvas.Cursor = Cursors.Arrow;
    }

    private void ResumeCodingOverlayInput()
    {
        if (_codingOverlaySuspendDepth <= 0)
            return;

        _codingOverlaySuspendDepth--;
        if (_codingOverlaySuspendDepth > 0)
            return;

        // Canvas-Inhalt wieder einblenden (Gegenstueck zum Ausblenden in SuspendCodingOverlayInput).
        CodingOverlayCanvas.Visibility = Visibility.Visible;

        if (_codingOverlayWasOpenBeforeSuspend)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            RedrawCodingCanvas(includeManualOverlay: _codingVm?.CurrentOverlay != null);
        }

        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayCursor();
        _codingOverlayWasOpenBeforeSuspend = false;
    }

    private void HideCodingOverlayForExternalWindow()
    {
        _codingOverlayWasOpenBeforeExternalHide = CodingOverlayPopup.IsOpen;
        SuspendCodingOverlayInput();
        if (_codingOverlayWasOpenBeforeExternalHide)
            CodingOverlayPopup.IsOpen = false;
    }

    private void RestoreCodingOverlayAfterExternalWindow()
    {
        ResumeCodingOverlayInput();
        if (_codingOverlayWasOpenBeforeExternalHide)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            RedrawCodingCanvas(includeManualOverlay: _codingVm?.CurrentOverlay != null);
        }

        _codingOverlayWasOpenBeforeExternalHide = false;
    }
}
