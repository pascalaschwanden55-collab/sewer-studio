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

        if (_codingVm != null && _codingVm.Events.Count > 0)
        {
            var endMeter = _codingLastOsdMeter ?? _codingVm.EndMeter;
            CloseTrackedStreckenschaeden(endMeter);
            if (!CloseOpenStreckenschaeden(endMeter))
            {
                _isCodingMode = true;
                return;
            }

            if (!CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode(_codingVm.Events))
            {
                var endTime = TimeSpan.FromMilliseconds(_player?.Length ?? 0);
                EnsureRohrendeExists(_codingVm.EndMeter, endTime, _detectionPendingFrameBytes);
            }
        }

        StopCodingOsdTimer();
        DisposeCodingOsdMeterService();
        _codingLiveAiTimers?.Stop(resetButton: true);
        StopCodingAiPulse();
        StopPipelineHealthMonitor();

        _codingAnalysisCts = CancellationTokenSourceLifecycle.CancelDisposeAndClear(_codingAnalysisCts);

        CodingImportReferenceStateResetter.ClearEvents(_codingImportEvents);
        _lastCodingMatch = CodingProtocolMatchStateResetter.Reset(_codingProtocolMatchBuckets);
        UpdateCodingProtocolMatchSummary(_lastCodingMatch);
        LstImportEvents.ItemsSource = null;

        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
        DetectionConfirmationPanel.Visibility = Visibility.Collapsed;
        _codingPendingConfirmEvent = null;
        _codingPendingGateResult = null;
        _detectionPendingFindings = null;
        _detectionPendingFrameBytes = null;
        _detectionPendingTimestampSec = null;
        DetectionOverlayCleaner.ClearCanvas(DetectionCanvas, DetectionOverlayGrid, hideOverlay: !_isDetecting);

        if (CodingOverlayCanvas.IsMouseCaptured)
            CodingOverlayCanvas.ReleaseMouseCapture();
        CodingOverlayPopup.IsOpen = false;
        CodingOverlayCanvas.Children.Clear();
        CodingOverlayCanvas.IsHitTestVisible = false;
        CodingOverlayCanvas.Cursor = Cursors.Arrow;
        CodingSidePanel.Visibility = Visibility.Collapsed;
        CodingSidePanelColumn.Width = new GridLength(0);
        CodingToolbar.Visibility = Visibility.Collapsed;
        CodingTimelinePanel.Visibility = Visibility.Collapsed;
        HideInlineDefectDetail();
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
        CodingMeasurementPanel.Visibility = Visibility.Collapsed;
        CodingOsdBadgeControls.Hide(OsdMeterBadge);
        LiveDetectionButton.Visibility = Visibility.Visible;
        LiveDetectionStatusControls.SetDetectionStatusVisibility(LiveDetectionStatusText, _isDetecting);

        _activeCodingToolName = null;
        TxtActiveToolLabel.Text = "";
        BtnCodingLiveAi.IsChecked = false;
        TxtCodingAiStage.Text = string.Empty;

        _codingSchemaManager.Cancel();
        _codingSchemaType = null;

        if (_codingVm != null)
            _codingVm.PropertyChanged -= CodingVm_PropertyChanged;
        _codingVm = null;
        _codingSessionService = null;
        _codingOverlayService = null;
        _codingIsCalibrating = false;
        _codingCalibStart = null;
        ResetFrameReadiness();
        _codingOverlaySuspendDepth = 0;
        _codingOverlayWasOpenBeforeSuspend = false;
    }

    private void CodingModeExit_Click(object sender, RoutedEventArgs e) => ExitCodingMode();
}
