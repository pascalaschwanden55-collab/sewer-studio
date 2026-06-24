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
        var multiModel = _codingAiController.MultiModel;
        var analysisCts = _codingAiController.AnalysisCancellation;
        if (multiModel == null || analysisCts == null)
            return;

        SetCodingAiState(activityText, PlayerStatusColors.Warning,
            "Schritt 1 von 4: Snapshot", pulse: true);

        var pngBytes = await CaptureSnapshotAsync(analysisCts.Token);
        if (pngBytes == null || pngBytes.Length == 0)
        {
            SetCodingAiState("Frame nicht extrahierbar", PlayerStatusColors.Error,
                "Multi-Model");
            return;
        }

        _detectionConfirmationBuffer.StoreAnalyzedFrame(pngBytes, captureTimestampSec);
        var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
            pngBytes,
            captureTimestampSec,
            analysisCts.Token);

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
            analysisCts.Token,
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

        CodingMultiModelAnalysisResultWorkflow.Execute(
            new CodingMultiModelAnalysisResultWorkflowRequest(mmResult, activityText),
            new CodingMultiModelAnalysisResultWorkflowActions(
                SetCodingAiState,
                () => Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas),
                BuildCodingSegmentedFindings,
                ShowMultiModelResults,
                (findings, imageWidth, imageHeight, yoloMaxConfidence) => AddMultiModelFindingsAsEvents(
                    findings,
                    imageWidth,
                    imageHeight,
                    yoloMaxConfidence,
                    captureTimestampSec,
                    frameOsdMeter)));
    }
}
