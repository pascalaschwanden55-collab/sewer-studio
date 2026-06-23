using System;
using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StopLiveDetection()
    {
        var updateUi = !_closing && !_playbackDisposed;

        _detectionTimer?.Stop();
        _detectionTimer = null;
        _detectionCts?.Cancel();
        _detectionCts?.Dispose();
        _detectionCts = null;
        _isDetecting = false;
        _isDetectionInFlight = false;
        _liveDetectionService = null;
        _liveDetectionClient?.Dispose();
        _liveDetectionClient = null;
        _liveDetectionModelName = string.Empty;
        _currentFindings.Clear();

        if (!updateUi)
            return;

        if (!_isManualMarkMode)
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        AiStatusBadge.Visibility = Visibility.Collapsed;
        SetYoloStatus("Gestoppt", PlayerStatusColors.Muted);
        DetectionCanvas.Children.Clear();
        FindingSummaryPanel.Visibility = Visibility.Collapsed;

        var totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusText.Text = $"KI-Analyse beendet \u2014 {totalEvents} Beobachtungen";
        LiveDetectionStatusText.Visibility = Visibility.Visible;

        if (_player != null && !_playbackDisposed && _player.IsPlaying)
            _player.SetPause(true);

        var hideTimer = PlayerWindowTimerFactory.CreateOneShotTimer(TimeSpan.FromSeconds(5), () =>
        {
            if (!_isDetecting)
                LiveDetectionStatusText.Visibility = Visibility.Collapsed;
        });
        hideTimer.Start();
    }
}
