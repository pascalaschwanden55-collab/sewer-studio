using System.ComponentModel;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var result = PlayerWindowClosingWorkflow.Execute(
            new PlayerWindowClosingWorkflowRequest(_shutdownState.IsClosing),
            new PlayerWindowClosingWorkflowActions(
                ConfirmCanClose: ConfirmUnappliedCodingChangesOnClose,
                MarkClosing: _shutdownState.MarkClosing,
                ClearLastOpened: () => PlayerLastOpenedClearWorkflow.Execute(
                    new PlayerLastOpenedClearRequest(LastOpenedWindow.IsCurrent(this)),
                    new PlayerLastOpenedClearActions(
                        ClearLastOpened: LastOpenedWindow.Clear)),
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
            new PlayerWindowCleanupWorkflowRequest(_shutdownState.IsPlaybackDisposed),
            new PlayerWindowCleanupWorkflowActions(
                MarkPlaybackDisposed: _shutdownState.MarkPlaybackDisposed,
                StopPlayerTimers: StopPlayerTimers,
                DetachVideoView: () => _playerMediaRuntime.DetachVideoView(VideoView),
                DisposeMediaPlayer: () => _playerMediaRuntime.DisposeMediaPlayer(
                    message => PlayerTrace.WriteLine(message)),
                DisposeLibVlc: () => _playerMediaRuntime.DisposeLibVlc(
                    message => PlayerTrace.WriteLine(message))));
    }

    private void StopPlayerTimers()
        => _playerTimerController.StopPlaybackTimers(
            _liveDetectionController.DetectionTimer,
            _codingLiveAiTimerOwner.Controller,
            _codingOsdMeterController.Timer);
}
