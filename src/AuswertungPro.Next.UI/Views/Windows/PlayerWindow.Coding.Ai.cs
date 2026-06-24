using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingAnalyzeFrame_Click(object sender, RoutedEventArgs e)
        => RunCodingAnalysisAsync("Aktuellen Frame analysieren...", disableAnalyzeButton: true)
            .SafeFireAndForget(
                "CodingAnalyzeFrame",
                ex => PlayerTrace.WriteLine($"[PlayerWindow] CodingAnalyzeFrame_Click error: {ex.Message}"));

    private async Task RunCodingAnalysisAsync(
        string activityText,
        bool disableAnalyzeButton = false,
        string? keywordHint = null,
        string? codeHint = null)
    {
        if (!_codingAiController.TryBeginAnalysis())
            return;

        var analysisCts = _codingAiController.AnalysisCancellation!;

        try
        {
            var preflight = CodingAnalysisPreflightWorkflow.Execute(
                new CodingAnalysisPreflightWorkflowRequest(
                    disableAnalyzeButton,
                    _codingAiController.UseMultiModel,
                    _codingAiController.MultiModel != null),
                new CodingAnalysisPreflightWorkflowActions(
                    SetAnalyzeButtonEnabled: enabled => CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, enabled),
                    ResolveFramePosition: () =>
                    {
                        var timestamp = _player.Time / 1000.0;
                        return new CodingAnalysisFramePosition(
                            timestamp,
                            ResolveCodingMeterForFrame(timestamp),
                            TimeSpan.FromSeconds(timestamp));
                    },
                    IsAfterTerminalBoundary: framePosition => IsCodingAfterTerminalBoundary(
                        framePosition.CurrentMeter,
                        framePosition.VideoTime),
                    ClearDetectionOverlays: ClearDetectionOverlays,
                    ClearSamMasks: () => Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas),
                    SetCodingAiState: (status, color, detail) => SetCodingAiState(status, color, detail)));

            var captureTimestampSec = preflight.CaptureTimestampSeconds;
            if (preflight.Outcome == CodingAnalysisPreflightWorkflowOutcome.StopAtTerminalBoundary)
                return;

            if (preflight.Outcome == CodingAnalysisPreflightWorkflowOutcome.RunMultiModel)
            {
                await RunCodingMultiModelAnalysisAsync(activityText, captureTimestampSec);
                return;
            }

            SetCodingAiState(activityText, PlayerStatusColors.Warning,
                "Schritt 1 von 3: Snapshot", pulse: true);

            var pngBytes = await CaptureSnapshotAsync(analysisCts.Token);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                SetCodingAiState("Frame nicht extrahierbar", PlayerStatusColors.Error,
                    $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiController.ModelName)}");
                return;
            }

            _detectionConfirmationBuffer.StoreAnalyzedFrame(pngBytes, captureTimestampSec);
            var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
                pngBytes,
                captureTimestampSec,
                analysisCts.Token);

            SetCodingAiState(activityText, PlayerStatusColors.Warning,
                $"Schritt 2 von 3: Inferenz ({LiveDetectionDisplayPolicy.CompactModelName(_codingAiController.ModelName)})",
                pulse: true);

            LiveDetection result;
            if (_codingAiController.EnhancedVision != null)
            {
                var b64 = Convert.ToBase64String(pngBytes);
                var importContext = GatherImportContext();
                var enhanced = await _codingAiController.EnhancedVision.AnalyzeAsync(
                    b64, importContext, analysisCts.Token);
                result = LiveDetectionMapper.FromEnhancedAnalysis(enhanced, captureTimestampSec);
            }
            else
            {
                result = await _codingAiController.LiveDetection!.AnalyzeFrameAsync(
                    pngBytes, captureTimestampSec, analysisCts.Token);
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
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiController.ModelName)}");
        }
        finally
        {
            _codingAiController.EndAnalysis();
            if (disableAnalyzeButton)
                CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, true);
        }
    }

}
