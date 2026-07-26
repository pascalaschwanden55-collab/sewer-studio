using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Das tatsaechlich sichtbare Video-Rechteck im Overlay-Canvas: VLC zeigt das Video
    // formattreu (Letterbox/Pillarbox). Overlays muessen in DIESES Rechteck gerechnet werden,
    // nicht in die volle Flaeche - sonst werden z.B. 4:3-Befunde in einer 16:9-Flaeche verzerrt.
    private Rect GetCodingContentRect()
    {
        var canvasSize = CodingOverlayInputControls.GetCanvasActualSize(CodingOverlayCanvas);

        return CodingOverlayViewportMapper.GetContentRect(
            canvasSize.Width,
            canvasSize.Height,
            _codingOverlayRenderState.VideoAspect);
    }

    private NormalizedPoint CodingPixelToNorm(Point pixel)
    {
        var canvasSize = CodingOverlayInputControls.GetCanvasActualSize(CodingOverlayCanvas);

        CodingOverlayViewportRefreshWorkflow.Execute(
            new CodingOverlayViewportRefreshRequest(
                canvasSize.Width,
                canvasSize.Height),
            new CodingOverlayViewportRefreshActions(
                UpdateViewport: UpdateCodingOverlayViewport));

        var r = GetCodingContentRect();
        return CodingOverlayViewportMapper.PixelToNorm(pixel, r);
    }

    private Point CodingNormToPixel(NormalizedPoint norm)
    {
        var r = GetCodingContentRect();
        return CodingOverlayViewportMapper.NormToPixel(norm, r);
    }

    private void ClearTransientCodingCanvas(bool clearManualOverlay)
    {
        _codingOverlayRenderController.ClearTransient(clearManualOverlay);
    }

    private void RedrawCodingCanvas(bool includeManualOverlay)
    {
        CodingCanvasRedrawWorkflow.Execute(
            new CodingCanvasRedrawWorkflowRequest(
                includeManualOverlay,
                _codingSchemaManager.IsActive,
                _codingSessionHost.CurrentOverlay != null),
            new CodingCanvasRedrawWorkflowActions(
                UpdateViewport: UpdateCodingOverlayViewport,
                ClearTransientCanvas: ClearTransientCodingCanvas,
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                RenderActiveSchema: RenderActiveCodingSchema,
                RenderManualOverlay: () => RenderOverlayGeometry(_codingSessionHost.CurrentOverlay!, isPreview: false),
                UpdateToolBadge: UpdateToolBadge));
    }
}
