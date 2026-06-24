using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void SuspendCodingOverlayInput()
    {
        _codingOverlaySuspendDepth++;
        if (_codingOverlaySuspendDepth > 1)
            return;

        _codingSchemaManager.EndDrag();
        _codingOverlayService?.CancelDraw();
        _codingOverlayWasOpenBeforeSuspend = CodingOverlayPopup.IsOpen;
        // Das Popup ist ein eigenes transparentes Top-Level-HWND und liegt grafisch
        // UEBER eigenen Dialogen (Loeschen-Bestaetigung, VsaCodeExplorer). IsHitTestVisible=false
        // nimmt nur die Maus weg, der #01000000-Schleier + Kreise bleiben sichtbar und stoeren.
        // Canvas-Inhalt zusaetzlich ausblenden (NICHT Popup.IsOpen togglen -> kein HWND-Flicker,
        // depth-gezaehlt reentrant-sicher, kein Doppel-Redraw). Resume macht es wieder sichtbar.
        CodingOverlayInputControls.SuspendCanvas(CodingOverlayCanvas);
    }

    private void ResumeCodingOverlayInput()
    {
        if (_codingOverlaySuspendDepth <= 0)
            return;

        _codingOverlaySuspendDepth--;
        if (_codingOverlaySuspendDepth > 0)
            return;

        // Canvas-Inhalt wieder einblenden (Gegenstueck zum Ausblenden in SuspendCodingOverlayInput).
        CodingOverlayInputControls.ResumeCanvas(CodingOverlayCanvas);

        if (_codingOverlayWasOpenBeforeSuspend)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            RedrawCodingCanvas(includeManualOverlay: _codingSessionHost.CurrentOverlay != null);
        }

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
            RedrawCodingCanvas(includeManualOverlay: _codingSessionHost.CurrentOverlay != null);
        }

        _codingOverlayWasOpenBeforeExternalHide = false;
    }
}
