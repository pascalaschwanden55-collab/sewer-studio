namespace AuswertungPro.Next.UI.Player;

public enum CodingCanvasRedrawWorkflowOutcome
{
    NoOverlayRendered,
    ManualOverlayRendered,
    SchemaRendered
}

public sealed record CodingCanvasRedrawWorkflowRequest(
    bool IncludeManualOverlay,
    bool IsSchemaActive,
    bool HasCurrentOverlay);

public sealed record CodingCanvasRedrawWorkflowActions(
    Action UpdateViewport,
    Action<bool> ClearTransientCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action RenderActiveSchema,
    Action RenderManualOverlay,
    Action UpdateToolBadge);

public sealed record CodingCanvasRedrawWorkflowResult(
    CodingCanvasRedrawWorkflowOutcome Outcome);

public static class CodingCanvasRedrawWorkflow
{
    public static CodingCanvasRedrawWorkflowResult Execute(
        CodingCanvasRedrawWorkflowRequest request,
        CodingCanvasRedrawWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.UpdateViewport();
        actions.ClearTransientCanvas(true);
        actions.RenderAiOverlays();
        actions.RenderReferenceDn();

        var outcome = CodingCanvasRedrawWorkflowOutcome.NoOverlayRendered;
        if (request.IsSchemaActive)
        {
            actions.RenderActiveSchema();
            outcome = CodingCanvasRedrawWorkflowOutcome.SchemaRendered;
        }
        else if (request.IncludeManualOverlay && request.HasCurrentOverlay)
        {
            actions.RenderManualOverlay();
            outcome = CodingCanvasRedrawWorkflowOutcome.ManualOverlayRendered;
        }

        actions.UpdateToolBadge();
        return new CodingCanvasRedrawWorkflowResult(outcome);
    }
}
