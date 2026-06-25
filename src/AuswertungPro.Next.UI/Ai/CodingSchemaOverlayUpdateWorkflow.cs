namespace AuswertungPro.Next.UI.Ai;

public enum CodingSchemaOverlayUpdateWorkflowOutcome
{
    NoViewModel,
    UpdatedWithoutOverlay,
    UpdatedWithOverlay
}

public sealed record CodingSchemaOverlayUpdateRequest(
    bool HasViewModel,
    bool EnableCreateEvent);

public sealed record CodingSchemaOverlayUpdateActions(
    Func<bool> BuildSetAndReportOverlay,
    Action UpdateOverlayInfo,
    Action<bool> SetCreateEventEnabled,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action UpdateToolBadge,
    Action RenderActiveCodingSchema);

public sealed record CodingSchemaOverlayUpdateWorkflowResult(
    CodingSchemaOverlayUpdateWorkflowOutcome Outcome);

public static class CodingSchemaOverlayUpdateWorkflow
{
    public static CodingSchemaOverlayUpdateWorkflowResult Execute(
        CodingSchemaOverlayUpdateRequest request,
        CodingSchemaOverlayUpdateActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasViewModel)
            return Result(CodingSchemaOverlayUpdateWorkflowOutcome.NoViewModel);

        var hasOverlay = actions.BuildSetAndReportOverlay();
        actions.UpdateOverlayInfo();
        actions.SetCreateEventEnabled(request.EnableCreateEvent && hasOverlay);

        actions.ClearTransientCodingCanvas();
        actions.RenderAiOverlays();
        actions.RenderReferenceDn();
        actions.UpdateToolBadge();
        actions.RenderActiveCodingSchema();

        return Result(
            hasOverlay
                ? CodingSchemaOverlayUpdateWorkflowOutcome.UpdatedWithOverlay
                : CodingSchemaOverlayUpdateWorkflowOutcome.UpdatedWithoutOverlay);
    }

    private static CodingSchemaOverlayUpdateWorkflowResult Result(
        CodingSchemaOverlayUpdateWorkflowOutcome outcome)
        => new(outcome);
}
