using System.Threading;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingMultiModelAnalysisStartWorkflowOutcome
{
    NoSnapshot,
    FrameNotReady,
    Ready
}

public sealed record CodingMultiModelAnalysisStartWorkflowRequest(
    string ActivityText,
    double CaptureTimestampSeconds,
    CancellationToken CancellationToken);

public sealed record CodingMultiModelAnalysisStartWorkflowActions(
    Action<string, Color, string?, bool> SetCodingAiState,
    Func<CancellationToken, Task<byte[]?>> CaptureSnapshotAsync,
    Action<byte[], double> StoreAnalyzedFrame,
    Func<byte[], double, CancellationToken, Task<double?>> TryReadAnalyzedFrameOsdMeterAsync,
    Action<LiveDetection> UpdateFrameReadiness,
    Func<bool> IsFrameReady);

public sealed record CodingMultiModelAnalysisStartWorkflowResult(
    CodingMultiModelAnalysisStartWorkflowOutcome Outcome,
    byte[]? FrameBytes,
    double? FrameOsdMeter);

public static class CodingMultiModelAnalysisStartWorkflow
{
    public static async Task<CodingMultiModelAnalysisStartWorkflowResult> ExecuteAsync(
        CodingMultiModelAnalysisStartWorkflowRequest request,
        CodingMultiModelAnalysisStartWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetCodingAiState(
            request.ActivityText,
            PlayerStatusColors.Warning,
            "Schritt 1 von 4: Snapshot",
            true);

        var pngBytes = await actions.CaptureSnapshotAsync(request.CancellationToken);
        if (pngBytes == null || pngBytes.Length == 0)
        {
            actions.SetCodingAiState(
                "Frame nicht extrahierbar",
                PlayerStatusColors.Error,
                "Multi-Model",
                false);
            return new CodingMultiModelAnalysisStartWorkflowResult(
                CodingMultiModelAnalysisStartWorkflowOutcome.NoSnapshot,
                FrameBytes: null,
                FrameOsdMeter: null);
        }

        actions.StoreAnalyzedFrame(pngBytes, request.CaptureTimestampSeconds);
        var frameOsdMeter = await actions.TryReadAnalyzedFrameOsdMeterAsync(
            pngBytes,
            request.CaptureTimestampSeconds,
            request.CancellationToken);

        actions.UpdateFrameReadiness(new LiveDetection(
            request.CaptureTimestampSeconds,
            Array.Empty<LiveFrameFinding>(),
            frameOsdMeter,
            null));
        if (!actions.IsFrameReady())
        {
            actions.SetCodingAiState(
                "Dateneinblendung erkannt - uebersprungen",
                PlayerStatusColors.Muted,
                "Warte auf sauberes Videobild...",
                false);
            return new CodingMultiModelAnalysisStartWorkflowResult(
                CodingMultiModelAnalysisStartWorkflowOutcome.FrameNotReady,
                pngBytes,
                frameOsdMeter);
        }

        actions.SetCodingAiState(
            request.ActivityText,
            PlayerStatusColors.Warning,
            "Schritt 2 von 4: YOLO und DINO",
            true);

        return new CodingMultiModelAnalysisStartWorkflowResult(
            CodingMultiModelAnalysisStartWorkflowOutcome.Ready,
            pngBytes,
            frameOsdMeter);
    }
}
