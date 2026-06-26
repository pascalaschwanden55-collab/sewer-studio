using System;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void DetectionTimer_Tick(object? sender, EventArgs e)
        => LiveDetectionTimerDispatchWorkflow.Execute(
            new LiveDetectionTimerDispatchWorkflowRequest(
                _shutdownState.IsClosing,
                _shutdownState.IsPlaybackDisposed),
            new LiveDetectionTimerDispatchWorkflowActions(
                RunDetectionAsync,
                Dispatch: (runDetectionAsync, operationName, onError) =>
                    runDetectionAsync().SafeFireAndForget(operationName, onError),
                LogError: message => PlayerTrace.WriteLine(message)));

    private async Task RunDetectionAsync()
    {
        await LiveDetectionRunCommandWorkflow.ExecuteAsync(
            new LiveDetectionRunCommandActions(
                ShouldRunTick: () => _liveDetectionController.ShouldRunTick(
                    isClosing: _shutdownState.IsClosing,
                    hasPlayer: !_shutdownState.IsPlaybackDisposed,
                    isPlayerPlaying: !_shutdownState.IsPlaybackDisposed && _playerPlaybackControlHost.IsPlaying,
                    hasPendingFindings: _detectionConfirmationBuffer.HasFindings),
                GetModelName: () => _liveDetectionController.ModelName,
                BeginDetection: _liveDetectionController.BeginDetection,
                EndDetection: _liveDetectionController.EndDetection,
                CaptureCurrentFrameAsync: CaptureCurrentFrameAsync,
                GetTimestampSeconds: () => _playerTimelineHost.CurrentSecondsOrZero,
                GetDetectionCancellationToken: () => _liveDetectionController.DetectionCancellation?.Token,
                CreateAnalyzeFrameAsync: () => _liveDetectionController.CreateAnalyzeFrameAsync(),
                IsClosing: () => _shutdownState.IsClosing,
                IsPlaybackDisposed: () => _shutdownState.IsPlaybackDisposed,
                IsDetecting: () => _liveDetectionController.IsDetecting,
                InvokeOnUi: action => PlayerDispatcherScheduler.Invoke(Dispatcher, action),
                ApplyDetectionResult: _liveDetectionController.ApplyDetectionResult,
                RenderDetectionOverlay: RenderDetectionOverlay,
                UpdateDetectionStatus: UpdateDetectionStatus,
                SetLiveDetectionBadge: SetLiveDetectionBadge,
                StoreFindings: (findings, frameBytes, timestamp) => _detectionConfirmationBuffer.StoreFindings(
                    findings,
                    frameBytes,
                    timestamp),
                ShowDetectionConfirmation: ShowDetectionConfirmation,
                ShowDetectionError: message => LiveDetectionStatusControls.ShowDetectionError(
                    LiveDetectionStatusText,
                    message)));
    }
}
