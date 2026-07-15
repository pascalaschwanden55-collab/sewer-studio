using System;
using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CreateCodingSessionState()
    {
        CodingSessionStateCreationWorkflow.Execute(
            new CodingSessionStateCreationRequest(
                _playbackContext.VideoPath,
                _protocolContext.Settings),
            new CodingSessionStateCreationApplyActions(
                SetSessionService: _codingSessionRuntimeOwner.Set,
                SetOverlayService: _codingOverlayRuntimeOwner.Set,
                CancelSchema: _codingSchemaManager.Cancel,
                ClearSchemaType: _codingSchemaTypeState.Clear,
                SetViewModel: (viewModel, observePropertyChanged) => _codingSessionViewModelOwner.Set(
                    viewModel,
                    observePropertyChanged)));
    }

    private void ApplyCodingDnCalibration()
    {
        CodingDnCalibrationApplyWorkflow.Execute(
            new CodingDnCalibrationApplyWorkflowRequest(
                HasHaltungRecord: _protocolContext.HasHaltungRecord,
                HasOverlayService: _codingOverlayRuntimeOwner.HasService),
            new CodingDnCalibrationApplyWorkflowActions(
                BuildCalibration: () => CodingDnCalibrationPolicy.Build(_protocolContext.HaltungRecord!.Fields),
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
                HasRequiredState: _protocolContext.HasHaltungRecord && _codingSessionHost.HasViewModel && _codingSessionRuntimeOwner.Service != null,
                EndMeter: _codingSessionHost.EndMeter),
            new CodingSessionStartWorkflowActions(
                ExecuteStartSession: () => _codingSessionHost.ExecuteStartSession(_protocolContext.HaltungRecord!),
                HasActiveSession: () => _codingSessionRuntimeOwner.Service!.ActiveSession != null,
                ShowSessionStartFailed: CodingModeDialogWorkflow.ShowSessionStartFailed,
                ExitCodingMode: _codingModeExitController.Exit,
                PauseSession: () => _codingSessionRuntimeOwner.Service!.PauseSession(),
                SetRangeText: endMeter => CodingSessionHeaderControls.SetRangeText(TxtCodingRange, endMeter),
                SetMeterText: meter => CodingMeterTimelineControls.SetText(TxtCodingMeter, meter)));
    }

    private void CodingModeExit_Click(object sender, RoutedEventArgs e)
        => _codingModeExitController.Exit();
}
