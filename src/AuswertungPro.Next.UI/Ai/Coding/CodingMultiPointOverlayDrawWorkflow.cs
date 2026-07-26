namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingMultiPointOverlayDrawWorkflowOutcome
{
    PrerequisitesMissing,
    NotDrawing,
    PointAdded,
    CompletedWithoutOverlay,
    Completed,
    PreviewUpdatedWithoutOverlay,
    PreviewRendered
}

public sealed record CodingMultiPointOverlayMouseDownRequest(
    bool HasOverlayService,
    bool HasViewModel,
    int DrawPointCount,
    bool IsLiveAiChecked);

public sealed record CodingMultiPointOverlayMouseMoveRequest(
    bool HasOverlayService,
    bool HasViewModel,
    bool IsMultiPointTool,
    int DrawPointCount);

public sealed record CodingMultiPointOverlayMouseDownActions(
    Action ClearCurrentOverlay,
    Action<bool> SetCreateEventEnabled,
    Action UpdateOverlayInfoEmpty,
    Func<bool> AddMultiPointOverlayPoint,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action UpdateToolBadge,
    Func<bool> HasCurrentOverlay,
    Action RenderPreviewOverlay,
    Action RenderFinalOverlay,
    Action UpdateOverlayInfoCurrent,
    Action AnalyzeWithOverlayHint);

public sealed record CodingMultiPointOverlayMouseMoveActions(
    Action UpdateMultiPointOverlayPreview,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action UpdateToolBadge,
    Func<bool> HasCurrentOverlay,
    Action RenderPreviewOverlay);

public sealed record CodingMultiPointOverlayDrawWorkflowResult(
    CodingMultiPointOverlayDrawWorkflowOutcome Outcome)
{
    public bool Handled => Outcome is
        CodingMultiPointOverlayDrawWorkflowOutcome.PointAdded
        or CodingMultiPointOverlayDrawWorkflowOutcome.CompletedWithoutOverlay
        or CodingMultiPointOverlayDrawWorkflowOutcome.Completed
        or CodingMultiPointOverlayDrawWorkflowOutcome.PreviewUpdatedWithoutOverlay
        or CodingMultiPointOverlayDrawWorkflowOutcome.PreviewRendered;
}

public static class CodingMultiPointOverlayDrawWorkflow
{
    public static CodingMultiPointOverlayDrawWorkflowResult MouseDown(
        CodingMultiPointOverlayMouseDownRequest request,
        CodingMultiPointOverlayMouseDownActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingMultiPointOverlayDrawWorkflowOutcome.PrerequisitesMissing);

        if (request.DrawPointCount == 0)
        {
            actions.ClearCurrentOverlay();
            actions.SetCreateEventEnabled(false);
            actions.UpdateOverlayInfoEmpty();
        }

        var complete = actions.AddMultiPointOverlayPoint();
        RedrawTransient(
            actions.ClearTransientCodingCanvas,
            actions.RenderAiOverlays,
            actions.RenderReferenceDn,
            actions.UpdateToolBadge);

        var hasOverlay = actions.HasCurrentOverlay();
        if (hasOverlay)
        {
            if (complete)
                actions.RenderFinalOverlay();
            else
                actions.RenderPreviewOverlay();
        }

        if (!complete)
            return Result(CodingMultiPointOverlayDrawWorkflowOutcome.PointAdded);

        actions.UpdateOverlayInfoCurrent();
        actions.SetCreateEventEnabled(true);

        if (!hasOverlay)
            return Result(CodingMultiPointOverlayDrawWorkflowOutcome.CompletedWithoutOverlay);

        if (request.IsLiveAiChecked)
            actions.AnalyzeWithOverlayHint();

        return Result(CodingMultiPointOverlayDrawWorkflowOutcome.Completed);
    }

    public static CodingMultiPointOverlayDrawWorkflowResult MouseMove(
        CodingMultiPointOverlayMouseMoveRequest request,
        CodingMultiPointOverlayMouseMoveActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingMultiPointOverlayDrawWorkflowOutcome.PrerequisitesMissing);

        if (!request.IsMultiPointTool || request.DrawPointCount <= 0)
            return Result(CodingMultiPointOverlayDrawWorkflowOutcome.NotDrawing);

        actions.UpdateMultiPointOverlayPreview();
        RedrawTransient(
            actions.ClearTransientCodingCanvas,
            actions.RenderAiOverlays,
            actions.RenderReferenceDn,
            actions.UpdateToolBadge);

        if (!actions.HasCurrentOverlay())
            return Result(CodingMultiPointOverlayDrawWorkflowOutcome.PreviewUpdatedWithoutOverlay);

        actions.RenderPreviewOverlay();
        return Result(CodingMultiPointOverlayDrawWorkflowOutcome.PreviewRendered);
    }

    private static void RedrawTransient(
        Action clearTransientCodingCanvas,
        Action renderAiOverlays,
        Action renderReferenceDn,
        Action updateToolBadge)
    {
        clearTransientCodingCanvas();
        renderAiOverlays();
        renderReferenceDn();
        updateToolBadge();
    }

    private static CodingMultiPointOverlayDrawWorkflowResult Result(
        CodingMultiPointOverlayDrawWorkflowOutcome outcome)
        => new(outcome);
}
