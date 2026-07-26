using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionManualMarkCompletionWorkflowOutcome
{
    Deactivated,
    KeptActive
}

public sealed record LiveDetectionManualMarkCompletionWorkflowRequest(
    bool Saved,
    bool IsCodingMode,
    OverlayToolType MarkToolType);

public sealed record LiveDetectionManualMarkCompletionWorkflowActions(
    Action ClearSamMasks,
    Action ClearBendMarker,
    Action ClearCurrentOverlay,
    Action RedrawCodingCanvasWithoutManualOverlay,
    Action DeactivateMarkTool,
    Action<OverlayToolType> SetActiveTool,
    Action ApplyCrossCursor);

public sealed record LiveDetectionManualMarkCompletionWorkflowResult(
    LiveDetectionManualMarkCompletionWorkflowOutcome Outcome);

public static class LiveDetectionManualMarkCompletionWorkflow
{
    public static LiveDetectionManualMarkCompletionWorkflowResult Execute(
        LiveDetectionManualMarkCompletionWorkflowRequest request,
        LiveDetectionManualMarkCompletionWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.ClearSamMasks();
        actions.ClearBendMarker();
        actions.ClearCurrentOverlay();
        actions.RedrawCodingCanvasWithoutManualOverlay();

        if (request.Saved && !request.IsCodingMode)
        {
            actions.DeactivateMarkTool();
            return new LiveDetectionManualMarkCompletionWorkflowResult(
                LiveDetectionManualMarkCompletionWorkflowOutcome.Deactivated);
        }

        actions.SetActiveTool(request.MarkToolType);
        actions.ApplyCrossCursor();
        return new LiveDetectionManualMarkCompletionWorkflowResult(
            LiveDetectionManualMarkCompletionWorkflowOutcome.KeptActive);
    }
}
