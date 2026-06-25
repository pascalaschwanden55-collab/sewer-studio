using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (!_codingOverlayToolHost.HasOverlayService || !_codingSessionHost.HasViewModel) return;
        ToolsDropdownPopup.IsOpen = false;
        var state = CodingCalibrationTogglePolicy.Build(_codingIsCalibrating);
        _codingIsCalibrating = state.IsCalibrating;
        _codingCalibStart = null;
        _codingOverlayToolHost.SetActiveTool(state.ActiveTool);
        _activeCodingToolName = state.ActiveToolName;
        CodingOverlayInputControls.ApplyActiveToolSelection(TxtActiveToolLabel, BtnCodingCreateEvent, state.ToolLabel);

        _codingSessionHost.ClearCurrentOverlay();
        UpdateCodingOverlayInfo(null);

        CodingCalibrationControls.ApplyToggle(CodingCalibrationHint, TxtCodingCalibHint, state);
        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private void ApplyCodingCalibration(NormalizedPoint start, NormalizedPoint end)
    {
        if (!_codingOverlayToolHost.HasOverlayService) return;
        var p1 = CodingNormToPixel(start);
        var p2 = CodingNormToPixel(end);
        int dn = _codingOverlayToolHost.NominalDiameterMm ?? 300;
        var result = CodingManualCalibrationPolicy.Build(start, end, p1, p2, dn);
        CodingManualCalibrationWorkflow.Apply(
            new CodingManualCalibrationWorkflowRequest(
                result,
                _activeCodingToolName,
                _codingSchemaManager.IsActive),
            new CodingManualCalibrationWorkflowActions(
                ShowInvalidHint: text => CodingCalibrationControls.ShowHint(TxtCodingCalibHint, text),
                ClearCalibrationStart: () => _codingCalibStart = null,
                SetOverlayCalibration: calibration => { _codingOverlayToolHost.SetCalibration(calibration); },
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
        return CodingCalibrationPointerWorkflow.Start(
            new CodingCalibrationPointerStartRequest(_codingIsCalibrating),
            new CodingCalibrationPointerStartActions(
                SetCalibrationStart: () => _codingCalibStart = norm,
                CaptureMouse: () => { CodingOverlayCanvas.CaptureMouse(); },
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn))
            .Handled;
    }

    private bool TryPreviewCodingCalibration(NormalizedPoint norm)
    {
        var calibrationStart = _codingCalibStart;

        return CodingCalibrationPointerWorkflow.Preview(
            new CodingCalibrationPointerPreviewRequest(
                _codingIsCalibrating,
                calibrationStart != null),
            new CodingCalibrationPointerPreviewActions(
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                RenderPreview: () =>
                {
                    var p1 = CodingNormToPixel(calibrationStart!);
                    var p2 = CodingNormToPixel(norm);
                    var preview = CodingCalibrationPreviewPolicy.Build(p1, p2);
                    _codingPreviewLine = CodingCalibrationPreviewLineRenderer.Render(CodingOverlayCanvas, preview);
                    CodingCalibrationControls.ApplyPreview(TxtCodingCalibHint, preview);
                }))
            .Handled;
    }

    private bool TryFinishCodingCalibration(NormalizedPoint norm)
    {
        var calibrationStart = _codingCalibStart;

        return CodingCalibrationPointerWorkflow.Finish(
            new CodingCalibrationPointerFinishRequest(
                _codingIsCalibrating,
                calibrationStart != null),
            new CodingCalibrationPointerFinishActions(
                ReleaseMouseCapture: CodingOverlayCanvas.ReleaseMouseCapture,
                ApplyCalibration: () => ApplyCodingCalibration(calibrationStart!, norm)))
            .Handled;
    }
}
