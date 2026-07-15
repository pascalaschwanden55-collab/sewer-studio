using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StopLiveDetection()
    {
        _liveDetectionController.Stop();

        LiveDetectionStopUiWorkflow.Execute(
            new LiveDetectionStopUiWorkflowRequest(
                ShouldUpdateUi: !_shutdownState.IsUnavailable,
                HideOverlay: !_liveDetectionController.IsManualMarkMode,
                TotalEvents: _codingSessionHost.EventCollection?.Count ?? 0,
                HasPlayer: !_shutdownState.IsPlaybackDisposed,
                IsPlaybackDisposed: _shutdownState.IsPlaybackDisposed,
                IsPlayerPlaying: !_shutdownState.IsPlaybackDisposed && _playerPlaybackControlHost.IsPlaying),
            new LiveDetectionStopUiWorkflowActions(
                SetStoppedStatus: () => _liveDetectionStatusController.SetYoloStatus("Gestoppt", PlayerStatusColors.Muted),
                ClearOverlay: hideOverlay => DetectionOverlayCleanupController.ClearCanvas(
                    DetectionCanvas,
                    DetectionOverlayGrid,
                    hideOverlay),
                ShowStoppedDetectionStatus: totalEvents => LiveDetectionStatusControls.ShowStoppedDetectionStatus(
                    AiStatusBadge,
                    FindingSummaryPanel,
                    LiveDetectionStatusText,
                    totalEvents),
                SetPause: _playerPlaybackControlHost.SetPause,
                StartHideStatusTimer: StartLiveDetectionHideStatusTimer));
    }

    private void StartLiveDetectionHideStatusTimer()
        => LiveDetectionHideStatusTimerWorkflow.Schedule(
            new LiveDetectionHideStatusTimerDisplayActions(
                IsDetecting: () => _liveDetectionController.IsDetecting,
                HideDetectionStatus: () => LiveDetectionStatusControls.HideDetectionStatus(LiveDetectionStatusText)));
}
