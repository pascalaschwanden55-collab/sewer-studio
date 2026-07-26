namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingOverlayInputVisibilityWorkflowOutcome
{
    NotSuspended,
    NestedSuspend,
    Suspended,
    NestedResume,
    Resumed,
    HiddenForExternalWindow,
    RestoredAfterExternalWindow
}

public sealed record CodingOverlayInputSuspendRequest(
    int SuspendDepth,
    bool IsPopupOpen);

public sealed record CodingOverlayInputSuspendActions(
    Action<int> SetSuspendDepth,
    Action EndDrag,
    Action CancelDraw,
    Action<bool> RememberOpenBeforeSuspend,
    Action SuspendCanvas);

public sealed record CodingOverlayInputResumeRequest(
    int SuspendDepth,
    bool WasOpenBeforeSuspend,
    bool HasCurrentOverlay);

public sealed record CodingOverlayInputResumeActions(
    Action<int> SetSuspendDepth,
    Action ResumeCanvas,
    Action OpenPopup,
    Action UpdateViewport,
    Action<bool> RedrawCanvas,
    Action UpdateCursor,
    Action<bool> RememberOpenBeforeSuspend);

public sealed record CodingOverlayInputExternalWindowRequest(
    bool IsPopupOpen);

public sealed record CodingOverlayInputExternalWindowHideActions(
    Action<bool> RememberOpenBeforeExternalHide,
    Action Suspend,
    Action ClosePopup);

public sealed record CodingOverlayInputExternalWindowRestoreRequest(
    bool WasOpenBeforeExternalHide,
    bool HasCurrentOverlay);

public sealed record CodingOverlayInputExternalWindowRestoreActions(
    Action Resume,
    Action OpenPopup,
    Action UpdateViewport,
    Action<bool> RedrawCanvas,
    Action<bool> RememberOpenBeforeExternalHide);

public sealed record CodingOverlayInputVisibilityWorkflowResult(
    CodingOverlayInputVisibilityWorkflowOutcome Outcome);

public static class CodingOverlayInputVisibilityWorkflow
{
    public static CodingOverlayInputVisibilityWorkflowResult Suspend(
        CodingOverlayInputSuspendRequest request,
        CodingOverlayInputSuspendActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var nextDepth = request.SuspendDepth + 1;
        actions.SetSuspendDepth(nextDepth);

        if (nextDepth > 1)
            return new CodingOverlayInputVisibilityWorkflowResult(
                CodingOverlayInputVisibilityWorkflowOutcome.NestedSuspend);

        actions.EndDrag();
        actions.CancelDraw();
        actions.RememberOpenBeforeSuspend(request.IsPopupOpen);
        actions.SuspendCanvas();

        return new CodingOverlayInputVisibilityWorkflowResult(
            CodingOverlayInputVisibilityWorkflowOutcome.Suspended);
    }

    public static CodingOverlayInputVisibilityWorkflowResult Resume(
        CodingOverlayInputResumeRequest request,
        CodingOverlayInputResumeActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SuspendDepth <= 0)
            return new CodingOverlayInputVisibilityWorkflowResult(
                CodingOverlayInputVisibilityWorkflowOutcome.NotSuspended);

        var nextDepth = request.SuspendDepth - 1;
        actions.SetSuspendDepth(nextDepth);

        if (nextDepth > 0)
            return new CodingOverlayInputVisibilityWorkflowResult(
                CodingOverlayInputVisibilityWorkflowOutcome.NestedResume);

        actions.ResumeCanvas();

        if (request.WasOpenBeforeSuspend)
        {
            actions.OpenPopup();
            actions.UpdateViewport();
            actions.RedrawCanvas(request.HasCurrentOverlay);
        }

        actions.UpdateCursor();
        actions.RememberOpenBeforeSuspend(false);

        return new CodingOverlayInputVisibilityWorkflowResult(
            CodingOverlayInputVisibilityWorkflowOutcome.Resumed);
    }

    public static CodingOverlayInputVisibilityWorkflowResult HideForExternalWindow(
        CodingOverlayInputExternalWindowRequest request,
        CodingOverlayInputExternalWindowHideActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.RememberOpenBeforeExternalHide(request.IsPopupOpen);
        actions.Suspend();

        if (request.IsPopupOpen)
            actions.ClosePopup();

        return new CodingOverlayInputVisibilityWorkflowResult(
            CodingOverlayInputVisibilityWorkflowOutcome.HiddenForExternalWindow);
    }

    public static CodingOverlayInputVisibilityWorkflowResult RestoreAfterExternalWindow(
        CodingOverlayInputExternalWindowRestoreRequest request,
        CodingOverlayInputExternalWindowRestoreActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.Resume();

        if (request.WasOpenBeforeExternalHide)
        {
            actions.OpenPopup();
            actions.UpdateViewport();
            actions.RedrawCanvas(request.HasCurrentOverlay);
        }

        actions.RememberOpenBeforeExternalHide(false);

        return new CodingOverlayInputVisibilityWorkflowResult(
            CodingOverlayInputVisibilityWorkflowOutcome.RestoredAfterExternalWindow);
    }
}
