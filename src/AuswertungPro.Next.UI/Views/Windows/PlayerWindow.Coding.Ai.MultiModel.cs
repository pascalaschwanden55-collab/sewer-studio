using System;
using System.Threading.Tasks;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task RunCodingMultiModelAnalysisAsync(string activityText, double captureTimestampSec)
    {
        var multiModel = _codingMultiModel;
        if (multiModel == null || _codingAnalysisCts == null)
            return;

        SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
            "Schritt 1 von 4: Snapshot", pulse: true);

        var pngBytes = await CaptureSnapshotAsync(_codingAnalysisCts.Token);
        if (pngBytes == null || pngBytes.Length == 0)
        {
            SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44),
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
                Color.FromRgb(0x94, 0xA3, 0xB8), "Warte auf sauberes Videobild...");
            return;
        }

        SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
            "Schritt 2 von 4: YOLO und DINO", pulse: true);

        var dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;
        var currentMeterForClassifier = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var reachLengthForClassifier = _codingVm?.EndMeter > 0
            ? _codingVm.EndMeter
            : Math.Max(currentMeterForClassifier, 1);

        var mmResult = await multiModel.AnalyzeFrameAsync(
            pngBytes, dn, _codingOverlayService?.Calibration,
            _codingAnalysisCts.Token,
            currentMeterForClassifier,
            reachLengthForClassifier);

        if (mmResult.Error != null)
        {
            SetCodingAiState($"Fehler: {mmResult.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                "Multi-Model");
            return;
        }

        if (TryHandleBoundaryClassifierResult(mmResult, captureTimestampSec, frameOsdMeter))
            return;

        if (TryHandleStructuralClassifierResult(mmResult, captureTimestampSec, frameOsdMeter))
            return;

        if (!mmResult.IsRelevant || !mmResult.HasDetections)
        {
            SetCodingAiState("Kein Schaden erkannt", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"YOLO {mmResult.YoloTimeMs:F0}ms | {mmResult.DinoDetections.Count} Detektionen");
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            return;
        }

        SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
            $"Schritt 3 von 4: SAM-Masken ({mmResult.DinoDetections.Count} Befunde)", pulse: true);

        var segmented = BuildCodingSegmentedFindings(mmResult);
        var findingSummary = CodingMultiModelFindingSummary.Build(segmented, mmResult);

        ShowMultiModelResults(mmResult, segmented);

        if (findingSummary.HasNoSegmentedFindings)
        {
            SetCodingAiState("SAM ohne Maske - Befund nicht segmentiert",
                Color.FromRgb(0xF5, 0x9E, 0x0B),
                mmResult.SamResponse?.Degraded == true
                    ? $"SAM degraded ({mmResult.SamResponse.SkippedBoxes} Box(en) verloren)"
                    : "keine Maske erzeugt");
            return;
        }

        if (findingSummary.HasOnlyAheadFindings)
        {
            SetCodingAiState("Ereignis voraus erkannt - naeher heranfahren",
                Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"{findingSummary.VorausCount} voraus");
            return;
        }

        SetCodingAiState(
            findingSummary.DetectedStatusText,
            Color.FromRgb(0x22, 0xC5, 0x5E),
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
