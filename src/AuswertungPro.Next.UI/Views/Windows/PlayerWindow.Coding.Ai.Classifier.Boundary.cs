using System;
using AuswertungPro.Next.Application.Ai;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task<bool> TryHandleBoundaryClassifierResultAsync(
        SingleFrameResult mmResult,
        CodingAnalyzedFrameEvidence frame)
    {
        var result = await CodingBoundaryClassifierCommandWorkflow.ExecuteAsync(
            new CodingBoundaryClassifierCommandRequest(
                Result: mmResult,
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                HasCodingSessionService: _codingSessionRuntimeOwner.Service is not null,
                CaptureTimestampSeconds: frame.CaptureTime.TotalSeconds,
                FrameOsdMeter: frame.HasSameFrameOsd ? frame.Meter : null,
                CurrentVideoTime: frame.CaptureTime,
                FallbackVideoTime: frame.CaptureTime,
                EndMeter: _codingSessionHost.EndMeter,
                ExistingEventCount: _codingSessionHost.EventCollection?.Count ?? 0,
                AnalyzedFrameBytes: frame.ImageBytes),
            new CodingBoundaryClassifierCommandActions(
                ResolveMeterForFrame: (_, _) => frame.Meter,
                ExecuteResultWorkflowAsync: request => CodingBoundaryClassifierResultWorkflow.ExecuteAsync(
                    request,
                    new CodingBoundaryClassifierResultWorkflowActions(
                        _codingFindingContext.LookupLabel,
                        message => PlayerTrace.WriteLine(message),
                        ClearDetectionOverlays,
                        () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
                        (code, label) => CodingFindingsListControls.ShowPossibleBoundary(
                            CodingFindingsList,
                            code,
                            label),
                        (code, label) => CodingFindingsListControls.ShowBoundary(
                            CodingFindingsList,
                            code,
                            label),
                        (_, _, _) => _codingBoundaryContext.EnsureStartAsync(frame),
                        _codingStreckenschadenTrackingController.CloseTracked,
                        (_, _, _) => _codingBoundaryContext.EnsureEnd(frame),
                        () => _codingSessionHost.EventCollection?.Count ?? 0,
                        (status, color, detail) => _liveDetectionStatusController.SetCodingAiState(status, color, detail)))));
        return result.Handled;
    }
}
