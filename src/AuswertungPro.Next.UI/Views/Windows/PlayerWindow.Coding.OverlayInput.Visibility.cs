using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void SuspendCodingOverlayInput()
    {
        CodingOverlayInputVisibilityWorkflow.Suspend(
            new CodingOverlayInputSuspendRequest(
                SuspendDepth: _codingOverlaySuspendDepth,
                IsPopupOpen: CodingOverlayPopup.IsOpen),
            new CodingOverlayInputSuspendActions(
                SetSuspendDepth: depth => _codingOverlaySuspendDepth = depth,
                EndDrag: _codingSchemaManager.EndDrag,
                CancelDraw: () => _codingOverlayToolHost.CancelDraw(),
                RememberOpenBeforeSuspend: isOpen => _codingOverlayWasOpenBeforeSuspend = isOpen,
                SuspendCanvas: () => CodingOverlayInputControls.SuspendCanvas(CodingOverlayCanvas)));
    }

    private void ResumeCodingOverlayInput()
    {
        CodingOverlayInputVisibilityWorkflow.Resume(
            new CodingOverlayInputResumeRequest(
                SuspendDepth: _codingOverlaySuspendDepth,
                WasOpenBeforeSuspend: _codingOverlayWasOpenBeforeSuspend,
                HasCurrentOverlay: _codingSessionHost.CurrentOverlay != null),
            new CodingOverlayInputResumeActions(
                SetSuspendDepth: depth => _codingOverlaySuspendDepth = depth,
                ResumeCanvas: () => CodingOverlayInputControls.ResumeCanvas(CodingOverlayCanvas),
                OpenPopup: () => CodingOverlayPopup.IsOpen = true,
                UpdateViewport: UpdateCodingOverlayViewport,
                RedrawCanvas: includeManualOverlay => RedrawCodingCanvas(includeManualOverlay),
                UpdateCursor: UpdateCodingOverlayCursor,
                RememberOpenBeforeSuspend: isOpen => _codingOverlayWasOpenBeforeSuspend = isOpen));
    }

    private void HideCodingOverlayForExternalWindow()
    {
        CodingOverlayInputVisibilityWorkflow.HideForExternalWindow(
            new CodingOverlayInputExternalWindowRequest(
                IsPopupOpen: CodingOverlayPopup.IsOpen),
            new CodingOverlayInputExternalWindowHideActions(
                RememberOpenBeforeExternalHide: isOpen => _codingOverlayWasOpenBeforeExternalHide = isOpen,
                Suspend: SuspendCodingOverlayInput,
                ClosePopup: () => CodingOverlayPopup.IsOpen = false));
    }

    private void RestoreCodingOverlayAfterExternalWindow()
    {
        CodingOverlayInputVisibilityWorkflow.RestoreAfterExternalWindow(
            new CodingOverlayInputExternalWindowRestoreRequest(
                WasOpenBeforeExternalHide: _codingOverlayWasOpenBeforeExternalHide,
                HasCurrentOverlay: _codingSessionHost.CurrentOverlay != null),
            new CodingOverlayInputExternalWindowRestoreActions(
                Resume: ResumeCodingOverlayInput,
                OpenPopup: () => CodingOverlayPopup.IsOpen = true,
                UpdateViewport: UpdateCodingOverlayViewport,
                RedrawCanvas: includeManualOverlay => RedrawCodingCanvas(includeManualOverlay),
                RememberOpenBeforeExternalHide: isOpen => _codingOverlayWasOpenBeforeExternalHide = isOpen));
    }
}
