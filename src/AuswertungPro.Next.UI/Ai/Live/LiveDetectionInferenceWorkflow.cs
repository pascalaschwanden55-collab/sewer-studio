using System.Threading;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionInferenceWorkflowOutcome
{
    Skipped,
    Completed
}

public sealed record LiveDetectionInferenceWorkflowRequest(
    byte[] Snapshot,
    double TimestampSeconds,
    bool IsClosing,
    bool IsPlaybackDisposed,
    string ModelName,
    CancellationToken? CancellationToken);

public sealed record LiveDetectionInferenceWorkflowActions(
    Func<byte[], double, CancellationToken, Task<LiveDetection>>? AnalyzeFrameAsync,
    Action<string, Color, string?> SetLiveDetectionBadge);

public sealed record LiveDetectionInferenceWorkflowResult(
    LiveDetectionInferenceWorkflowOutcome Outcome,
    LiveDetection? Result)
{
    public bool HasResult => Outcome == LiveDetectionInferenceWorkflowOutcome.Completed;
}

public static class LiveDetectionInferenceWorkflow
{
    public static async Task<LiveDetectionInferenceWorkflowResult> ExecuteAsync(
        LiveDetectionInferenceWorkflowRequest request,
        LiveDetectionInferenceWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Snapshot);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.SetLiveDetectionBadge);

        var cancellation = request.CancellationToken;
        if (request.IsClosing
            || request.IsPlaybackDisposed
            || actions.AnalyzeFrameAsync is null
            || cancellation is null)
        {
            return new LiveDetectionInferenceWorkflowResult(
                LiveDetectionInferenceWorkflowOutcome.Skipped,
                null);
        }

        actions.SetLiveDetectionBadge(
            "KI aktiv",
            PlayerStatusColors.Warning,
            $"{LiveDetectionDisplayPolicy.CompactModelName(request.ModelName)} | Inferenz");

        var result = await actions.AnalyzeFrameAsync(
            request.Snapshot,
            request.TimestampSeconds,
            cancellation.Value).ConfigureAwait(false);

        return new LiveDetectionInferenceWorkflowResult(
            LiveDetectionInferenceWorkflowOutcome.Completed,
            result);
    }
}
