namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingEingabemarkerToggleWorkflowOutcome
{
    Activated,
    Cancelled
}

public sealed record CodingEingabemarkerToggleWorkflowRequest(
    bool IsChecked);

public sealed record CodingEingabemarkerToggleWorkflowActions(
    Action PauseForCodingInteraction,
    Action SetDrawingPhase,
    Action EnsureMarkOverlayReady,
    Action OpenCodingOverlayPopup,
    Action UpdateCodingOverlayViewport,
    Action EnableDrawingCanvas,
    Action ShowDrawingStatus,
    Action SetInactivePhase,
    Action UncheckButton,
    Action HideInputPopup,
    Action ClearPreview,
    Action ResetCanvasCursor);

public sealed record CodingEingabemarkerToggleWorkflowResult(
    CodingEingabemarkerToggleWorkflowOutcome Outcome);

public static class CodingEingabemarkerToggleWorkflow
{
    public static CodingEingabemarkerToggleWorkflowResult Execute(
        CodingEingabemarkerToggleWorkflowRequest request,
        CodingEingabemarkerToggleWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsChecked)
        {
            actions.SetInactivePhase();
            actions.UncheckButton();
            actions.HideInputPopup();
            actions.ClearPreview();
            actions.ResetCanvasCursor();
            return Result(CodingEingabemarkerToggleWorkflowOutcome.Cancelled);
        }

        actions.PauseForCodingInteraction();
        actions.SetDrawingPhase();
        actions.EnsureMarkOverlayReady();
        actions.OpenCodingOverlayPopup();
        actions.UpdateCodingOverlayViewport();
        actions.EnableDrawingCanvas();
        actions.ShowDrawingStatus();
        return Result(CodingEingabemarkerToggleWorkflowOutcome.Activated);
    }

    private static CodingEingabemarkerToggleWorkflowResult Result(
        CodingEingabemarkerToggleWorkflowOutcome outcome)
        => new(outcome);
}
