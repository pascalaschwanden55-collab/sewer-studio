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
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _liveDetectionController.EndDetection();
                if (!_closing && !_playbackDisposed)
                {
                    SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                        $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionController.ModelName)} | Bereit");
                }
                return;
            }

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
                if (_closing || _playbackDisposed || !_liveDetectionController.IsDetecting) return;

                _liveDetectionController.ApplyDetectionResult(result);

                RenderDetectionOverlay(result.Findings, result.TimestampSeconds);
                UpdateDetectionStatus(result);

                SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                    $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionController.ModelName)} | Overlay");

                var significantFindings = LiveDetectionConfirmationPolicy.SelectSignificantFindings(result.Findings);
                if (significantFindings.Count > 0)
                {
                    _detectionConfirmationBuffer.StoreFindings(
                        significantFindings,
                        snapshot,
                        result.TimestampSeconds);
                    ShowDetectionConfirmation(significantFindings);
                    SetLiveDetectionBadge("Befund erkannt", PlayerStatusColors.Warning,
                        $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionController.ModelName)} | Warte auf Bestaetigung");
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_closing || _playbackDisposed)
                return;

            var msg = ex.Message;
            if (msg.Length > 200) msg = msg[..200] + "...";
            Dispatcher.Invoke(() =>
            {
                if (_closing || _playbackDisposed)
                    return;

                LiveDetectionStatusControls.ShowDetectionError(LiveDetectionStatusText, msg);
                SetLiveDetectionBadge("KI Fehler", PlayerStatusColors.Error,
                    LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionController.ModelName));
            });
        }
        finally
        {
            _liveDetectionController.EndDetection();
        }
    }
}
