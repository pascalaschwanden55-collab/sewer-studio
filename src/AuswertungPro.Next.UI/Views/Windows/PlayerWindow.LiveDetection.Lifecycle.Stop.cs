using System;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StopLiveDetection()
    {
        var updateUi = !_closing && !_playbackDisposed;

        _detectionTimer = PlayerWindowTimerStopper.StopAndClear(_detectionTimer);
        _detectionCts = CancellationTokenSourceLifecycle.CancelDisposeAndClear(_detectionCts);
        _isDetecting = false;
        _isDetectionInFlight = false;
        _liveDetectionService = null;
        _liveDetectionClient = DisposableReferenceLifecycle.DisposeAndClear(_liveDetectionClient);
        _liveDetectionModelName = string.Empty;
        _currentFindings.Clear();

        if (!updateUi)
            return;

        SetYoloStatus("Gestoppt", PlayerStatusColors.Muted);
        DetectionOverlayCleaner.ClearCanvas(DetectionCanvas, DetectionOverlayGrid, hideOverlay: !_isManualMarkMode);

        var totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusControls.ShowStoppedDetectionStatus(
            AiStatusBadge,
            FindingSummaryPanel,
            LiveDetectionStatusText,
            totalEvents);

        PlayerLiveDetectionStopPlayback.PauseIfRunning(
            _player != null,
            _playbackDisposed,
            _player?.IsPlaying == true,
            pause => _player!.SetPause(pause));

        var hideTimer = PlayerWindowTimerFactory.CreateOneShotTimer(TimeSpan.FromSeconds(5), () =>
        {
            if (!_isDetecting)
                LiveDetectionStatusControls.HideDetectionStatus(LiveDetectionStatusText);
        });
        hideTimer.Start();
    }
}
