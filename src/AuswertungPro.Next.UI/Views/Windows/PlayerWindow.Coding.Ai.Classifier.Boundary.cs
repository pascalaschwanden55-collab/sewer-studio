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

        var codingVm = _codingVm;
        if (codingVm == null || _codingSessionService == null)
            return false;

        var videoTime = codingVm.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var result = CodingBoundaryClassifierResultWorkflow.Execute(
            new CodingBoundaryClassifierResultWorkflowRequest(
                mmResult,
                meter,
                codingVm.EndMeter,
                videoTime,
                codingVm.Events.Count,
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
                () => codingVm.Events.Count,
                (status, color, detail) => SetCodingAiState(status, color, detail)));
        return result.Handled;
    }
}
