using System;
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
                ShouldUpdateUi: !_closing && !_playbackDisposed,
                HideOverlay: !_isManualMarkMode,
                TotalEvents: _codingSessionHost.EventCollection?.Count ?? 0,
                HasPlayer: !_playbackDisposed,
                IsPlaybackDisposed: _playbackDisposed,
                IsPlayerPlaying: !_playbackDisposed && _playerPlaybackControlHost.IsPlaying),
            new LiveDetectionStopUiWorkflowActions(
                SetStoppedStatus: () => SetYoloStatus("Gestoppt", PlayerStatusColors.Muted),
                ClearOverlay: hideOverlay => DetectionOverlayCleaner.ClearCanvas(
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
    {
        var hideTimer = PlayerWindowTimerFactory.CreateOneShotTimer(TimeSpan.FromSeconds(5), () =>
        {
            if (!_liveDetectionController.IsDetecting)
                LiveDetectionStatusControls.HideDetectionStatus(LiveDetectionStatusText);
        });
        hideTimer.Start();
    }
}
