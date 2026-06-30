using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task RunCodingMultiModelAnalysisAsync(string activityText, double captureTimestampSec)
    {
        await CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync(
            new CodingMultiModelAnalysisCommandRequest<Infrastructure.Ai.Pipeline.SingleFrameMultiModelService>(
                _codingAiRuntimeOwner.Controller.MultiModel,
                _codingAiRuntimeOwner.Controller.AnalysisCancellation),
            new CodingMultiModelAnalysisCommandActions<Infrastructure.Ai.Pipeline.SingleFrameMultiModelService>(
                StartAnalysisAsync: cancellationToken => CodingMultiModelAnalysisStartWorkflow.ExecuteAsync(
                    new CodingMultiModelAnalysisStartWorkflowRequest(
                        activityText,
                        captureTimestampSec,
                        cancellationToken),
                    new CodingMultiModelAnalysisStartWorkflowActions(
                        SetCodingAiState: SetCodingAiState,
                        CaptureSnapshotAsync: CaptureSnapshotAsync,
                        StoreAnalyzedFrame: (frameBytes, timestamp) => _liveDetectionController.StoreAnalyzedFrame(
                            frameBytes,
                            timestamp),
                        TryReadAnalyzedFrameOsdMeterAsync: TryReadAnalyzedFrameOsdMeterAsync,
                        UpdateFrameReadiness: UpdateFrameReadiness,
                        IsFrameReady: IsFrameReady)),
                ResolveEndMeter: () => CodingEndMeterResolveWorkflow.Execute(
                    new CodingEndMeterResolveRequest(_codingSessionHost.HasViewModel),
                    new CodingEndMeterResolveActions(
                        ResolveEndMeter: () => _codingSessionHost.EndMeter))
                    .EndMeter,
                RunInferenceAsync: (multiModel, start, endMeter, cancellationToken) => CodingMultiModelInferenceWorkflow.ExecuteAsync(
                    new CodingMultiModelInferenceWorkflowRequest(
                        activityText,
                        start.FrameBytes!,
                        captureTimestampSec,
                        start.FrameOsdMeter,
                        _codingOverlayToolHost.NominalDiameterMm,
                        endMeter,
                        cancellationToken),
                    new CodingMultiModelInferenceWorkflowActions(
                        ResolveCurrentMeter: ResolveCodingMeterForFrame,
                        AnalyzeFrameAsync: (frameBytes, classifierInput, inferenceCancellationToken) => multiModel.AnalyzeFrameAsync(
                            frameBytes,
                            classifierInput.NominalDiameterMm,
                            _codingOverlayToolHost.Calibration,
                            inferenceCancellationToken,
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
                                    start.FrameOsdMeter)))))));
    }
}
