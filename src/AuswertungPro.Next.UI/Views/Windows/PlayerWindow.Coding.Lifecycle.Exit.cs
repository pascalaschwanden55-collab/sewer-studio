using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ExitCodingMode()
    {
        if (!_isCodingMode) return;
        _isCodingMode = false;

        var finalization = CodingModeExitFinalizationWorkflow.Execute(
            new CodingModeExitFinalizationWorkflowRequest(
                _codingVm?.Events,
                _codingOsdMeterController.LastMeter,
                _codingVm?.EndMeter ?? 0,
                TimeSpan.FromMilliseconds(_player?.Length ?? 0),
                _detectionConfirmationBuffer.FrameBytes),
            new CodingModeExitFinalizationWorkflowActions(
                CloseTrackedStreckenschaeden,
                CloseOpenStreckenschaeden,
                EnsureRohrendeExists));
        if (!finalization.CanExit)
        {
            _isCodingMode = true;
            return;
        }

        CodingModeExitTeardownWorkflow.Execute(
            new CodingModeExitTeardownWorkflowRequest(
                HasCodingLiveAiTimers: _codingLiveAiTimers is not null,
                HasCodingViewModel: _codingVm is not null,
                IsLiveDetectionRunning: _liveDetectionController.IsDetecting),
            new CodingModeExitTeardownWorkflowActions(
                StopCodingOsdTimer: StopCodingOsdTimer,
                DisposeCodingOsdMeterService: DisposeCodingOsdMeterService,
                StopCodingLiveAiTimers: resetButton => _codingLiveAiTimers!.Stop(resetButton),
                StopCodingAiPulse: StopCodingAiPulse,
                StopPipelineHealthMonitor: StopPipelineHealthMonitor,
                DisposeAnalysisCancellation: _codingAiController.DisposeAnalysisCancellation,
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
                ClearDetectionOverlay: hideOverlay => DetectionOverlayCleaner.ClearCanvas(
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
                DetachCodingViewModelPropertyChanged: () => _codingVm!.PropertyChanged -= CodingVm_PropertyChanged,
                ClearCodingSessionReferences: () =>
                {
                    _codingVm = null;
                    _codingSessionService = null;
                    _codingOverlayService = null;
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
    }

    private void CodingModeExit_Click(object sender, RoutedEventArgs e) => ExitCodingMode();
}
