using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Breite/Hoehe des sichtbaren Videobildes (aus dem Analyse-Frame), 0 = unbekannt.
    private double _codingVideoAspect;

    // Das tatsaechlich sichtbare Video-Rechteck im Overlay-Canvas: VLC zeigt das Video
    // formattreu (Letterbox/Pillarbox). Overlays muessen in DIESES Rechteck gerechnet werden,
    // nicht in die volle Flaeche - sonst werden z.B. 4:3-Befunde in einer 16:9-Flaeche verzerrt.
    private Rect GetCodingContentRect()
        => CodingOverlayViewportMapper.GetContentRect(
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight,
            _codingVideoAspect);

    private NormalizedPoint CodingPixelToNorm(Point pixel)
    {
        CodingOverlayViewportRefreshWorkflow.Execute(
            new CodingOverlayViewportRefreshRequest(
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight),
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
