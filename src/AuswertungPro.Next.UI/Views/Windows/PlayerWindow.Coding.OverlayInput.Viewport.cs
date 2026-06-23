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
        if (CodingOverlayCanvas.ActualWidth <= 0 || CodingOverlayCanvas.ActualHeight <= 0)
            UpdateCodingOverlayViewport();
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
        CodingOverlayCanvasCleaner.ClearTransient(CodingOverlayCanvas, clearManualOverlay);
    }

    private void RedrawCodingCanvas(bool includeManualOverlay)
    {
        UpdateCodingOverlayViewport();
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();

        if (_codingSchemaManager.IsActive)
            RenderActiveCodingSchema();
        else if (includeManualOverlay && _codingVm?.CurrentOverlay != null)
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: false);

        UpdateToolBadge();
    }
}
