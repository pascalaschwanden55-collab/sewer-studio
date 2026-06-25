using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void HandleCodingStandardMouseDown(NormalizedPoint norm)
    {
        if (!_codingSessionHost.HasViewModel)
            return;

        _codingSessionHost.ClearCurrentOverlay();
        CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, false);
        UpdateCodingOverlayInfo(null);

        _codingSessionHost.BeginOverlayDraw(norm);
        CodingOverlayCanvas.CaptureMouse();
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
    }

    private bool TryHandleCodingStandardMouseMove(NormalizedPoint norm)
    {
        if (!_codingOverlayToolHost.HasOverlayService || !_codingSessionHost.HasViewModel)
            return false;

        if (!_codingOverlayToolHost.IsDrawing)
            return false;

        _codingSessionHost.UpdateOverlayDraw(norm);
        var overlay = _codingSessionHost.CurrentOverlay;
        if (overlay == null)
            return true;

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        RenderOverlayGeometry(overlay, isPreview: true, labelAnchor: norm);
        return true;
    }

    private bool TryHandleCodingStandardMouseUp(NormalizedPoint norm)
    {
        if (!_codingOverlayToolHost.HasOverlayService || !_codingSessionHost.HasViewModel)
            return false;

        if (!_codingOverlayToolHost.IsDrawing)
            return false;

        _codingSessionHost.CompleteOverlayDraw(norm);
        CodingOverlayCanvas.ReleaseMouseCapture();

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();

        var overlay = _codingSessionHost.CurrentOverlay;
        if (overlay != null)
        {
            RenderOverlayGeometry(overlay, isPreview: false);

            // Mark-Modus: direkt VsaCodeExplorer oeffnen + Training speichern
            if (_markToolType != OverlayToolType.None)
            {
                HandleMarkDrawingComplete();
                return true;
            }

            UpdateCodingOverlayInfo(overlay);
            CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, true);

            // Wenn Auto-KI aktiv: Overlay-Zeichnung -> KI analysiert markierte Stelle
            if (BtnCodingLiveAi.IsChecked == true)
                AnalyzeWithOverlayHintAsync(overlay).SafeFireAndForget("OverlayHint");
        }
        else
        {
            UpdateCodingOverlayInfo(null);
            CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, false);
        }

        return true;
    }
}
