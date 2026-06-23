using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

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
        TxtActiveToolLabel.Text = state.ToolLabel;

        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        CodingCalibrationHint.Visibility = state.ShowHint ? Visibility.Visible : Visibility.Collapsed;
        TxtCodingCalibHint.Text = state.HintText;
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
        if (!result.IsValid || result.Calibration == null)
        {
            TxtCodingCalibHint.Text = result.HintText;
            _codingCalibStart = null;
            return;
        }

        var cal = result.Calibration;
        _codingOverlayService.SetCalibration(cal);
        _codingSchemaManager.Active?.ApplyCalibration(cal);

        TxtCodingCalibStatus.Text = result.StatusText;
        TxtCodingCalibHint.Text = result.HintText;

        _codingIsCalibrating = false;
        _codingCalibStart = null;
        if (string.Equals(_activeCodingToolName, CodingCalibrationTogglePolicy.CalibrateButtonName))
            _activeCodingToolName = null;
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
        UpdateCodingOverlayCursor();
        if (_codingSchemaManager.IsActive)
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
    }
}
