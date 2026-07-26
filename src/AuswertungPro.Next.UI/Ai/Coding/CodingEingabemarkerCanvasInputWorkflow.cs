using System.Windows;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingEingabemarkerCanvasInputWorkflowOutcome
{
    Ignored,
    DragStarted,
    PreviewUpdated,
    Cancelled,
    Completed
}

public sealed record CodingEingabemarkerCanvasMouseDownRequest(
    bool IsDrawing,
    Point CanvasPosition);

public sealed record CodingEingabemarkerCanvasMouseMoveRequest(
    bool IsDrawing,
    bool HasPreview,
    Point DragStart,
    Point CanvasPosition);

public sealed record CodingEingabemarkerCanvasMouseUpRequest(
    bool IsDrawing,
    Point DragStart,
    Point CanvasPosition,
    Size CanvasSize);

public sealed record CodingEingabemarkerCanvasMouseDownActions(
    Action<Point> StoreDragStart,
    Action CaptureMouse,
    Action<Point> CreatePreview);

public sealed record CodingEingabemarkerCanvasMouseMoveActions(
    Action<Rect> UpdatePreview);

public sealed record CodingEingabemarkerCanvasMouseUpActions(
    Action ReleaseMouseCapture,
    Action CancelMarker,
    Action<Rect> StoreNormalizedSelection,
    Action SetInputPhase,
    Action DisableDrawingCanvas,
    Action ShowInputPopup,
    Action FocusInput,
    Action ShowInputStatus);

public sealed record CodingEingabemarkerCanvasInputWorkflowResult(
    CodingEingabemarkerCanvasInputWorkflowOutcome Outcome);

public static class CodingEingabemarkerCanvasInputWorkflow
{
    public static CodingEingabemarkerCanvasInputWorkflowResult MouseDown(
        CodingEingabemarkerCanvasMouseDownRequest request,
        CodingEingabemarkerCanvasMouseDownActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsDrawing)
            return Result(CodingEingabemarkerCanvasInputWorkflowOutcome.Ignored);

        actions.StoreDragStart(request.CanvasPosition);
        actions.CaptureMouse();
        actions.CreatePreview(request.CanvasPosition);
        return Result(CodingEingabemarkerCanvasInputWorkflowOutcome.DragStarted);
    }

    public static CodingEingabemarkerCanvasInputWorkflowResult MouseMove(
        CodingEingabemarkerCanvasMouseMoveRequest request,
        CodingEingabemarkerCanvasMouseMoveActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsDrawing || !request.HasPreview)
            return Result(CodingEingabemarkerCanvasInputWorkflowOutcome.Ignored);

        actions.UpdatePreview(CodingEingabemarkerGeometryPolicy.BuildPreviewRect(
            request.DragStart,
            request.CanvasPosition));
        return Result(CodingEingabemarkerCanvasInputWorkflowOutcome.PreviewUpdated);
    }

    public static CodingEingabemarkerCanvasInputWorkflowResult MouseUp(
        CodingEingabemarkerCanvasMouseUpRequest request,
        CodingEingabemarkerCanvasMouseUpActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsDrawing)
            return Result(CodingEingabemarkerCanvasInputWorkflowOutcome.Ignored);

        actions.ReleaseMouseCapture();

        var normalizedRect = CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection(
            request.DragStart,
            request.CanvasPosition,
            request.CanvasSize);
        if (normalizedRect is null)
        {
            actions.CancelMarker();
            return Result(CodingEingabemarkerCanvasInputWorkflowOutcome.Cancelled);
        }

        actions.StoreNormalizedSelection(normalizedRect.Value);
        actions.SetInputPhase();
        actions.DisableDrawingCanvas();
        actions.ShowInputPopup();
        actions.FocusInput();
        actions.ShowInputStatus();
        return Result(CodingEingabemarkerCanvasInputWorkflowOutcome.Completed);
    }

    private static CodingEingabemarkerCanvasInputWorkflowResult Result(
        CodingEingabemarkerCanvasInputWorkflowOutcome outcome)
        => new(outcome);
}
