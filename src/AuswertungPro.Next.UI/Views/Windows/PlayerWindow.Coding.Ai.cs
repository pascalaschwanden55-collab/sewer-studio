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
        await CodingAnalysisCommandWorkflow.ExecuteAsync(
            new CodingAnalysisCommandWorkflowRequest(
                activityText,
                disableAnalyzeButton,
                _codingAiRuntimeOwner.Controller.ModelName),
            new CodingAnalysisCommandWorkflowActions(
                TryBeginAnalysis: _codingAiRuntimeOwner.Controller.TryBeginAnalysis,
                GetAnalysisCancellationToken: () => _codingAiRuntimeOwner.Controller.AnalysisCancellation!.Token,
                RunPreflight: () => CodingAnalysisPreflightWorkflow.Execute(
                    new CodingAnalysisPreflightWorkflowRequest(
                        disableAnalyzeButton,
                        _codingAiRuntimeOwner.Controller.UseMultiModel,
                        _codingAiRuntimeOwner.Controller.MultiModel != null),
                    new CodingAnalysisPreflightWorkflowActions(
                        SetAnalyzeButtonEnabled: enabled => CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, enabled),
                        ResolveFramePosition: () =>
                        {
                            var timestamp = _playerTimelineHost.CurrentSecondsOrZero;
                            return new CodingAnalysisFramePosition(
                                timestamp,
                                ResolveCodingMeterForFrame(timestamp),
                                TimeSpan.FromSeconds(timestamp));
                        },
                        IsAfterTerminalBoundary: framePosition => _codingAnalysisContext.IsAfterTerminalBoundary(
                            framePosition.CurrentMeter,
                            framePosition.VideoTime),
                        ClearDetectionOverlays: ClearDetectionOverlays,
                        ClearSamMasks: () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
                        SetCodingAiState: (status, color, detail) => SetCodingAiState(status, color, detail))),
                RunSingleModelAnalysisAsync: (captureTimestampSec, cancellationToken) => CodingSingleModelAnalysisWorkflow.ExecuteAsync(
                    new CodingSingleModelAnalysisWorkflowRequest(
                        activityText,
                        _codingAiRuntimeOwner.Controller.ModelName,
                        captureTimestampSec,
                        _codingAiRuntimeOwner.Controller.EnhancedVision != null,
                        cancellationToken),
                    new CodingSingleModelAnalysisWorkflowActions(
                        SetCodingAiState: SetCodingAiState,
                        CaptureSnapshotAsync: _codingAnalysisContext.CaptureSnapshotAsync,
                        StoreAnalyzedFrame: (frameBytes, timestamp) => _liveDetectionController.StoreAnalyzedFrame(
                            frameBytes,
                            timestamp),
                        TryReadAnalyzedFrameOsdMeterAsync: TryReadAnalyzedFrameOsdMeterAsync,
                        AnalyzeEnhancedVisionAsync: async (frameBytes, timestamp, cancellationToken) =>
                        {
                            var b64 = Convert.ToBase64String(frameBytes);
                            var importContext = GatherImportContext();
                            var enhanced = await _codingAiRuntimeOwner.Controller.EnhancedVision!.AnalyzeAsync(
                                b64,
                                importContext,
                                cancellationToken);
                            return LiveDetectionMapper.FromEnhancedAnalysis(enhanced, timestamp);
                        },
                        AnalyzeLiveDetectionAsync: (frameBytes, timestamp, cancellationToken) =>
                            _codingAiRuntimeOwner.Controller.LiveDetection!.AnalyzeFrameAsync(
                                frameBytes,
                                timestamp,
                                cancellationToken),
                        ShowCodingAiResults: ShowCodingAiResults)),
                RunMultiModelAnalysisAsync: captureTimestampSec => RunCodingMultiModelAnalysisAsync(activityText, captureTimestampSec),
                SetCodingAiState: (status, color, detail) => SetCodingAiState(status, color, detail),
                EndAnalysis: _codingAiRuntimeOwner.Controller.EndAnalysis,
                SetAnalyzeButtonEnabled: enabled => CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, enabled)));
    }

}
