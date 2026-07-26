using System.Threading;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingSingleModelAnalysisWorkflowOutcome
{
    NoSnapshot,
    ResultShown
}

public sealed record CodingSingleModelAnalysisWorkflowRequest(
    string ActivityText,
    string? ModelName,
    double CaptureTimestampSeconds,
    bool HasEnhancedVision,
    CancellationToken CancellationToken);

public sealed record CodingSingleModelAnalysisWorkflowActions(
    Action<string, Color, string?, bool> SetCodingAiState,
    Func<CancellationToken, Task<byte[]?>> CaptureSnapshotAsync,
    Action<byte[], double> StoreAnalyzedFrame,
    Func<byte[], double, CancellationToken, Task<double?>> TryReadAnalyzedFrameOsdMeterAsync,
    Func<byte[], double, CancellationToken, Task<LiveDetection>> AnalyzeEnhancedVisionAsync,
    Func<byte[], double, CancellationToken, Task<LiveDetection>> AnalyzeLiveDetectionAsync,
    Action<LiveDetection> ShowCodingAiResults);

public static class CodingSingleModelAnalysisWorkflow
{
    public static async Task<CodingSingleModelAnalysisWorkflowOutcome> ExecuteAsync(
        CodingSingleModelAnalysisWorkflowRequest request,
        CodingSingleModelAnalysisWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetCodingAiState(
            request.ActivityText,
            PlayerStatusColors.Warning,
            "Schritt 1 von 3: Snapshot",
            true);

        var pngBytes = await actions.CaptureSnapshotAsync(request.CancellationToken);
        if (pngBytes == null || pngBytes.Length == 0)
        {
            actions.SetCodingAiState(
                "Frame nicht extrahierbar",
                PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(request.ModelName)}",
                false);
            return CodingSingleModelAnalysisWorkflowOutcome.NoSnapshot;
        }

        actions.StoreAnalyzedFrame(pngBytes, request.CaptureTimestampSeconds);
        var frameOsdMeter = await actions.TryReadAnalyzedFrameOsdMeterAsync(
            pngBytes,
            request.CaptureTimestampSeconds,
            request.CancellationToken);

        actions.SetCodingAiState(
            request.ActivityText,
            PlayerStatusColors.Warning,
            $"Schritt 2 von 3: Inferenz ({LiveDetectionDisplayPolicy.CompactModelName(request.ModelName)})",
            true);

        var result = request.HasEnhancedVision
            ? await actions.AnalyzeEnhancedVisionAsync(pngBytes, request.CaptureTimestampSeconds, request.CancellationToken)
            : await actions.AnalyzeLiveDetectionAsync(pngBytes, request.CaptureTimestampSeconds, request.CancellationToken);

        actions.ShowCodingAiResults(result with { MeterReading = frameOsdMeter });
        return CodingSingleModelAnalysisWorkflowOutcome.ResultShown;
    }
}
