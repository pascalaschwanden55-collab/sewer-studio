using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionTickStartWorkflowOutcome
{
    Skipped,
    Started
}

public sealed record LiveDetectionTickStartWorkflowRequest(
    bool ShouldRunTick,
    string ModelName);

public sealed record LiveDetectionTickStartWorkflowActions(
    Action BeginDetection,
    Action<string, Color, string?> SetLiveDetectionBadge);

public sealed record LiveDetectionTickStartWorkflowResult(
    LiveDetectionTickStartWorkflowOutcome Outcome)
{
    public bool Started => Outcome == LiveDetectionTickStartWorkflowOutcome.Started;
}

public static class LiveDetectionTickStartWorkflow
{
    public static LiveDetectionTickStartWorkflowResult Start(
        LiveDetectionTickStartWorkflowRequest request,
        LiveDetectionTickStartWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.ShouldRunTick)
            return new LiveDetectionTickStartWorkflowResult(
                LiveDetectionTickStartWorkflowOutcome.Skipped);

        actions.BeginDetection();
        actions.SetLiveDetectionBadge(
            "KI aktiv",
            PlayerStatusColors.Warning,
            $"{LiveDetectionDisplayPolicy.CompactModelName(request.ModelName)} | Snapshot");

        return new LiveDetectionTickStartWorkflowResult(
            LiveDetectionTickStartWorkflowOutcome.Started);
    }
}
