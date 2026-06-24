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
                TotalEvents: _codingVm?.Events?.Count ?? 0,
                HasPlayer: _player is not null,
                IsPlaybackDisposed: _playbackDisposed,
                IsPlayerPlaying: _player?.IsPlaying == true),
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
                SetPause: pause => _player!.SetPause(pause),
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
