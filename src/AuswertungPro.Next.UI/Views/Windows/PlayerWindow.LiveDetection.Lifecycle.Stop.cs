using System;
using System.Windows;
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

        AiStatusBadge.Visibility = Visibility.Collapsed;
        SetYoloStatus("Gestoppt", PlayerStatusColors.Muted);
        DetectionOverlayCleaner.ClearCanvas(DetectionCanvas, DetectionOverlayGrid, hideOverlay: !_isManualMarkMode);
        FindingSummaryPanel.Visibility = Visibility.Collapsed;

        var totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusText.Text = $"KI-Analyse beendet \u2014 {totalEvents} Beobachtungen";
        LiveDetectionStatusText.Visibility = Visibility.Visible;

        PlayerLiveDetectionStopPlayback.PauseIfRunning(
            _player != null,
            _playbackDisposed,
            _player?.IsPlaying == true,
            pause => _player!.SetPause(pause));

        var hideTimer = PlayerWindowTimerFactory.CreateOneShotTimer(TimeSpan.FromSeconds(5), () =>
        {
            if (!_isDetecting)
                LiveDetectionStatusText.Visibility = Visibility.Collapsed;
        });
        hideTimer.Start();
    }
}
