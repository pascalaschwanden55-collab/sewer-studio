using System;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CreateCodingSessionState()
    {
        CodingSessionStateCreationWorkflow.Execute(
            new CodingSessionStateCreationRequest(
                _videoPath,
                _dependencies.Settings),
            new CodingSessionStateCreationApplyActions(
                SetSessionService: _codingSessionRuntimeOwner.Set,
                SetOverlayService: _codingOverlayRuntimeOwner.Set,
                CancelSchema: _codingSchemaManager.Cancel,
                ClearSchemaType: () => _codingSchemaType = null,
                SetViewModel: (viewModel, observePropertyChanged) => _codingSessionViewModelOwner.Set(
                    viewModel,
                    observePropertyChanged)));
    }

    private void ApplyCodingDnCalibration()
    {
        CodingDnCalibrationApplyWorkflow.Execute(
            new CodingDnCalibrationApplyWorkflowRequest(
                HasHaltungRecord: _haltungRecord != null,
                HasOverlayService: _codingOverlayRuntimeOwner.HasService),
            new CodingDnCalibrationApplyWorkflowActions(
                BuildCalibration: () => CodingDnCalibrationPolicy.Build(_haltungRecord!.Fields),
                SetCalibration: calibration => _codingOverlayToolHost.SetCalibration(calibration),
                ApplyCalibrationControls: dnCalibration => CodingSessionHeaderControls.ApplyCalibration(
                    TxtCodingCalibDn,
                    TxtCodingCalibStatus,
                    dnCalibration)));
    }

    private bool TryStartCodingSession()
    {
        return CodingSessionStartWorkflow.Execute(
            new CodingSessionStartWorkflowRequest(
                HasRequiredState: _haltungRecord != null && _codingSessionHost.HasViewModel && _codingSessionRuntimeOwner.Service != null,
                EndMeter: _codingSessionHost.EndMeter),
            new CodingSessionStartWorkflowActions(
                ExecuteStartSession: () => _codingSessionHost.ExecuteStartSession(_haltungRecord!),
                HasActiveSession: () => _codingSessionRuntimeOwner.Service!.ActiveSession != null,
                ShowSessionStartFailed: CodingModeDialogWorkflow.ShowSessionStartFailed,
                ExitCodingMode: ExitCodingMode,
                PauseSession: () => _codingSessionRuntimeOwner.Service!.PauseSession(),
                SetRangeText: endMeter => CodingSessionHeaderControls.SetRangeText(TxtCodingRange, endMeter),
                SetMeterText: meter => CodingMeterTimelineControls.SetText(TxtCodingMeter, meter)));
    }
}
