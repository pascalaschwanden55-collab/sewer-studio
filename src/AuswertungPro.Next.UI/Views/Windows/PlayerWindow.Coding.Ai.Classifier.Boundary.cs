using System;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private bool TryHandleBoundaryClassifierResult(
        SingleFrameResult mmResult,
        double captureTimestampSec,
        double? frameOsdMeter)
    {
        var result = CodingBoundaryClassifierCommandWorkflow.Execute(
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
                AnalyzedFrameBytes: _detectionConfirmationBuffer.FrameBytes),
            new CodingBoundaryClassifierCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                    ResolveCodingMeterForFrame(timestamp, osdMeter),
                ExecuteResultWorkflow: request => CodingBoundaryClassifierResultWorkflow.Execute(
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
                        (startMeter, startTime, frameBytes) =>
                        {
                            var anyAdded = false;
                            EnsureRohranfangExists(startMeter, startTime, frameBytes, ref anyAdded);
                            return anyAdded;
                        },
                        CloseTrackedStreckenschaeden,
                        (meterEnd, endTime, frameBytes) => EnsureRohrendeExists(meterEnd, endTime, frameBytes),
                        () => _codingSessionHost.EventCollection?.Count ?? 0,
                        (status, color, detail) => SetCodingAiState(status, color, detail)))));
        return result.Handled;
    }
}
