using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private bool TryHandleStructuralClassifierResult(
        SingleFrameResult mmResult,
        CodingAnalyzedFrameEvidence frame)
    {
        var result = CodingStructuralClassifierCommandWorkflow.ExecuteAnalyzedFrame(
            new CodingStructuralClassifierCommandRequest(
                Result: mmResult,
                CaptureTimestampSeconds: frame.CaptureTime.TotalSeconds,
                FrameOsdMeter: frame.HasSameFrameOsd ? frame.Meter : null,
                CurrentVideoTime: frame.CaptureTime,
                FallbackVideoTime: frame.CaptureTime,
                ViewEvents: _codingSessionHost.EventCollection,
                CodingSessionService: _codingSessionRuntimeOwner.Service,
                MeterFromOsd: frame.MeterFromOsd), frame,
            new CodingStructuralClassifierCommandActions(
                ResolveMeterForFrame: (_, _) => frame.Meter,
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
                        entry => frame.AttachPhoto(entry, (e, bytes) => _codingPhotoAttachmentController.AttachExactAnalyzedFramePhoto(e, bytes)),
                        RefreshCodingEventsList,
                        (status, color, detail) => _liveDetectionStatusController.SetCodingAiState(status, color, detail)))));
        return result.Handled;
    }
}
