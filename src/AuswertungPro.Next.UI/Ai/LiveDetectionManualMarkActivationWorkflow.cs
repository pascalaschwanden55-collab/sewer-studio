using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionManualMarkActivationWorkflowOutcome
{
    PointToolActivated,
    DrawingToolActivated
}

public sealed record LiveDetectionManualMarkActivationWorkflowRequest(
    OverlayToolType Tool,
    string Label);

public sealed record LiveDetectionManualMarkActivationWorkflowActions(
    Action<string> BeginActivation,
    Action<OverlayToolType> SetMarkToolType,
    Action<bool> SetPause,
    Action CancelSchema,
    Action ClearSchemaType,
    Action<bool> SetManualMarkMode,
    Action ActivatePointTool,
    Action EnsureOverlayReady,
    Action<OverlayToolType> SetActiveTool,
    Action ClearCurrentOverlay,
    Action OpenCodingOverlay,
    Action UpdateCodingOverlayViewport,
    Action EnableCodingOverlayInput);

public sealed record LiveDetectionManualMarkActivationWorkflowResult(
    LiveDetectionManualMarkActivationWorkflowOutcome Outcome);

public static class LiveDetectionManualMarkActivationWorkflow
{
    public static LiveDetectionManualMarkActivationWorkflowResult Execute(
        LiveDetectionManualMarkActivationWorkflowRequest request,
        LiveDetectionManualMarkActivationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.BeginActivation(request.Label);
        actions.SetMarkToolType(request.Tool);
        PlayerManualMarkPlayback.PauseForManualMarking(actions.SetPause);
        actions.CancelSchema();
        actions.ClearSchemaType();

        if (request.Tool == OverlayToolType.Point)
        {
            actions.SetManualMarkMode(true);
            actions.ActivatePointTool();
            return new LiveDetectionManualMarkActivationWorkflowResult(
                LiveDetectionManualMarkActivationWorkflowOutcome.PointToolActivated);
        }

        actions.SetManualMarkMode(false);
        actions.EnsureOverlayReady();
        actions.SetActiveTool(request.Tool);
        actions.ClearCurrentOverlay();
        actions.OpenCodingOverlay();
        actions.UpdateCodingOverlayViewport();
        actions.EnableCodingOverlayInput();
        return new LiveDetectionManualMarkActivationWorkflowResult(
            LiveDetectionManualMarkActivationWorkflowOutcome.DrawingToolActivated);
    }
}
