using System;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void DetectionTimer_Tick(object? sender, EventArgs e)
    {
        if (_closing || _player is null) return;

        RunDetectionAsync().SafeFireAndForget(
            "DetectionTimer",
            ex => PlayerTrace.WriteLine($"[PlayerWindow] DetectionTimer_Tick Fehler: {ex.Message}"));
    }

    private async Task RunDetectionAsync()
    {
        var player = _player;
        if (!_liveDetectionController.ShouldRunTick(
                isClosing: _closing,
                hasPlayer: player is not null,
                isPlayerPlaying: player?.IsPlaying == true,
                hasPendingFindings: _detectionConfirmationBuffer.HasFindings))
            return;

        _liveDetectionController.BeginDetection();
        SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Warning,
            $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionController.ModelName)} | Snapshot");

        try
        {
            var snapshotResult = LiveDetectionSnapshotWorkflow.Handle(
                new LiveDetectionSnapshotWorkflowRequest(
                    await CaptureCurrentFrameAsync(),
                    _closing,
                    _playbackDisposed,
                    _liveDetectionController.ModelName),
                new LiveDetectionSnapshotWorkflowActions(
                    _liveDetectionController.EndDetection,
                    SetLiveDetectionBadge));
            if (!snapshotResult.HasSnapshot)
                return;

            var snapshot = snapshotResult.Snapshot!;

            var service = _liveDetectionController.Service;
            var cancellation = _liveDetectionController.DetectionCancellation;
            if (_closing || _playbackDisposed || service is null || cancellation is null)
                return;

            SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Warning,
                $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionController.ModelName)} | Inferenz");
            var timestampSec = player!.Time / 1000.0;
            var result = await service.AnalyzeFrameAsync(
                snapshot, timestampSec, cancellation.Token).ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                LiveDetectionResultWorkflow.Execute(
                    new LiveDetectionResultWorkflowRequest(
                        result,
                        snapshot,
                        _closing,
                        _playbackDisposed,
                        _liveDetectionController.IsDetecting,
                        _liveDetectionController.ModelName),
                    new LiveDetectionResultWorkflowActions(
                        _liveDetectionController.ApplyDetectionResult,
                        RenderDetectionOverlay,
                        UpdateDetectionStatus,
                        SetLiveDetectionBadge,
                        (findings, frameBytes, timestamp) => _detectionConfirmationBuffer.StoreFindings(
                            findings,
                            frameBytes,
                            timestamp),
                        ShowDetectionConfirmation));
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_closing || _playbackDisposed)
                return;

            Dispatcher.Invoke(() =>
            {
                LiveDetectionErrorWorkflow.Execute(
                    new LiveDetectionErrorWorkflowRequest(
                        ex,
                        _closing,
                        _playbackDisposed,
                        _liveDetectionController.ModelName),
                    new LiveDetectionErrorWorkflowActions(
                        message => LiveDetectionStatusControls.ShowDetectionError(
                            LiveDetectionStatusText,
                            message),
                        SetLiveDetectionBadge));
            });
        }
        finally
        {
            _liveDetectionController.EndDetection();
        }
    }
}
