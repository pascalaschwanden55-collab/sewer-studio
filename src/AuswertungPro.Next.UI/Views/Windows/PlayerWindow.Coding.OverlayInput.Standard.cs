using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void HandleCodingStandardMouseDown(NormalizedPoint norm)
    {
        CodingStandardOverlayDrawWorkflow.MouseDown(
            new CodingStandardOverlayMouseDownRequest(_codingSessionHost.HasViewModel),
            new CodingStandardOverlayMouseDownActions(
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                SetCreateEventEnabled: enabled => CodingOverlayInputControls.SetCreateEventEnabled(
                    BtnCodingCreateEvent,
                    enabled),
                UpdateOverlayInfoEmpty: () => UpdateCodingOverlayInfo(null),
                BeginOverlayDraw: () => _codingSessionHost.BeginOverlayDraw(norm),
                CaptureMouse: () => { CodingOverlayCanvas.CaptureMouse(); },
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                UpdateToolBadge: UpdateToolBadge));
    }

    private bool TryHandleCodingStandardMouseMove(NormalizedPoint norm)
    {
        var result = CodingStandardOverlayDrawWorkflow.MouseMove(
            new CodingStandardOverlayMouseMoveRequest(
                _codingOverlayToolHost.HasOverlayService,
                _codingSessionHost.HasViewModel,
                _codingOverlayToolHost.IsDrawing),
            new CodingStandardOverlayMouseMoveActions(
                UpdateOverlayDraw: () => _codingSessionHost.UpdateOverlayDraw(norm),
                HasCurrentOverlay: () => _codingSessionHost.CurrentOverlay != null,
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                UpdateToolBadge: UpdateToolBadge,
                RenderPreviewOverlay: () => RenderOverlayGeometry(
                    _codingSessionHost.CurrentOverlay!,
                    isPreview: true,
                    labelAnchor: norm)));

        return result.Handled;
    }

    private bool TryHandleCodingStandardMouseUp(NormalizedPoint norm)
    {
        var result = CodingStandardOverlayDrawWorkflow.MouseUp(
            new CodingStandardOverlayMouseUpRequest(
                _codingOverlayToolHost.HasOverlayService,
                _codingSessionHost.HasViewModel,
                _codingOverlayToolHost.IsDrawing,
                _liveDetectionController.MarkToolType != OverlayToolType.None,
                BtnCodingLiveAi.IsChecked == true),
            new CodingStandardOverlayMouseUpActions(
                CompleteOverlayDraw: () => _codingSessionHost.CompleteOverlayDraw(norm),
                ReleaseMouseCapture: CodingOverlayCanvas.ReleaseMouseCapture,
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                UpdateToolBadge: UpdateToolBadge,
                HasCurrentOverlay: () => _codingSessionHost.CurrentOverlay != null,
                RenderFinalOverlay: () => RenderOverlayGeometry(
                    _codingSessionHost.CurrentOverlay!,
                    isPreview: false),
                HandleMarkDrawingComplete: HandleMarkDrawingComplete,
                UpdateOverlayInfoEmpty: () => UpdateCodingOverlayInfo(null),
                UpdateOverlayInfoCurrent: () => UpdateCodingOverlayInfo(_codingSessionHost.CurrentOverlay!),
                SetCreateEventEnabled: enabled => CodingOverlayInputControls.SetCreateEventEnabled(
                    BtnCodingCreateEvent,
                    enabled),
                AnalyzeWithOverlayHint: () => AnalyzeWithOverlayHintAsync(_codingSessionHost.CurrentOverlay!)
                    .SafeFireAndForget("OverlayHint")));

        return result.Handled;
    }
}
