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
        var result = CodingStructuralClassifierCommandWorkflow.Execute(
            new CodingStructuralClassifierCommandRequest(
                Result: mmResult,
                CaptureTimestampSeconds: captureTimestampSec,
                FrameOsdMeter: frameOsdMeter,
                CurrentVideoTime: _codingSessionHost.CurrentVideoTime,
                FallbackVideoTime: TimeSpan.FromSeconds(captureTimestampSec),
                ViewEvents: _codingSessionHost.EventCollection,
                CodingSessionService: _codingSessionRuntimeOwner.Service,
                MeterFromOsd: _codingOsdMeterController.LastResolvedMeterIsOsd),
            new CodingStructuralClassifierCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                    ResolveCodingMeterForFrame(timestamp, osdMeter),
                ExecuteResultWorkflow: request => CodingStructuralClassifierResultWorkflow.Execute(
                    request,
                    new CodingStructuralClassifierResultWorkflowActions(
                        _codingFindingContext.LookupLabel,
                        _codingFindingContext.ResolveCode,
                        ClearDetectionOverlays,
                        () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
                        (finding, resolvedCode) => CodingFindingsListControls.ShowResolvedFinding(
                            CodingFindingsList,
                            finding,
                            resolvedCode),
                        entry => AttachAnalyzedFramePhoto(entry),
                        RefreshCodingEventsList,
                        (status, color, detail) => _liveDetectionStatusController.SetCodingAiState(status, color, detail)))));
        return result.Handled;
    }
}
