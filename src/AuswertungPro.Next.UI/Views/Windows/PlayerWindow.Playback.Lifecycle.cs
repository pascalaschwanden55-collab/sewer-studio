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
        CancellationTokenSourceLifecycle.CancelIfPresent(_detectionCts);
        CancellationTokenSourceLifecycle.CancelIfPresent(_codingAnalysisCts);
        StopLiveDetection();
        StopPipelineHealthMonitor();

        AuswertungPro.Next.Application.Common.BestEffort.Try(
            () => { if (VideoView != null) VideoView.MediaPlayer = null; },
            "VLC: VideoView trennen");

        AuswertungPro.Next.Application.Common.BestEffort.Try(
            () => _player.Stop(),
            "VLC: Player stoppen");

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
        AuswertungPro.Next.Application.Common.BestEffort.Try(
            () => { if (VideoView != null) VideoView.MediaPlayer = null; },
            "VLC: VideoView trennen");
        try { _player.Dispose(); } catch (Exception ex) { PlayerTrace.WriteLine($"[PlayerWindow] MediaPlayer Dispose error: {ex.Message}"); }
        try { _libVlc.Dispose(); } catch (Exception ex) { PlayerTrace.WriteLine($"[PlayerWindow] LibVLC Dispose error: {ex.Message}"); }
    }

    private void StopPlayerTimers()
        => PlayerWindowTimerStopper.StopPlaybackTimers(
            _timer,
            _scrubTimer,
            _detectionTimer,
            _codingLiveAiTimers,
            _codingOsdTimer);
}
