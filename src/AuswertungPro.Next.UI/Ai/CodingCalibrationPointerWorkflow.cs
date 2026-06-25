namespace AuswertungPro.Next.UI.Ai;

public enum CodingCalibrationPointerWorkflowOutcome
{
    NotCalibrating,
    MissingStart,
    Started,
    Previewed,
    Finished
}

public sealed record CodingCalibrationPointerStartRequest(bool IsCalibrating);

public sealed record CodingCalibrationPointerPreviewRequest(
    bool IsCalibrating,
    bool HasCalibrationStart);

public sealed record CodingCalibrationPointerFinishRequest(
    bool IsCalibrating,
    bool HasCalibrationStart);

public sealed record CodingCalibrationPointerStartActions(
    Action SetCalibrationStart,
    Action CaptureMouse,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn);

public sealed record CodingCalibrationPointerPreviewActions(
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action RenderPreview);

public sealed record CodingCalibrationPointerFinishActions(
    Action ReleaseMouseCapture,
    Action ApplyCalibration);

public sealed record CodingCalibrationPointerWorkflowResult(
    CodingCalibrationPointerWorkflowOutcome Outcome)
{
    public bool Handled
        => Outcome is CodingCalibrationPointerWorkflowOutcome.Started
            or CodingCalibrationPointerWorkflowOutcome.Previewed
            or CodingCalibrationPointerWorkflowOutcome.Finished;
}

public static class CodingCalibrationPointerWorkflow
{
    public static CodingCalibrationPointerWorkflowResult Start(
        CodingCalibrationPointerStartRequest request,
        CodingCalibrationPointerStartActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsCalibrating)
            return Result(CodingCalibrationPointerWorkflowOutcome.NotCalibrating);

        actions.SetCalibrationStart();
        actions.CaptureMouse();
        actions.ClearTransientCodingCanvas();
        actions.RenderAiOverlays();
        actions.RenderReferenceDn();
        return Result(CodingCalibrationPointerWorkflowOutcome.Started);
    }

    public static CodingCalibrationPointerWorkflowResult Preview(
        CodingCalibrationPointerPreviewRequest request,
        CodingCalibrationPointerPreviewActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var skipped = GetInactiveOutcome(request.IsCalibrating, request.HasCalibrationStart);
        if (skipped.HasValue)
            return Result(skipped.Value);

        actions.ClearTransientCodingCanvas();
        actions.RenderAiOverlays();
        actions.RenderReferenceDn();
        actions.RenderPreview();
        return Result(CodingCalibrationPointerWorkflowOutcome.Previewed);
    }

    public static CodingCalibrationPointerWorkflowResult Finish(
        CodingCalibrationPointerFinishRequest request,
        CodingCalibrationPointerFinishActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var skipped = GetInactiveOutcome(request.IsCalibrating, request.HasCalibrationStart);
        if (skipped.HasValue)
            return Result(skipped.Value);

        actions.ReleaseMouseCapture();
        actions.ApplyCalibration();
        return Result(CodingCalibrationPointerWorkflowOutcome.Finished);
    }

    private static CodingCalibrationPointerWorkflowOutcome? GetInactiveOutcome(
        bool isCalibrating,
        bool hasCalibrationStart)
    {
        if (!isCalibrating)
            return CodingCalibrationPointerWorkflowOutcome.NotCalibrating;

        if (!hasCalibrationStart)
            return CodingCalibrationPointerWorkflowOutcome.MissingStart;

        return null;
    }

    private static CodingCalibrationPointerWorkflowResult Result(
        CodingCalibrationPointerWorkflowOutcome outcome)
        => new(outcome);
}
