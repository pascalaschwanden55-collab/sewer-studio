namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingOverlayInputEingabemarkerState
{
    Inactive,
    Drawing,
    InputBlocked
}

public enum CodingOverlayInputMouseWorkflowOutcome
{
    EingabemarkerHandled,
    InputBlocked,
    PrerequisitesMissing,
    ActiveToolNone,
    CalibrationHandled,
    SchemaHandled,
    MultiPointHandled,
    StandardHandled,
    NotHandled
}

public sealed record CodingOverlayInputMouseDownRequest(
    CodingOverlayInputEingabemarkerState EingabemarkerState,
    bool HasOverlayService,
    bool HasViewModel,
    bool IsActiveToolNone,
    bool IsMultiPointTool);

public sealed record CodingOverlayInputMouseMoveRequest(
    bool IsEingabemarkerDrawingWithPreview,
    bool HasOverlayService,
    bool HasViewModel);

public sealed record CodingOverlayInputMouseUpRequest(
    bool IsEingabemarkerDrawing,
    bool HasOverlayService,
    bool HasViewModel);

public sealed record CodingOverlayInputMouseDownActions(
    Action HandleEingabemarkerMouseDown,
    Action MarkHandled,
    Func<bool> TryStartCalibration,
    Func<bool> TryHandleSchemaMouseDown,
    Action HandleMultiPointMouseDown,
    Action HandleStandardMouseDown);

public sealed record CodingOverlayInputMouseMoveActions(
    Action HandleEingabemarkerMouseMove,
    Func<bool> TryPreviewCalibration,
    Func<bool> TryHandleSchemaMouseMove,
    Func<bool> TryHandleMultiPointMouseMove,
    Func<bool> TryHandleStandardMouseMove);

public sealed record CodingOverlayInputMouseUpActions(
    Action HandleEingabemarkerMouseUp,
    Action MarkHandled,
    Func<bool> TryFinishCalibration,
    Func<bool> TryHandleSchemaMouseUp,
    Func<bool> TryHandleStandardMouseUp);

public sealed record CodingOverlayInputMouseWorkflowResult(
    CodingOverlayInputMouseWorkflowOutcome Outcome);

public static class CodingOverlayInputMouseWorkflow
{
    public static CodingOverlayInputMouseWorkflowResult MouseDown(
        CodingOverlayInputMouseDownRequest request,
        CodingOverlayInputMouseDownActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.EingabemarkerState == CodingOverlayInputEingabemarkerState.Drawing)
        {
            actions.HandleEingabemarkerMouseDown();
            actions.MarkHandled();
            return Result(CodingOverlayInputMouseWorkflowOutcome.EingabemarkerHandled);
        }

        if (request.EingabemarkerState == CodingOverlayInputEingabemarkerState.InputBlocked)
        {
            actions.MarkHandled();
            return Result(CodingOverlayInputMouseWorkflowOutcome.InputBlocked);
        }

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingOverlayInputMouseWorkflowOutcome.PrerequisitesMissing);

        if (actions.TryStartCalibration())
            return Result(CodingOverlayInputMouseWorkflowOutcome.CalibrationHandled);

        if (request.IsActiveToolNone)
            return Result(CodingOverlayInputMouseWorkflowOutcome.ActiveToolNone);

        if (actions.TryHandleSchemaMouseDown())
            return Result(CodingOverlayInputMouseWorkflowOutcome.SchemaHandled);

        if (request.IsMultiPointTool)
        {
            actions.HandleMultiPointMouseDown();
            return Result(CodingOverlayInputMouseWorkflowOutcome.MultiPointHandled);
        }

        actions.HandleStandardMouseDown();
        return Result(CodingOverlayInputMouseWorkflowOutcome.StandardHandled);
    }

    public static CodingOverlayInputMouseWorkflowResult MouseMove(
        CodingOverlayInputMouseMoveRequest request,
        CodingOverlayInputMouseMoveActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsEingabemarkerDrawingWithPreview)
        {
            actions.HandleEingabemarkerMouseMove();
            return Result(CodingOverlayInputMouseWorkflowOutcome.EingabemarkerHandled);
        }

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingOverlayInputMouseWorkflowOutcome.PrerequisitesMissing);

        if (actions.TryPreviewCalibration())
            return Result(CodingOverlayInputMouseWorkflowOutcome.CalibrationHandled);

        if (actions.TryHandleSchemaMouseMove())
            return Result(CodingOverlayInputMouseWorkflowOutcome.SchemaHandled);

        if (actions.TryHandleMultiPointMouseMove())
            return Result(CodingOverlayInputMouseWorkflowOutcome.MultiPointHandled);

        if (actions.TryHandleStandardMouseMove())
            return Result(CodingOverlayInputMouseWorkflowOutcome.StandardHandled);

        return Result(CodingOverlayInputMouseWorkflowOutcome.NotHandled);
    }

    public static CodingOverlayInputMouseWorkflowResult MouseUp(
        CodingOverlayInputMouseUpRequest request,
        CodingOverlayInputMouseUpActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsEingabemarkerDrawing)
        {
            actions.HandleEingabemarkerMouseUp();
            actions.MarkHandled();
            return Result(CodingOverlayInputMouseWorkflowOutcome.EingabemarkerHandled);
        }

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingOverlayInputMouseWorkflowOutcome.PrerequisitesMissing);

        if (actions.TryFinishCalibration())
            return Result(CodingOverlayInputMouseWorkflowOutcome.CalibrationHandled);

        if (actions.TryHandleSchemaMouseUp())
            return Result(CodingOverlayInputMouseWorkflowOutcome.SchemaHandled);

        if (actions.TryHandleStandardMouseUp())
            return Result(CodingOverlayInputMouseWorkflowOutcome.StandardHandled);

        return Result(CodingOverlayInputMouseWorkflowOutcome.NotHandled);
    }

    private static CodingOverlayInputMouseWorkflowResult Result(CodingOverlayInputMouseWorkflowOutcome outcome)
        => new(outcome);
}
