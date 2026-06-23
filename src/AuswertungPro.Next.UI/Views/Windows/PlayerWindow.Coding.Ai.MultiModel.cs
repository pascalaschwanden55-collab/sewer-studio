using System;
using System.Threading.Tasks;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task RunCodingMultiModelAnalysisAsync(string activityText, double captureTimestampSec)
    {
        var multiModel = _codingMultiModel;
        if (multiModel == null || _codingAnalysisCts == null)
            return;

        SetCodingAiState(activityText, PlayerStatusColors.Warning,
            "Schritt 1 von 4: Snapshot", pulse: true);

        var pngBytes = await CaptureSnapshotAsync(_codingAnalysisCts.Token);
        if (pngBytes == null || pngBytes.Length == 0)
        {
            SetCodingAiState("Frame nicht extrahierbar", PlayerStatusColors.Error,
                "Multi-Model");
            return;
        }

        _detectionPendingFrameBytes = pngBytes;
        _detectionPendingTimestampSec = captureTimestampSec;
        var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
            pngBytes,
            captureTimestampSec,
            _codingAnalysisCts.Token);

        var readinessProbe = new LiveDetection(
            captureTimestampSec,
            Array.Empty<LiveFrameFinding>(),
            frameOsdMeter,
            null);
        UpdateFrameReadiness(readinessProbe);
        if (!IsFrameReady())
        {
            SetCodingAiState("Dateneinblendung erkannt - uebersprungen",
                PlayerStatusColors.Muted, "Warte auf sauberes Videobild...");
            return;
        }

        SetCodingAiState(activityText, PlayerStatusColors.Warning,
            "Schritt 2 von 4: YOLO und DINO", pulse: true);

        var currentMeterForClassifier = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var classifierInput = CodingMultiModelClassifierInputPolicy.Build(
            _codingOverlayService?.Calibration?.NominalDiameterMm,
            currentMeterForClassifier,
            _codingVm?.EndMeter);

        var mmResult = await multiModel.AnalyzeFrameAsync(
            pngBytes, classifierInput.NominalDiameterMm, _codingOverlayService?.Calibration,
            _codingAnalysisCts.Token,
            classifierInput.CurrentMeter,
            classifierInput.ReachLength);

        if (mmResult.Error != null)
        {
            SetCodingAiState($"Fehler: {mmResult.Error}", PlayerStatusColors.Error,
                "Multi-Model");
            return;
        }

        if (TryHandleBoundaryClassifierResult(mmResult, captureTimestampSec, frameOsdMeter))
            return;

        if (TryHandleStructuralClassifierResult(mmResult, captureTimestampSec, frameOsdMeter))
            return;

        if (!mmResult.IsRelevant || !mmResult.HasDetections)
        {
            SetCodingAiState("Kein Schaden erkannt", PlayerStatusColors.Success,
                $"YOLO {mmResult.YoloTimeMs:F0}ms | {mmResult.DinoDetections.Count} Detektionen");
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            return;
        }

        SetCodingAiState(activityText, PlayerStatusColors.Warning,
            $"Schritt 3 von 4: SAM-Masken ({mmResult.DinoDetections.Count} Befunde)", pulse: true);

        var segmented = BuildCodingSegmentedFindings(mmResult);
        var findingSummary = CodingMultiModelFindingSummary.Build(segmented, mmResult);

        ShowMultiModelResults(mmResult, segmented);

        if (findingSummary.HasNoSegmentedFindings)
        {
            SetCodingAiState("SAM ohne Maske - Befund nicht segmentiert",
                PlayerStatusColors.Warning,
                mmResult.SamResponse?.Degraded == true
                    ? $"SAM degraded ({mmResult.SamResponse.SkippedBoxes} Box(en) verloren)"
                    : "keine Maske erzeugt");
            return;
        }

        if (findingSummary.HasOnlyAheadFindings)
        {
            SetCodingAiState("Ereignis voraus erkannt - naeher heranfahren",
                PlayerStatusColors.Warning,
                $"{findingSummary.VorausCount} voraus");
            return;
        }

        SetCodingAiState(
            findingSummary.DetectedStatusText,
            PlayerStatusColors.Success,
            findingSummary.TimingText);

        AddMultiModelFindingsAsEvents(
            findingSummary.VisibleCodierbar,
            mmResult.SamResponse?.ImageWidth ?? 1,
            mmResult.SamResponse?.ImageHeight ?? 1,
            mmResult.YoloMaxConfidence,
            captureTimestampSec,
            frameOsdMeter);
    }
}
