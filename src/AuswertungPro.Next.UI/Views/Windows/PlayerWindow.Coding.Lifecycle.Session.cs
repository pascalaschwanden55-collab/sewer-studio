using System;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CreateCodingSessionState()
    {
        var state = CodingSessionStateFactory.Create(_videoPath, _dependencies.Settings);
        _codingSessionService = state.SessionService;
        _codingOverlayService = state.OverlayService;
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;
        _codingVm = state.ViewModel;
        _codingVm.PropertyChanged += CodingVm_PropertyChanged;
    }

    private void ApplyCodingDnCalibration()
    {
        if (_haltungRecord == null || _codingOverlayService == null)
            return;

        var dnCalibration = CodingDnCalibrationPolicy.Build(_haltungRecord.Fields);
        if (dnCalibration.Calibration != null)
            _codingOverlayService.SetCalibration(dnCalibration.Calibration);
        CodingSessionHeaderControls.ApplyCalibration(
            TxtCodingCalibDn,
            TxtCodingCalibStatus,
            dnCalibration);
    }

    private bool TryStartCodingSession()
    {
        return CodingSessionStartWorkflow.Execute(
            new CodingSessionStartWorkflowRequest(
                HasRequiredState: _haltungRecord != null && _codingVm != null && _codingSessionService != null,
                EndMeter: _codingVm?.EndMeter ?? 0),
            new CodingSessionStartWorkflowActions(
                ExecuteStartSession: () => _codingVm!.StartSessionCommand.Execute(_haltungRecord!),
                HasActiveSession: () => _codingSessionService!.ActiveSession != null,
                ShowSessionStartFailed: message => CodingModeDialogServiceFactory.Create().ShowSessionStartFailed(message),
                ExitCodingMode: ExitCodingMode,
                PauseSession: () => _codingSessionService!.PauseSession(),
                SetRangeText: endMeter => CodingSessionHeaderControls.SetRangeText(TxtCodingRange, endMeter),
                SetMeterText: meter => CodingMeterTimelineControls.SetText(TxtCodingMeter, meter)));
    }
}
