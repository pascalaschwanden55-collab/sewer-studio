using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionManualMarkDeactivationWorkflowOutcome
{
    CodingOverlayKept,
    CodingOverlayDeactivated
}

public sealed record LiveDetectionManualMarkDeactivationWorkflowRequest(
    bool IsCodingMode,
    bool IsLiveDetectionRunning);

public sealed record LiveDetectionManualMarkDeactivationWorkflowActions(
    Action<OverlayToolType> SetMarkToolType,
    Action<bool> SetManualMarkMode,
    Action ResetToolLabel,
    Action<bool> DeactivateDetectionSide,
    Action CancelSchema,
    Action CancelDraw,
    Action<OverlayToolType> SetActiveTool,
    Action DeactivateCodingOverlay);

public sealed record LiveDetectionManualMarkDeactivationWorkflowResult(
    LiveDetectionManualMarkDeactivationWorkflowOutcome Outcome);

public static class LiveDetectionManualMarkDeactivationWorkflow
{
    public static LiveDetectionManualMarkDeactivationWorkflowResult Execute(
        LiveDetectionManualMarkDeactivationWorkflowRequest request,
        LiveDetectionManualMarkDeactivationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetMarkToolType(OverlayToolType.None);
        actions.SetManualMarkMode(false);
        actions.ResetToolLabel();
        actions.DeactivateDetectionSide(request.IsLiveDetectionRunning);

        if (request.IsCodingMode)
            return new LiveDetectionManualMarkDeactivationWorkflowResult(
                LiveDetectionManualMarkDeactivationWorkflowOutcome.CodingOverlayKept);

        actions.CancelSchema();
        actions.CancelDraw();
        actions.SetActiveTool(OverlayToolType.None);
        actions.DeactivateCodingOverlay();
        return new LiveDetectionManualMarkDeactivationWorkflowResult(
            LiveDetectionManualMarkDeactivationWorkflowOutcome.CodingOverlayDeactivated);
    }
}
