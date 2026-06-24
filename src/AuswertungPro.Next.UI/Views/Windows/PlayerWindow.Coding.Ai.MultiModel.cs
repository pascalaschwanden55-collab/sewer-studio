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

        var endMeter = _codingSessionHost.HasViewModel
            ? _codingSessionHost.EndMeter
            : (double?)null;

        await CodingMultiModelInferenceWorkflow.ExecuteAsync(
            new CodingMultiModelInferenceWorkflowRequest(
                activityText,
                start.FrameBytes!,
                captureTimestampSec,
                start.FrameOsdMeter,
                _codingOverlayService?.Calibration?.NominalDiameterMm,
                endMeter,
                analysisCts.Token),
            new CodingMultiModelInferenceWorkflowActions(
                ResolveCurrentMeter: ResolveCodingMeterForFrame,
                AnalyzeFrameAsync: (frameBytes, classifierInput, cancellationToken) => multiModel.AnalyzeFrameAsync(
                    frameBytes,
                    classifierInput.NominalDiameterMm,
                    _codingOverlayService?.Calibration,
                    cancellationToken,
                    classifierInput.CurrentMeter,
                    classifierInput.ReachLength),
                SetCodingAiState: SetCodingAiState,
                TryHandleBoundaryClassifierResult: TryHandleBoundaryClassifierResult,
                TryHandleStructuralClassifierResult: TryHandleStructuralClassifierResult,
                HandleAnalysisResult: result => CodingMultiModelAnalysisResultWorkflow.Execute(
                    new CodingMultiModelAnalysisResultWorkflowRequest(result, activityText),
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
                            start.FrameOsdMeter)))));
    }
}
