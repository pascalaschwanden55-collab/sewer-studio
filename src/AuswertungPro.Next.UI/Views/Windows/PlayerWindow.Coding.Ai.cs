using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async void CodingAnalyzeFrame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunCodingAnalysisAsync("Aktuellen Frame analysieren...", disableAnalyzeButton: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingAnalyzeFrame_Click error: {ex.Message}");
        }
    }

    private async Task RunCodingAnalysisAsync(
        string activityText,
        bool disableAnalyzeButton = false,
        string? keywordHint = null,
        string? codeHint = null)
    {
        if ((_codingEnhancedVision == null && _codingLiveDetection == null && _codingMultiModel == null)
            || _codingIsAnalyzing)
            return;

        _codingIsAnalyzing = true;
        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts = new CancellationTokenSource();

        try
        {
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = false;

            var captureTimestampSec = _player.Time / 1000.0;
            var currentMeterForStop = ResolveCodingMeterForFrame(captureTimestampSec);
            var currentVideoTimeForStop = TimeSpan.FromSeconds(captureTimestampSec);
            if (IsCodingAfterTerminalBoundary(currentMeterForStop, currentVideoTimeForStop))
            {
                ClearDetectionOverlays();
                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                SetCodingAiState("Rohrende erreicht - KI-Analyse gestoppt",
                    PlayerStatusColors.Success, "Codierung abgeschlossen");
                return;
            }

            if (_codingUseMultiModel && _codingMultiModel != null)
            {
                await RunCodingMultiModelAnalysisAsync(activityText, captureTimestampSec);
                return;
            }

            SetCodingAiState(activityText, PlayerStatusColors.Warning,
                "Schritt 1 von 3: Snapshot", pulse: true);

            var pngBytes = await CaptureSnapshotAsync(_codingAnalysisCts.Token);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                SetCodingAiState("Frame nicht extrahierbar", PlayerStatusColors.Error,
                    $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
                return;
            }

            _detectionPendingFrameBytes = pngBytes;
            _detectionPendingTimestampSec = captureTimestampSec;
            var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
                pngBytes,
                captureTimestampSec,
                _codingAnalysisCts.Token);

            SetCodingAiState(activityText, PlayerStatusColors.Warning,
                $"Schritt 2 von 3: Inferenz ({LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)})",
                pulse: true);

            LiveDetection result;
            if (_codingEnhancedVision != null)
            {
                var b64 = Convert.ToBase64String(pngBytes);
                var importContext = GatherImportContext();
                var enhanced = await _codingEnhancedVision.AnalyzeAsync(
                    b64, importContext, _codingAnalysisCts.Token);
                result = LiveDetectionMapper.FromEnhancedAnalysis(enhanced, captureTimestampSec);
            }
            else
            {
                result = await _codingLiveDetection!.AnalyzeFrameAsync(
                    pngBytes, captureTimestampSec, _codingAnalysisCts.Token);
            }

            result = result with { MeterReading = frameOsdMeter };
            ShowCodingAiResults(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
        }
        finally
        {
            _codingIsAnalyzing = false;
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = true;
        }
    }

}
