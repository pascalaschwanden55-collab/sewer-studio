using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionSnapshotWorkflowOutcome
{
    Available,
    Missing
}

public sealed record LiveDetectionSnapshotWorkflowRequest(
    byte[]? Snapshot,
    bool IsClosing,
    bool IsPlaybackDisposed,
    string ModelName);

public sealed record LiveDetectionSnapshotWorkflowActions(
    Action EndDetection,
    Action<string, Color, string?> SetLiveDetectionBadge);

public sealed record LiveDetectionSnapshotWorkflowResult(
    LiveDetectionSnapshotWorkflowOutcome Outcome,
    byte[]? Snapshot)
{
    public bool HasSnapshot => Outcome == LiveDetectionSnapshotWorkflowOutcome.Available;
}

public static class LiveDetectionSnapshotWorkflow
{
    public static LiveDetectionSnapshotWorkflowResult Handle(
        LiveDetectionSnapshotWorkflowRequest request,
        LiveDetectionSnapshotWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.Snapshot is not null)
            return new LiveDetectionSnapshotWorkflowResult(
                LiveDetectionSnapshotWorkflowOutcome.Available,
                request.Snapshot);

        actions.EndDetection();
        if (!request.IsClosing && !request.IsPlaybackDisposed)
        {
            actions.SetLiveDetectionBadge(
                "KI aktiv",
                PlayerStatusColors.Success,
                $"{LiveDetectionDisplayPolicy.CompactModelName(request.ModelName)} | Bereit");
        }

        return new LiveDetectionSnapshotWorkflowResult(
            LiveDetectionSnapshotWorkflowOutcome.Missing,
            null);
    }
}
