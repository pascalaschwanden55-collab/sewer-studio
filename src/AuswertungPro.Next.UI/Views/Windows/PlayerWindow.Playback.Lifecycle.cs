using System.ComponentModel;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var result = PlayerWindowClosingWorkflow.Execute(
            new PlayerWindowClosingWorkflowRequest(_closing),
            new PlayerWindowClosingWorkflowActions(
                ConfirmCanClose: ConfirmUnappliedCodingChangesOnClose,
                MarkClosing: () => _closing = true,
                ClearLastOpened: () =>
                {
                    if (ReferenceEquals(_lastOpened, this))
                        _lastOpened = null;
                },
                StopPlayerTimers: StopPlayerTimers,
                CancelQuickScan: _quickScanController.Cancel,
                CancelLiveDetection: _liveDetectionController.CancelDetectionIfPresent,
                CancelCodingAnalysis: _codingAiRuntimeOwner.Controller.CancelAnalysisIfPresent,
                StopLiveDetection: StopLiveDetection,
                StopPipelineHealthMonitor: StopPipelineHealthMonitor,
                DetachVideoView: () => PlayerPlaybackResourceCleaner.DetachVideoView(
                    () => { if (VideoView != null) VideoView.MediaPlayer = null; }),
                StopPlayer: () => PlayerPlaybackResourceCleaner.StopPlayer(_playerPlaybackControlHost.Stop),
                Cleanup: Cleanup,
                LogCleanupError: ex => PlayerTrace.WriteLine($"[PlayerWindow] OnClosing error: {ex.Message}")));
        e.Cancel = result.CancelClose;
    }

    private void Cleanup()
    {
        PlayerWindowCleanupWorkflow.Execute(
            new PlayerWindowCleanupWorkflowRequest(_playbackDisposed),
            new PlayerWindowCleanupWorkflowActions(
                MarkPlaybackDisposed: () => _playbackDisposed = true,
                StopPlayerTimers: StopPlayerTimers,
                DetachVideoView: () => PlayerPlaybackResourceCleaner.DetachVideoView(
                    () => { if (VideoView != null) VideoView.MediaPlayer = null; }),
                DisposeMediaPlayer: () => PlayerPlaybackResourceCleaner.DisposeMediaPlayer(
                    _player,
                    message => PlayerTrace.WriteLine(message)),
                DisposeLibVlc: () => PlayerPlaybackResourceCleaner.DisposeLibVlc(
                    _libVlc,
                    message => PlayerTrace.WriteLine(message))));
    }

    private void StopPlayerTimers()
        => PlayerWindowTimerStopper.StopPlaybackTimers(
            _timer,
            _scrubTimer,
            _liveDetectionController.DetectionTimer,
            _codingLiveAiTimers,
            _codingOsdMeterController.Timer);
}
