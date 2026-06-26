using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private T RunWithSuspendedCodingOverlayInput<T>(Func<T> callback)
        => CodingOverlayInputInteractionWorkflow.Run(
            new CodingOverlayInputInteractionWorkflowActions(
                SuspendCodingOverlayInput,
                ResumeCodingOverlayInput),
            callback);

    private void RunWithSuspendedCodingOverlayInput(Action callback)
        => CodingOverlayInputInteractionWorkflow.Run(
            new CodingOverlayInputInteractionWorkflowActions(
                SuspendCodingOverlayInput,
                ResumeCodingOverlayInput),
            () =>
            {
                callback();
                return true;
            });

    private Task RunWithSuspendedCodingOverlayInputAsync(Func<Task> callback)
        => CodingOverlayInputInteractionWorkflow.RunAsync(
            new CodingOverlayInputInteractionWorkflowActions(
                SuspendCodingOverlayInput,
                ResumeCodingOverlayInput),
            callback);

    private void SuspendCodingOverlayInput()
    {
        CodingOverlayInputVisibilityWorkflow.Suspend(
            new CodingOverlayInputSuspendRequest(
                SuspendDepth: _codingOverlayInputVisibilityState.SuspendDepth,
                IsPopupOpen: CodingOverlayInputControls.IsPopupOpen(CodingOverlayPopup)),
            new CodingOverlayInputSuspendActions(
                SetSuspendDepth: _codingOverlayInputVisibilityState.SetSuspendDepth,
                EndDrag: _codingSchemaManager.EndDrag,
                CancelDraw: () => _codingOverlayToolHost.CancelDraw(),
                RememberOpenBeforeSuspend: _codingOverlayInputVisibilityState.RememberOpenBeforeSuspend,
                SuspendCanvas: () => CodingOverlayInputControls.SuspendCanvas(CodingOverlayCanvas)));
    }

    private void ResumeCodingOverlayInput()
    {
        CodingOverlayInputVisibilityWorkflow.Resume(
            new CodingOverlayInputResumeRequest(
                SuspendDepth: _codingOverlayInputVisibilityState.SuspendDepth,
                WasOpenBeforeSuspend: _codingOverlayInputVisibilityState.WasOpenBeforeSuspend,
                HasCurrentOverlay: _codingSessionHost.CurrentOverlay != null),
            new CodingOverlayInputResumeActions(
                SetSuspendDepth: _codingOverlayInputVisibilityState.SetSuspendDepth,
                ResumeCanvas: () => CodingOverlayInputControls.ResumeCanvas(CodingOverlayCanvas),
                OpenPopup: () => CodingOverlayInputControls.OpenPopup(CodingOverlayPopup),
                UpdateViewport: UpdateCodingOverlayViewport,
                RedrawCanvas: includeManualOverlay => RedrawCodingCanvas(includeManualOverlay),
                UpdateCursor: UpdateCodingOverlayCursor,
                RememberOpenBeforeSuspend: _codingOverlayInputVisibilityState.RememberOpenBeforeSuspend));
    }

    private void HideCodingOverlayForExternalWindow()
    {
        CodingOverlayInputVisibilityWorkflow.HideForExternalWindow(
            new CodingOverlayInputExternalWindowRequest(
                IsPopupOpen: CodingOverlayInputControls.IsPopupOpen(CodingOverlayPopup)),
            new CodingOverlayInputExternalWindowHideActions(
                RememberOpenBeforeExternalHide: _codingOverlayInputVisibilityState.RememberOpenBeforeExternalHide,
                Suspend: SuspendCodingOverlayInput,
                ClosePopup: () => CodingOverlayInputControls.ClosePopup(CodingOverlayPopup)));
    }

    private void RestoreCodingOverlayAfterExternalWindow()
    {
        CodingOverlayInputVisibilityWorkflow.RestoreAfterExternalWindow(
            new CodingOverlayInputExternalWindowRestoreRequest(
                WasOpenBeforeExternalHide: _codingOverlayInputVisibilityState.WasOpenBeforeExternalHide,
                HasCurrentOverlay: _codingSessionHost.CurrentOverlay != null),
            new CodingOverlayInputExternalWindowRestoreActions(
                Resume: ResumeCodingOverlayInput,
                OpenPopup: () => CodingOverlayInputControls.OpenPopup(CodingOverlayPopup),
                UpdateViewport: UpdateCodingOverlayViewport,
                RedrawCanvas: includeManualOverlay => RedrawCodingCanvas(includeManualOverlay),
                RememberOpenBeforeExternalHide: _codingOverlayInputVisibilityState.RememberOpenBeforeExternalHide));
    }
}
