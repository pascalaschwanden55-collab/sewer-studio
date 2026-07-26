namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingSchemaOverlayClearWorkflowOutcome
{
    Cleared,
    ClearedAndRedrawn
}

public sealed record CodingSchemaOverlayClearRequest(bool Redraw);

public sealed record CodingSchemaOverlayClearActions(
    Action CancelSchema,
    Action ClearCurrentOverlay,
    Action<bool> SetCreateEventEnabled,
    Action ClearOverlayInfo,
    Action<bool> RedrawCodingCanvas);

public sealed record CodingSchemaOverlayClearWorkflowResult(
    CodingSchemaOverlayClearWorkflowOutcome Outcome);

public static class CodingSchemaOverlayClearWorkflow
{
    public static CodingSchemaOverlayClearWorkflowResult Execute(
        CodingSchemaOverlayClearRequest request,
        CodingSchemaOverlayClearActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.CancelSchema();
        actions.ClearCurrentOverlay();
        actions.SetCreateEventEnabled(false);
        actions.ClearOverlayInfo();

        if (!request.Redraw)
            return Result(CodingSchemaOverlayClearWorkflowOutcome.Cleared);

        actions.RedrawCodingCanvas(false);
        return Result(CodingSchemaOverlayClearWorkflowOutcome.ClearedAndRedrawn);
    }

    private static CodingSchemaOverlayClearWorkflowResult Result(
        CodingSchemaOverlayClearWorkflowOutcome outcome)
        => new(outcome);
}
