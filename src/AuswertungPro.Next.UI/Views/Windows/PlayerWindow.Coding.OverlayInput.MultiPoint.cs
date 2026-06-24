using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void HandleCodingMultiPointMouseDown(NormalizedPoint norm)
    {
        if (_codingOverlayService == null || _codingVm == null)
            return;

        if (_codingOverlayService.DrawPointCount == 0)
        {
            _codingVm.CurrentOverlay = null;
            CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, false);
            UpdateCodingOverlayInfo(null);
        }

        bool complete = _codingVm.OnCanvasMultiPointClick(norm);
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();

        if (_codingVm.CurrentOverlay != null)
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: !complete);

        if (complete)
        {
            UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
            CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, true);
            if (BtnCodingLiveAi.IsChecked == true && _codingVm.CurrentOverlay != null)
                AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay).SafeFireAndForget("OverlayHint");
        }
    }

    private bool TryHandleCodingMultiPointMouseMove(NormalizedPoint norm)
    {
        if (_codingOverlayService == null || _codingVm == null)
            return false;

        if (!_codingOverlayService.IsMultiPointTool || _codingOverlayService.DrawPointCount <= 0)
            return false;

        _codingVm.OnCanvasMultiPointMove(norm);
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        if (_codingVm.CurrentOverlay != null)
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: true, labelAnchor: norm);

        return true;
    }
}
