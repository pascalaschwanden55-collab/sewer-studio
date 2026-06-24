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
        var codingVm = _codingVm;
        var codingSessionService = _codingSessionService;
        if (codingVm == null || codingSessionService == null)
            return false;

        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var videoTime = codingVm.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var result = CodingStructuralClassifierResultWorkflow.Execute(
            new CodingStructuralClassifierResultWorkflowRequest(
                mmResult,
                meter,
                videoTime,
                codingVm.Events,
                codingSessionService,
                _codingOsdMeterController.LastResolvedMeterIsOsd),
            new CodingStructuralClassifierResultWorkflowActions(
                LookupVsaLabel,
                ResolveFindingCodeForCoding,
                ClearDetectionOverlays,
                () => Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas),
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
