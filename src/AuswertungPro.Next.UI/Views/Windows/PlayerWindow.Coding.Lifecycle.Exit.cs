using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ExitCodingMode()
    {
        CodingModeExitCommandWorkflow.Execute(
            new CodingModeExitCommandRequest(_codingModeState.IsCodingMode),
            new CodingModeExitCommandActions(
                SetCodingMode: _codingModeState.Set,
                FinalizeExit: FinalizeCodingModeExit,
                Teardown: TeardownCodingModeExit));
    }

    private CodingModeExitFinalizationWorkflowResult FinalizeCodingModeExit()
        => CodingModeExitFinalizationWorkflow.Execute(
            new CodingModeExitFinalizationWorkflowRequest(
                _codingSessionHost.EventCollection,
                _codingOsdMeterController.LastMeter,
                _codingSessionHost.EndMeter,
                _playerTimelineHost.DurationTimeOrZero,
                _detectionConfirmationBuffer.FrameBytes),
            new CodingModeExitFinalizationWorkflowActions(
                CloseTrackedStreckenschaeden,
                CloseOpenStreckenschaeden,
                EnsureRohrendeExists));

    private void TeardownCodingModeExit()
        => CodingModeExitTeardownWorkflow.Execute(
            new CodingModeExitTeardownWorkflowRequest(
                HasCodingLiveAiTimers: _codingLiveAiTimers is not null,
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                IsLiveDetectionRunning: _liveDetectionController.IsDetecting),
            new CodingModeExitTeardownWorkflowActions(
                StopCodingOsdTimer: StopCodingOsdTimer,
                DisposeCodingOsdMeterService: DisposeCodingOsdMeterService,
                StopCodingLiveAiTimers: resetButton => _codingLiveAiTimers!.Stop(resetButton),
                StopCodingAiPulse: StopCodingAiPulse,
                StopPipelineHealthMonitor: StopPipelineHealthMonitor,
                DisposeAnalysisCancellation: _codingAiRuntimeOwner.Controller.DisposeAnalysisCancellation,
                ClearImportReferenceEvents: () => CodingImportReferenceStateResetter.ClearEvents(_codingImportEvents),
                ResetProtocolMatchState: () =>
                {
                    _codingProtocolMatchState.Reset();
                },
                UpdateProtocolMatchSummary: () => UpdateCodingProtocolMatchSummary(_codingProtocolMatchState.LastMatch),
                ClearImportEventsListSource: () => LstImportEvents.ItemsSource = null,
                HideConfirmationPanels: () => CodingModeChromeControls.HideConfirmationPanels(
                    CodingConfirmationPanel,
                    DetectionConfirmationPanel),
                ClearPendingConfirmation: _codingPendingConfirmationState.Clear,
                ClearDetectionConfirmationBuffer: _detectionConfirmationBuffer.Clear,
                ClearDetectionOverlay: hideOverlay => DetectionOverlayCleanupController.ClearCanvas(
                    DetectionCanvas,
                    DetectionOverlayGrid,
                    hideOverlay),
                HideCodingSurface: () => CodingModeChromeControls.HideCodingSurface(
                    CodingOverlayPopup,
                    CodingOverlayCanvas,
                    CodingSidePanel,
                    CodingSidePanelColumn,
                    CodingToolbar,
                    CodingTimelinePanel,
                    CodingCalibrationHint,
                    CodingMeasurementPanel),
                HideInlineDefectDetail: HideInlineDefectDetail,
                HideOsdBadge: () => CodingOsdBadgeControls.Hide(OsdMeterBadge),
                ShowLiveDetectionEntry: isDetecting => CodingModeChromeControls.ShowLiveDetectionEntry(
                    LiveDetectionButton,
                    LiveDetectionStatusText,
                    isDetecting),
                ClearActiveCodingToolName: _codingActiveToolNameState.Clear,
                ResetCodingIndicators: () => CodingModeChromeControls.ResetCodingIndicators(
                    TxtActiveToolLabel,
                    BtnCodingLiveAi,
                    TxtCodingAiStage),
                CancelCodingSchema: _codingSchemaManager.Cancel,
                ClearCodingSchemaType: _codingSchemaTypeState.Clear,
                DetachCodingViewModelPropertyChanged: _codingSessionViewModelOwner.DetachPropertyChanged,
                ClearCodingSessionReferences: () =>
                {
                    _codingSessionViewModelOwner.Clear();
                    _codingSessionRuntimeOwner.Clear();
                    _codingOverlayRuntimeOwner.Clear();
                },
                ClearCodingCalibrationState: _codingCalibrationState.Reset,
                ResetFrameReadiness: ResetFrameReadiness,
                ResetCodingOverlaySuspendState: _codingOverlayInputVisibilityState.ResetSuspendState));

    private void CodingModeExit_Click(object sender, RoutedEventArgs e) => ExitCodingMode();
}
