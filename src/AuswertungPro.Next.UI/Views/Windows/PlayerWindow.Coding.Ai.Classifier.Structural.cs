using System;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private bool TryHandleStructuralClassifierResult(
        SingleFrameResult mmResult,
        double captureTimestampSec,
        double? frameOsdMeter)
    {
        var codingSessionService = _codingSessionRuntimeOwner.Service;
        var viewEvents = _codingSessionHost.EventCollection;
        if (viewEvents == null || codingSessionService == null)
            return false;

        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var videoTime = _codingSessionHost.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var result = CodingStructuralClassifierResultWorkflow.Execute(
            new CodingStructuralClassifierResultWorkflowRequest(
                mmResult,
                meter,
                videoTime,
                viewEvents,
                codingSessionService,
                _codingOsdMeterController.LastResolvedMeterIsOsd),
            new CodingStructuralClassifierResultWorkflowActions(
                LookupVsaLabel,
                ResolveFindingCodeForCoding,
                ClearDetectionOverlays,
                () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
                (finding, resolvedCode) => CodingFindingsListControls.ShowResolvedFinding(
                    CodingFindingsList,
                    finding,
                    resolvedCode),
                entry => AttachAnalyzedFramePhoto(entry),
                RefreshCodingEventsList,
                (status, color, detail) => SetCodingAiState(status, color, detail)));
        return result.Handled;
    }
}
