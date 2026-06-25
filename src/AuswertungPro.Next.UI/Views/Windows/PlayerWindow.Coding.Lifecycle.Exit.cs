using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ExitCodingMode()
    {
        CodingModeExitCommandWorkflow.Execute(
            new CodingModeExitCommandRequest(_isCodingMode),
            new CodingModeExitCommandActions(
                SetCodingMode: enabled => _isCodingMode = enabled,
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
                ResetProtocolMatchState: () => _lastCodingMatch = CodingProtocolMatchStateResetter.Reset(_codingProtocolMatchBuckets),
                UpdateProtocolMatchSummary: () => UpdateCodingProtocolMatchSummary(_lastCodingMatch),
                ClearImportEventsListSource: () => LstImportEvents.ItemsSource = null,
                HideConfirmationPanels: () => CodingModeChromeControls.HideConfirmationPanels(
                    CodingConfirmationPanel,
                    DetectionConfirmationPanel),
                ClearPendingConfirmation: () =>
                {
                    _codingPendingConfirmEvent = null;
                    _codingPendingGateResult = null;
                },
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
                ClearActiveCodingToolName: () => _activeCodingToolName = null,
                ResetCodingIndicators: () => CodingModeChromeControls.ResetCodingIndicators(
                    TxtActiveToolLabel,
                    BtnCodingLiveAi,
                    TxtCodingAiStage),
                CancelCodingSchema: _codingSchemaManager.Cancel,
                ClearCodingSchemaType: () => _codingSchemaType = null,
                DetachCodingViewModelPropertyChanged: _codingSessionViewModelOwner.DetachPropertyChanged,
                ClearCodingSessionReferences: () =>
                {
                    _codingSessionViewModelOwner.Clear();
                    _codingSessionRuntimeOwner.Clear();
                    _codingOverlayRuntimeOwner.Clear();
                },
                ClearCodingCalibrationState: () =>
                {
                    _codingIsCalibrating = false;
                    _codingCalibStart = null;
                },
                ResetFrameReadiness: ResetFrameReadiness,
                ResetCodingOverlaySuspendState: () =>
                {
                    _codingOverlaySuspendDepth = 0;
                    _codingOverlayWasOpenBeforeSuspend = false;
                }));

    private void CodingModeExit_Click(object sender, RoutedEventArgs e) => ExitCodingMode();
}
