using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        ToolsDropdownPopup.IsOpen = false;
        var state = CodingCalibrationTogglePolicy.Build(_codingIsCalibrating);
        _codingIsCalibrating = state.IsCalibrating;
        _codingCalibStart = null;
        _codingOverlayService.ActiveTool = state.ActiveTool;
        _activeCodingToolName = state.ActiveToolName;
        CodingOverlayInputControls.ApplyActiveToolSelection(TxtActiveToolLabel, BtnCodingCreateEvent, state.ToolLabel);

        _codingVm.CurrentOverlay = null;
        UpdateCodingOverlayInfo(null);

        CodingCalibrationControls.ApplyToggle(CodingCalibrationHint, TxtCodingCalibHint, state);
        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private void ApplyCodingCalibration(NormalizedPoint start, NormalizedPoint end)
    {
        if (_codingOverlayService == null) return;
        var p1 = CodingNormToPixel(start);
        var p2 = CodingNormToPixel(end);
        int dn = _codingOverlayService.Calibration?.NominalDiameterMm ?? 300;
        var result = CodingManualCalibrationPolicy.Build(start, end, p1, p2, dn);
        CodingManualCalibrationWorkflow.Apply(
            new CodingManualCalibrationWorkflowRequest(
                result,
                _activeCodingToolName,
                _codingSchemaManager.IsActive),
            new CodingManualCalibrationWorkflowActions(
                ShowInvalidHint: text => CodingCalibrationControls.ShowHint(TxtCodingCalibHint, text),
                ClearCalibrationStart: () => _codingCalibStart = null,
                SetOverlayCalibration: calibration => _codingOverlayService.SetCalibration(calibration),
                ApplySchemaCalibration: calibration => _codingSchemaManager.Active?.ApplyCalibration(calibration),
                ApplyManualResult: manualResult => CodingCalibrationControls.ApplyManualResult(
                    TxtCodingCalibStatus,
                    TxtCodingCalibHint,
                    manualResult),
                EndCalibrationMode: () =>
                {
                    _codingIsCalibrating = false;
                    _codingCalibStart = null;
                },
                ClearActiveToolName: () => _activeCodingToolName = null,
                HideHint: () => CodingCalibrationControls.HideHint(CodingCalibrationHint),
                UpdateOverlayCursor: UpdateCodingOverlayCursor,
                EnableCodingSchemaOverlay: () => UpdateCodingSchemaOverlay(enableCreateEvent: true)));
    }

    private bool TryStartCodingCalibration(NormalizedPoint norm)
    {
        if (!_codingIsCalibrating)
            return false;

        _codingCalibStart = norm;
        CodingOverlayCanvas.CaptureMouse();
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        return true;
    }

    private bool TryPreviewCodingCalibration(NormalizedPoint norm)
    {
        if (!_codingIsCalibrating || _codingCalibStart == null)
            return false;

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();

        var p1 = CodingNormToPixel(_codingCalibStart);
        var p2 = CodingNormToPixel(norm);
        var preview = CodingCalibrationPreviewPolicy.Build(p1, p2);
        _codingPreviewLine = CodingCalibrationPreviewLineRenderer.Render(CodingOverlayCanvas, preview);
        CodingCalibrationControls.ApplyPreview(TxtCodingCalibHint, preview);
        return true;
    }

    private bool TryFinishCodingCalibration(NormalizedPoint norm)
    {
        if (!_codingIsCalibrating || _codingCalibStart == null)
            return false;

        CodingOverlayCanvas.ReleaseMouseCapture();
        ApplyCodingCalibration(_codingCalibStart, norm);
        return true;
    }
}
