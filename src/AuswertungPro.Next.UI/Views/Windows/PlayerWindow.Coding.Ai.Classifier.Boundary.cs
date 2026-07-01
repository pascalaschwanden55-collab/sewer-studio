using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task<bool> TryHandleBoundaryClassifierResultAsync(
        SingleFrameResult mmResult,
        double captureTimestampSec,
        double? frameOsdMeter)
    {
        var result = await CodingBoundaryClassifierCommandWorkflow.ExecuteAsync(
            new CodingBoundaryClassifierCommandRequest(
                Result: mmResult,
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                HasCodingSessionService: _codingSessionRuntimeOwner.Service is not null,
                CaptureTimestampSeconds: captureTimestampSec,
                FrameOsdMeter: frameOsdMeter,
                CurrentVideoTime: _codingSessionHost.CurrentVideoTime,
                FallbackVideoTime: TimeSpan.FromSeconds(captureTimestampSec),
                EndMeter: _codingSessionHost.EndMeter,
                ExistingEventCount: _codingSessionHost.EventCollection?.Count ?? 0,
                AnalyzedFrameBytes: _liveDetectionController.PendingConfirmationFrameBytes),
            new CodingBoundaryClassifierCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                    ResolveCodingMeterForFrame(timestamp, osdMeter),
                ExecuteResultWorkflowAsync: request => CodingBoundaryClassifierResultWorkflow.ExecuteAsync(
                    request,
                    new CodingBoundaryClassifierResultWorkflowActions(
                        LookupVsaLabel,
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
                        EnsureRohranfangExistsAsync,
                        CloseTrackedStreckenschaeden,
                        (meterEnd, endTime, frameBytes) => EnsureRohrendeExists(meterEnd, endTime, frameBytes),
                        () => _codingSessionHost.EventCollection?.Count ?? 0,
                        (status, color, detail) => SetCodingAiState(status, color, detail)))));
        return result.Handled;
    }
}
