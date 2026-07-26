using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void HandleCodingMultiPointMouseDown(NormalizedPoint norm)
    {
        CodingMultiPointOverlayDrawWorkflow.MouseDown(
            new CodingMultiPointOverlayMouseDownRequest(
                _codingOverlayToolHost.HasOverlayService,
                _codingSessionHost.HasViewModel,
                _codingOverlayToolHost.DrawPointCount,
                PlayerToggleButtonControls.IsChecked(BtnCodingLiveAi)),
            new CodingMultiPointOverlayMouseDownActions(
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                SetCreateEventEnabled: enabled => CodingOverlayInputControls.SetCreateEventEnabled(
                    BtnCodingCreateEvent,
                    enabled),
                UpdateOverlayInfoEmpty: () => UpdateCodingOverlayInfo(null),
                AddMultiPointOverlayPoint: () => _codingSessionHost.AddMultiPointOverlayPoint(norm),
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                UpdateToolBadge: UpdateToolBadge,
                HasCurrentOverlay: () => _codingSessionHost.CurrentOverlay != null,
                RenderPreviewOverlay: () => RenderOverlayGeometry(
                    _codingSessionHost.CurrentOverlay!,
                    isPreview: true),
                RenderFinalOverlay: () => RenderOverlayGeometry(
                    _codingSessionHost.CurrentOverlay!,
                    isPreview: false),
                UpdateOverlayInfoCurrent: () => UpdateCodingOverlayInfo(_codingSessionHost.CurrentOverlay),
                AnalyzeWithOverlayHint: () => AnalyzeWithOverlayHintAsync(_codingSessionHost.CurrentOverlay!)
                    .SafeFireAndForget("OverlayHint")));
    }

    private bool TryHandleCodingMultiPointMouseMove(NormalizedPoint norm)
    {
        return CodingMultiPointOverlayDrawWorkflow.MouseMove(
            new CodingMultiPointOverlayMouseMoveRequest(
                _codingOverlayToolHost.HasOverlayService,
                _codingSessionHost.HasViewModel,
                _codingOverlayToolHost.IsMultiPointTool,
                _codingOverlayToolHost.DrawPointCount),
            new CodingMultiPointOverlayMouseMoveActions(
                UpdateMultiPointOverlayPreview: () => _codingSessionHost.UpdateMultiPointOverlayPreview(norm),
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                UpdateToolBadge: UpdateToolBadge,
                HasCurrentOverlay: () => _codingSessionHost.CurrentOverlay != null,
                RenderPreviewOverlay: () => RenderOverlayGeometry(
                    _codingSessionHost.CurrentOverlay!,
                    isPreview: true,
                    labelAnchor: norm)))
            .Handled;
    }
}
