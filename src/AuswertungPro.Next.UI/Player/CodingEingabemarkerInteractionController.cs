using System.Windows;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingEingabemarkerInteractionController
{
    CodingOverlayInputEingabemarkerState OverlayInputState { get; }
    bool IsDrawing { get; }
    bool IsDrawingWithPreview { get; }

    CodingEingabemarkerToggleWorkflowResult Toggle(bool isChecked);
    CodingEingabemarkerToggleWorkflowResult Cancel();
    void SetAnalyzingPhase();
    CodingEingabemarkerCanvasInputWorkflowResult MouseDown(Point canvasPosition);
    CodingEingabemarkerCanvasInputWorkflowResult MouseMove(Point canvasPosition);
    CodingEingabemarkerCanvasInputWorkflowResult MouseUp(Point canvasPosition);
}

public sealed record CodingEingabemarkerInteractionControllerBindings(
    Action PauseForCodingInteraction,
    Action EnsureMarkOverlayReady,
    Action OpenCodingOverlayPopup,
    Action UpdateCodingOverlayViewport,
    Action EnableDrawingCanvas,
    Action ShowDrawingStatus,
    Action UncheckButton,
    Action HideInputPopup,
    Func<Rectangle?, Rectangle?> ClearPreview,
    Action ResetCanvasCursor,
    Action CaptureMouse,
    Func<Point, Rectangle> CreatePreview,
    Action<Rectangle, Rect> UpdatePreview,
    Action ReleaseMouseCapture,
    Func<Size> ResolveCanvasSize,
    Action DisableDrawingCanvas,
    Action ShowInputPopup,
    Action FocusInput,
    Action ShowInputStatus);

public sealed class CodingEingabemarkerInteractionController : ICodingEingabemarkerInteractionController
{
    private readonly CodingEingabemarkerInteractionControllerBindings _bindings;
    private readonly CodingEingabemarkerStateController _state = new();

    public CodingEingabemarkerInteractionController(
        CodingEingabemarkerInteractionControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.PauseForCodingInteraction);
        ArgumentNullException.ThrowIfNull(bindings.EnsureMarkOverlayReady);
        ArgumentNullException.ThrowIfNull(bindings.OpenCodingOverlayPopup);
        ArgumentNullException.ThrowIfNull(bindings.UpdateCodingOverlayViewport);
        ArgumentNullException.ThrowIfNull(bindings.EnableDrawingCanvas);
        ArgumentNullException.ThrowIfNull(bindings.ShowDrawingStatus);
        ArgumentNullException.ThrowIfNull(bindings.UncheckButton);
        ArgumentNullException.ThrowIfNull(bindings.HideInputPopup);
        ArgumentNullException.ThrowIfNull(bindings.ClearPreview);
        ArgumentNullException.ThrowIfNull(bindings.ResetCanvasCursor);
        ArgumentNullException.ThrowIfNull(bindings.CaptureMouse);
        ArgumentNullException.ThrowIfNull(bindings.CreatePreview);
        ArgumentNullException.ThrowIfNull(bindings.UpdatePreview);
        ArgumentNullException.ThrowIfNull(bindings.ReleaseMouseCapture);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCanvasSize);
        ArgumentNullException.ThrowIfNull(bindings.DisableDrawingCanvas);
        ArgumentNullException.ThrowIfNull(bindings.ShowInputPopup);
        ArgumentNullException.ThrowIfNull(bindings.FocusInput);
        ArgumentNullException.ThrowIfNull(bindings.ShowInputStatus);

        _bindings = bindings;
    }

    public CodingOverlayInputEingabemarkerState OverlayInputState => _state.OverlayInputState;

    public bool IsDrawing => _state.IsDrawing;

    public bool IsDrawingWithPreview => _state.IsDrawing && _state.HasPreview;

    public CodingEingabemarkerToggleWorkflowResult Toggle(bool isChecked)
        => CodingEingabemarkerToggleWorkflow.Execute(
            new CodingEingabemarkerToggleWorkflowRequest(isChecked),
            new CodingEingabemarkerToggleWorkflowActions(
                PauseForCodingInteraction: _bindings.PauseForCodingInteraction,
                SetDrawingPhase: _state.SetDrawingPhase,
                EnsureMarkOverlayReady: _bindings.EnsureMarkOverlayReady,
                OpenCodingOverlayPopup: _bindings.OpenCodingOverlayPopup,
                UpdateCodingOverlayViewport: _bindings.UpdateCodingOverlayViewport,
                EnableDrawingCanvas: _bindings.EnableDrawingCanvas,
                ShowDrawingStatus: _bindings.ShowDrawingStatus,
                SetInactivePhase: _state.SetInactivePhase,
                UncheckButton: _bindings.UncheckButton,
                HideInputPopup: _bindings.HideInputPopup,
                ClearPreview: () => _state.SetPreview(_bindings.ClearPreview(_state.PreviewRect)),
                ResetCanvasCursor: _bindings.ResetCanvasCursor));

    public CodingEingabemarkerToggleWorkflowResult Cancel()
        => Toggle(isChecked: false);

    public void SetAnalyzingPhase()
        => _state.SetAnalyzingPhase();

    public CodingEingabemarkerCanvasInputWorkflowResult MouseDown(Point canvasPosition)
        => CodingEingabemarkerCanvasInputWorkflow.MouseDown(
            new CodingEingabemarkerCanvasMouseDownRequest(
                _state.IsDrawing,
                canvasPosition),
            new CodingEingabemarkerCanvasMouseDownActions(
                StoreDragStart: _state.StoreDragStart,
                CaptureMouse: _bindings.CaptureMouse,
                CreatePreview: point => _state.SetPreview(_bindings.CreatePreview(point))));

    public CodingEingabemarkerCanvasInputWorkflowResult MouseMove(Point canvasPosition)
        => CodingEingabemarkerCanvasInputWorkflow.MouseMove(
            new CodingEingabemarkerCanvasMouseMoveRequest(
                _state.IsDrawing,
                _state.HasPreview,
                _state.DragStart,
                canvasPosition),
            new CodingEingabemarkerCanvasMouseMoveActions(
                UpdatePreview: previewRect => _bindings.UpdatePreview(
                    _state.PreviewRect!,
                    previewRect)));

    public CodingEingabemarkerCanvasInputWorkflowResult MouseUp(Point canvasPosition)
        => CodingEingabemarkerCanvasInputWorkflow.MouseUp(
            new CodingEingabemarkerCanvasMouseUpRequest(
                _state.IsDrawing,
                _state.DragStart,
                canvasPosition,
                _bindings.ResolveCanvasSize()),
            new CodingEingabemarkerCanvasMouseUpActions(
                ReleaseMouseCapture: _bindings.ReleaseMouseCapture,
                CancelMarker: () => Cancel(),
                StoreNormalizedSelection: _state.StoreNormalizedSelection,
                SetInputPhase: _state.SetInputPhase,
                DisableDrawingCanvas: _bindings.DisableDrawingCanvas,
                ShowInputPopup: _bindings.ShowInputPopup,
                FocusInput: _bindings.FocusInput,
                ShowInputStatus: _bindings.ShowInputStatus));
}
