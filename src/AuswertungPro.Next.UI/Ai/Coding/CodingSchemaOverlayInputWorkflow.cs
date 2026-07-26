namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingSchemaOverlayInputWorkflowOutcome
{
    NotSelected,
    MissingSchema,
    Activated,
    DragStarted,
    NoDrag,
    DragUpdated,
    DragCompleted
}

public sealed record CodingSchemaOverlayMouseDownRequest(
    bool IsSchemaToolSelected,
    bool IsSchemaActive);

public sealed record CodingSchemaOverlayMouseMoveRequest(
    bool IsSchemaToolSelected,
    bool IsSchemaActive,
    bool IsDragging);

public sealed record CodingSchemaOverlayMouseUpRequest(
    bool IsSchemaToolSelected,
    bool IsDragging);

public sealed record CodingSchemaOverlayMouseDownActions(
    Func<bool> CreateAndActivateSchema,
    Action PlaceSchema,
    Func<string> ResolveHandleId,
    Action<string> BeginDrag,
    Action UpdateDrag,
    Action CaptureMouse,
    Action UpdateOverlay);

public sealed record CodingSchemaOverlayMouseMoveActions(
    Action UpdateDrag,
    Action UpdateOverlay);

public sealed record CodingSchemaOverlayMouseUpActions(
    Action UpdateDrag,
    Action EndDrag,
    Action ReleaseMouseCapture,
    Action UpdateOverlay);

public sealed record CodingSchemaOverlayInputWorkflowResult(
    CodingSchemaOverlayInputWorkflowOutcome Outcome)
{
    public bool Handled => Outcome != CodingSchemaOverlayInputWorkflowOutcome.NotSelected;
}

public static class CodingSchemaOverlayInputWorkflow
{
    public static CodingSchemaOverlayInputWorkflowResult MouseDown(
        CodingSchemaOverlayMouseDownRequest request,
        CodingSchemaOverlayMouseDownActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsSchemaToolSelected)
            return Result(CodingSchemaOverlayInputWorkflowOutcome.NotSelected);

        if (!request.IsSchemaActive)
        {
            if (!actions.CreateAndActivateSchema())
                return Result(CodingSchemaOverlayInputWorkflowOutcome.MissingSchema);

            actions.PlaceSchema();
            actions.UpdateOverlay();
            return Result(CodingSchemaOverlayInputWorkflowOutcome.Activated);
        }

        var handleId = actions.ResolveHandleId();
        actions.BeginDrag(handleId);
        actions.UpdateDrag();
        actions.CaptureMouse();
        actions.UpdateOverlay();
        return Result(CodingSchemaOverlayInputWorkflowOutcome.DragStarted);
    }

    public static CodingSchemaOverlayInputWorkflowResult MouseMove(
        CodingSchemaOverlayMouseMoveRequest request,
        CodingSchemaOverlayMouseMoveActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsSchemaToolSelected || !request.IsSchemaActive)
            return Result(CodingSchemaOverlayInputWorkflowOutcome.NotSelected);

        if (!request.IsDragging)
            return Result(CodingSchemaOverlayInputWorkflowOutcome.NoDrag);

        actions.UpdateDrag();
        actions.UpdateOverlay();
        return Result(CodingSchemaOverlayInputWorkflowOutcome.DragUpdated);
    }

    public static CodingSchemaOverlayInputWorkflowResult MouseUp(
        CodingSchemaOverlayMouseUpRequest request,
        CodingSchemaOverlayMouseUpActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsSchemaToolSelected || !request.IsDragging)
            return Result(CodingSchemaOverlayInputWorkflowOutcome.NotSelected);

        actions.UpdateDrag();
        actions.EndDrag();
        actions.ReleaseMouseCapture();
        actions.UpdateOverlay();
        return Result(CodingSchemaOverlayInputWorkflowOutcome.DragCompleted);
    }

    private static CodingSchemaOverlayInputWorkflowResult Result(
        CodingSchemaOverlayInputWorkflowOutcome outcome)
        => new(outcome);
}
