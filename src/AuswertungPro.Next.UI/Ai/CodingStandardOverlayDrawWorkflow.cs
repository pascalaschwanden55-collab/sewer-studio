namespace AuswertungPro.Next.UI.Ai;

public enum CodingStandardOverlayDrawWorkflowOutcome
{
    NoViewModel,
    PrerequisitesMissing,
    NotDrawing,
    Started,
    UpdatedWithoutOverlay,
    PreviewRendered,
    CompletedWithoutOverlay,
    MarkCompleted,
    Completed
}

public sealed record CodingStandardOverlayMouseDownRequest(
    bool HasViewModel);

public sealed record CodingStandardOverlayMouseMoveRequest(
    bool HasOverlayService,
    bool HasViewModel,
    bool IsDrawing);

public sealed record CodingStandardOverlayMouseUpRequest(
    bool HasOverlayService,
    bool HasViewModel,
    bool IsDrawing,
    bool IsMarkToolActive,
    bool IsLiveAiChecked);

public sealed record CodingStandardOverlayMouseDownActions(
    Action ClearCurrentOverlay,
    Action<bool> SetCreateEventEnabled,
    Action UpdateOverlayInfoEmpty,
    Action BeginOverlayDraw,
    Action CaptureMouse,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action UpdateToolBadge);

public sealed record CodingStandardOverlayMouseMoveActions(
    Action UpdateOverlayDraw,
    Func<bool> HasCurrentOverlay,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action UpdateToolBadge,
    Action RenderPreviewOverlay);

public sealed record CodingStandardOverlayMouseUpActions(
    Action CompleteOverlayDraw,
    Action ReleaseMouseCapture,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action UpdateToolBadge,
    Func<bool> HasCurrentOverlay,
    Action RenderFinalOverlay,
    Action HandleMarkDrawingComplete,
    Action UpdateOverlayInfoEmpty,
    Action UpdateOverlayInfoCurrent,
    Action<bool> SetCreateEventEnabled,
    Action AnalyzeWithOverlayHint);

public sealed record CodingStandardOverlayDrawWorkflowResult(
    CodingStandardOverlayDrawWorkflowOutcome Outcome)
{
    public bool Handled => Outcome is
        CodingStandardOverlayDrawWorkflowOutcome.Started
        or CodingStandardOverlayDrawWorkflowOutcome.UpdatedWithoutOverlay
        or CodingStandardOverlayDrawWorkflowOutcome.PreviewRendered
        or CodingStandardOverlayDrawWorkflowOutcome.CompletedWithoutOverlay
        or CodingStandardOverlayDrawWorkflowOutcome.MarkCompleted
        or CodingStandardOverlayDrawWorkflowOutcome.Completed;
}

public static class CodingStandardOverlayDrawWorkflow
{
    public static CodingStandardOverlayDrawWorkflowResult MouseDown(
        CodingStandardOverlayMouseDownRequest request,
        CodingStandardOverlayMouseDownActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasViewModel)
            return Result(CodingStandardOverlayDrawWorkflowOutcome.NoViewModel);

        actions.ClearCurrentOverlay();
        actions.SetCreateEventEnabled(false);
        actions.UpdateOverlayInfoEmpty();
        actions.BeginOverlayDraw();
        actions.CaptureMouse();
        RedrawTransient(actions.ClearTransientCodingCanvas, actions.RenderAiOverlays, actions.RenderReferenceDn, actions.UpdateToolBadge);
        return Result(CodingStandardOverlayDrawWorkflowOutcome.Started);
    }

    public static CodingStandardOverlayDrawWorkflowResult MouseMove(
        CodingStandardOverlayMouseMoveRequest request,
        CodingStandardOverlayMouseMoveActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingStandardOverlayDrawWorkflowOutcome.PrerequisitesMissing);

        if (!request.IsDrawing)
            return Result(CodingStandardOverlayDrawWorkflowOutcome.NotDrawing);

        actions.UpdateOverlayDraw();
        if (!actions.HasCurrentOverlay())
            return Result(CodingStandardOverlayDrawWorkflowOutcome.UpdatedWithoutOverlay);

        RedrawTransient(actions.ClearTransientCodingCanvas, actions.RenderAiOverlays, actions.RenderReferenceDn, actions.UpdateToolBadge);
        actions.RenderPreviewOverlay();
        return Result(CodingStandardOverlayDrawWorkflowOutcome.PreviewRendered);
    }

    public static CodingStandardOverlayDrawWorkflowResult MouseUp(
        CodingStandardOverlayMouseUpRequest request,
        CodingStandardOverlayMouseUpActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingStandardOverlayDrawWorkflowOutcome.PrerequisitesMissing);

        if (!request.IsDrawing)
            return Result(CodingStandardOverlayDrawWorkflowOutcome.NotDrawing);

        actions.CompleteOverlayDraw();
        actions.ReleaseMouseCapture();
        RedrawTransient(actions.ClearTransientCodingCanvas, actions.RenderAiOverlays, actions.RenderReferenceDn, actions.UpdateToolBadge);

        if (!actions.HasCurrentOverlay())
        {
            actions.UpdateOverlayInfoEmpty();
            actions.SetCreateEventEnabled(false);
            return Result(CodingStandardOverlayDrawWorkflowOutcome.CompletedWithoutOverlay);
        }

        actions.RenderFinalOverlay();

        if (request.IsMarkToolActive)
        {
            actions.HandleMarkDrawingComplete();
            return Result(CodingStandardOverlayDrawWorkflowOutcome.MarkCompleted);
        }

        actions.UpdateOverlayInfoCurrent();
        actions.SetCreateEventEnabled(true);

        if (request.IsLiveAiChecked)
            actions.AnalyzeWithOverlayHint();

        return Result(CodingStandardOverlayDrawWorkflowOutcome.Completed);
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

    private static CodingStandardOverlayDrawWorkflowResult Result(
        CodingStandardOverlayDrawWorkflowOutcome outcome)
        => new(outcome);
}
