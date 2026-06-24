using System;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StopLiveDetection()
    {
        var updateUi = !_closing && !_playbackDisposed;

        _liveDetectionController.Stop(
            updateUi,
            new LiveDetectionControllerStopActions(
                SetStoppedStatus: () => SetYoloStatus("Gestoppt", PlayerStatusColors.Muted),
                ClearOverlay: () => DetectionOverlayCleaner.ClearCanvas(
                    DetectionCanvas,
                    DetectionOverlayGrid,
                    hideOverlay: !_isManualMarkMode),
                ShowStoppedDetectionStatus: ShowStoppedDetectionStatus,
                PausePlaybackIfRunning: PauseLiveDetectionPlaybackIfRunning,
                StartHideStatusTimer: StartLiveDetectionHideStatusTimer));
    }

    private void ShowStoppedDetectionStatus()
    {
        var totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusControls.ShowStoppedDetectionStatus(
            AiStatusBadge,
            FindingSummaryPanel,
            LiveDetectionStatusText,
            totalEvents);
    }

    private void PauseLiveDetectionPlaybackIfRunning()
        => PlayerLiveDetectionStopPlayback.PauseIfRunning(
            _player != null,
            _playbackDisposed,
            _player?.IsPlaying == true,
            pause => _player!.SetPause(pause));

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
