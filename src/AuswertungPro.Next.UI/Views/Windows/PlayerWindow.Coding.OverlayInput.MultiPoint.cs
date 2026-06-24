using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void HandleCodingMultiPointMouseDown(NormalizedPoint norm)
    {
        if (_codingOverlayService == null || !_codingSessionHost.HasViewModel)
            return;

        if (_codingOverlayService.DrawPointCount == 0)
        {
            _codingSessionHost.ClearCurrentOverlay();
            CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, false);
            UpdateCodingOverlayInfo(null);
        }

        bool complete = _codingSessionHost.AddMultiPointOverlayPoint(norm);
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();

        var overlay = _codingSessionHost.CurrentOverlay;
        if (overlay != null)
            RenderOverlayGeometry(overlay, isPreview: !complete);

        if (complete)
        {
            UpdateCodingOverlayInfo(overlay);
            CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, true);
            if (BtnCodingLiveAi.IsChecked == true && overlay != null)
                AnalyzeWithOverlayHintAsync(overlay).SafeFireAndForget("OverlayHint");
        }
    }

    private bool TryHandleCodingMultiPointMouseMove(NormalizedPoint norm)
    {
        if (_codingOverlayService == null || !_codingSessionHost.HasViewModel)
            return false;

        if (!_codingOverlayService.IsMultiPointTool || _codingOverlayService.DrawPointCount <= 0)
            return false;

        _codingSessionHost.UpdateMultiPointOverlayPreview(norm);
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        var overlay = _codingSessionHost.CurrentOverlay;
        if (overlay != null)
            RenderOverlayGeometry(overlay, isPreview: true, labelAnchor: norm);

        return true;
    }
}
