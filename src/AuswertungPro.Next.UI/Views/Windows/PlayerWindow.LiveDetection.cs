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
        if (!LiveDetectionTimerPolicy.ShouldRunTick(
                isClosing: _closing,
                hasPlayer: player is not null,
                isDetectionInFlight: _isDetectionInFlight,
                hasLiveDetectionService: _liveDetectionService is not null,
                hasDetectionCancellation: _detectionCts is not null,
                isPlayerPlaying: player?.IsPlaying == true,
                hasPendingFindings: _detectionConfirmationBuffer.HasFindings))
            return;

        _isDetectionInFlight = true;
        SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Warning,
            $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Snapshot");

        try
        {
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _isDetectionInFlight = false;
                if (!_closing && !_playbackDisposed)
                {
                    SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                        $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Bereit");
                }
                return;
            }

            if (_closing || _playbackDisposed || _liveDetectionService is null || _detectionCts is null)
                return;

            SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Warning,
                $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Inferenz");
            var timestampSec = player!.Time / 1000.0;
            var result = await _liveDetectionService.AnalyzeFrameAsync(
                snapshot, timestampSec, _detectionCts.Token).ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                if (_closing || _playbackDisposed || !_isDetecting) return;

                _lastDetectionTimestamp = result.TimestampSeconds;
                _currentFindings.Clear();
                _currentFindings.AddRange(result.Findings);

                RenderDetectionOverlay(result.Findings, result.TimestampSeconds);
                UpdateDetectionStatus(result);

                SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                    $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Overlay");

                var significantFindings = LiveDetectionConfirmationPolicy.SelectSignificantFindings(result.Findings);
                if (significantFindings.Count > 0)
                {
                    _detectionConfirmationBuffer.StoreFindings(
                        significantFindings,
                        snapshot,
                        result.TimestampSeconds);
                    ShowDetectionConfirmation(significantFindings);
                    SetLiveDetectionBadge("Befund erkannt", PlayerStatusColors.Warning,
                        $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Warte auf Bestaetigung");
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
                    LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName));
            });
        }
        finally
        {
            _isDetectionInFlight = false;
        }
    }
}
