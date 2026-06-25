using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task RunCodingMultiModelAnalysisAsync(string activityText, double captureTimestampSec)
    {
        var runtimeGate = CodingMultiModelRuntimeGateWorkflow.Execute(
            new CodingMultiModelRuntimeGateWorkflowRequest<Infrastructure.Ai.Pipeline.SingleFrameMultiModelService>(
                _codingAiRuntimeOwner.Controller.MultiModel,
                _codingAiRuntimeOwner.Controller.AnalysisCancellation));
        if (!runtimeGate.Ready)
            return;

        var multiModel = runtimeGate.MultiModel!;
        var analysisCts = runtimeGate.AnalysisCancellation!;

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

        var endMeter = CodingEndMeterResolveWorkflow.Execute(
            new CodingEndMeterResolveRequest(_codingSessionHost.HasViewModel),
            new CodingEndMeterResolveActions(
                ResolveEndMeter: () => _codingSessionHost.EndMeter))
            .EndMeter;

        await CodingMultiModelInferenceWorkflow.ExecuteAsync(
            new CodingMultiModelInferenceWorkflowRequest(
                activityText,
                start.FrameBytes!,
                captureTimestampSec,
                start.FrameOsdMeter,
                _codingOverlayToolHost.NominalDiameterMm,
                endMeter,
                analysisCts.Token),
            new CodingMultiModelInferenceWorkflowActions(
                ResolveCurrentMeter: ResolveCodingMeterForFrame,
                AnalyzeFrameAsync: (frameBytes, classifierInput, cancellationToken) => multiModel.AnalyzeFrameAsync(
                    frameBytes,
                    classifierInput.NominalDiameterMm,
                    _codingOverlayToolHost.Calibration,
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
                        () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
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
