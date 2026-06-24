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
        if (!CodingBoundaryClassifierResultWorkflow.CanHandle(mmResult))
            return false;

        if (!_codingSessionHost.HasViewModel || _codingSessionService == null)
            return false;

        var videoTime = _codingSessionHost.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var eventCount = _codingSessionHost.EventCollection?.Count ?? 0;
        var result = CodingBoundaryClassifierResultWorkflow.Execute(
            new CodingBoundaryClassifierResultWorkflowRequest(
                mmResult,
                meter,
                _codingSessionHost.EndMeter,
                videoTime,
                eventCount,
                _detectionConfirmationBuffer.FrameBytes),
            new CodingBoundaryClassifierResultWorkflowActions(
                LookupVsaLabel,
                message => PlayerTrace.WriteLine(message),
                ClearDetectionOverlays,
                () => Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas),
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
                (status, color, detail) => SetCodingAiState(status, color, detail)));
        return result.Handled;
    }
}
