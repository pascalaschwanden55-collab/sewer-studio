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
                ClearLastOpened: () => PlayerLastOpenedClearWorkflow.Execute(
                    new PlayerLastOpenedClearRequest(ReferenceEquals(_lastOpened, this)),
                    new PlayerLastOpenedClearActions(
                        ClearLastOpened: () => _lastOpened = null)),
                StopPlayerTimers: StopPlayerTimers,
                CancelQuickScan: _quickScanController.Cancel,
                CancelLiveDetection: _liveDetectionController.CancelDetectionIfPresent,
                CancelCodingAnalysis: _codingAiRuntimeOwner.Controller.CancelAnalysisIfPresent,
                StopLiveDetection: StopLiveDetection,
                StopPipelineHealthMonitor: StopPipelineHealthMonitor,
                DetachVideoView: () => _playerMediaRuntime.DetachVideoView(VideoView),
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
                DetachVideoView: () => _playerMediaRuntime.DetachVideoView(VideoView),
                DisposeMediaPlayer: () => _playerMediaRuntime.DisposeMediaPlayer(
                    message => PlayerTrace.WriteLine(message)),
                DisposeLibVlc: () => _playerMediaRuntime.DisposeLibVlc(
                    message => PlayerTrace.WriteLine(message))));
    }

    private void StopPlayerTimers()
        => PlayerWindowTimerStopper.StopPlaybackTimers(
            _timer,
            _scrubTimer,
            _liveDetectionController.DetectionTimer,
            _codingLiveAiTimerOwner.Controller,
            _codingOsdMeterController.Timer);
}
