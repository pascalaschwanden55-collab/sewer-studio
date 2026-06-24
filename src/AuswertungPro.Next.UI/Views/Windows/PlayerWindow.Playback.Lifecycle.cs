using System;
using System.ComponentModel;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closing)
            return;

        if (!ConfirmUnappliedCodingChangesOnClose())
        {
            e.Cancel = true;
            return;
        }

        _closing = true;
        if (ReferenceEquals(_lastOpened, this))
            _lastOpened = null;

        StopPlayerTimers();
        _quickScanController.Cancel();
        _liveDetectionController.CancelDetectionIfPresent();
        CancellationTokenSourceLifecycle.CancelIfPresent(_codingAnalysisCts);
        StopLiveDetection();
        StopPipelineHealthMonitor();

        PlayerPlaybackResourceCleaner.DetachVideoView(
            () => { if (VideoView != null) VideoView.MediaPlayer = null; });

        PlayerPlaybackResourceCleaner.StopPlayer(() => _player.Stop());

        try
        {
            Cleanup();
        }
        catch (Exception ex)
        {
            PlayerTrace.WriteLine($"[PlayerWindow] OnClosing error: {ex.Message}");
        }
    }

    private void Cleanup()
    {
        if (_playbackDisposed)
            return;

        _playbackDisposed = true;
        StopPlayerTimers();
        PlayerPlaybackResourceCleaner.DetachVideoView(
            () => { if (VideoView != null) VideoView.MediaPlayer = null; });
        PlayerPlaybackResourceCleaner.DisposeMediaPlayer(_player, message => PlayerTrace.WriteLine(message));
        PlayerPlaybackResourceCleaner.DisposeLibVlc(_libVlc, message => PlayerTrace.WriteLine(message));
    }

    private void StopPlayerTimers()
        => PlayerWindowTimerStopper.StopPlaybackTimers(
            _timer,
            _scrubTimer,
            _liveDetectionController.DetectionTimer,
            _codingLiveAiTimers,
            _codingOsdMeterController.Timer);
}
