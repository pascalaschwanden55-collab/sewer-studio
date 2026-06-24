using System.Threading.Tasks;
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

        var start = await CodingMultiModelAnalysisStartWorkflow.ExecuteAsync(
            new CodingMultiModelAnalysisStartWorkflowRequest(
                activityText,
                captureTimestampSec,
                analysisCts.Token),
            new CodingMultiModelAnalysisStartWorkflowActions(
                SetCodingAiState: SetCodingAiState,
                CaptureSnapshotAsync: CaptureSnapshotAsync,
                StoreAnalyzedFrame: (frameBytes, timestamp) => _detectionConfirmationBuffer.StoreAnalyzedFrame(
                    frameBytes,
                    timestamp),
                TryReadAnalyzedFrameOsdMeterAsync: TryReadAnalyzedFrameOsdMeterAsync,
                UpdateFrameReadiness: UpdateFrameReadiness,
                IsFrameReady: IsFrameReady));
        if (start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready)
            return;

        var pngBytes = start.FrameBytes!;
        var frameOsdMeter = start.FrameOsdMeter;

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
