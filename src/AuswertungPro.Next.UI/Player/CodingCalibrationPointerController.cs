using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingCalibrationPointerControllerActions(
    Action CaptureMouse,
    Action ReleaseMouseCapture,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Func<NormalizedPoint, NormalizedPoint, CodingCalibrationPreviewState> RenderPreview,
    Action<CodingCalibrationPreviewState> ApplyPreview,
    Action<NormalizedPoint, NormalizedPoint> ApplyCalibration);

public sealed class CodingCalibrationPointerController
{
    private readonly CodingCalibrationStateController _state;
    private readonly CodingCalibrationPointerControllerActions _actions;

    public CodingCalibrationPointerController(
        CodingCalibrationStateController state,
        CodingCalibrationPointerControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CaptureMouse);
        ArgumentNullException.ThrowIfNull(actions.ReleaseMouseCapture);
        ArgumentNullException.ThrowIfNull(actions.ClearTransientCodingCanvas);
        ArgumentNullException.ThrowIfNull(actions.RenderAiOverlays);
        ArgumentNullException.ThrowIfNull(actions.RenderReferenceDn);
        ArgumentNullException.ThrowIfNull(actions.RenderPreview);
        ArgumentNullException.ThrowIfNull(actions.ApplyPreview);
        ArgumentNullException.ThrowIfNull(actions.ApplyCalibration);

        _state = state;
        _actions = actions;
    }

    public bool Start(NormalizedPoint point)
        => CodingCalibrationPointerWorkflow.Start(
            new CodingCalibrationPointerStartRequest(_state.IsCalibrating),
            new CodingCalibrationPointerStartActions(
                SetCalibrationStart: () => _state.SetStart(point),
                CaptureMouse: _actions.CaptureMouse,
                ClearTransientCodingCanvas: _actions.ClearTransientCodingCanvas,
                RenderAiOverlays: _actions.RenderAiOverlays,
                RenderReferenceDn: _actions.RenderReferenceDn))
        .Handled;

    public bool Preview(NormalizedPoint point)
    {
        var start = _state.Start;

        return CodingCalibrationPointerWorkflow.Preview(
            new CodingCalibrationPointerPreviewRequest(
                _state.IsCalibrating,
                start is not null),
            new CodingCalibrationPointerPreviewActions(
                ClearTransientCodingCanvas: _actions.ClearTransientCodingCanvas,
                RenderAiOverlays: _actions.RenderAiOverlays,
                RenderReferenceDn: _actions.RenderReferenceDn,
                RenderPreview: () => _actions.ApplyPreview(
                    _actions.RenderPreview(start!, point))))
            .Handled;
    }

    public bool Finish(NormalizedPoint point)
    {
        var start = _state.Start;

        return CodingCalibrationPointerWorkflow.Finish(
            new CodingCalibrationPointerFinishRequest(
                _state.IsCalibrating,
                start is not null),
            new CodingCalibrationPointerFinishActions(
                ReleaseMouseCapture: _actions.ReleaseMouseCapture,
                ApplyCalibration: () => _actions.ApplyCalibration(start!, point)))
            .Handled;
    }
}
