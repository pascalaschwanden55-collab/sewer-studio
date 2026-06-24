using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionErrorWorkflowOutcome
{
    Ignored,
    Shown
}

public sealed record LiveDetectionErrorWorkflowRequest(
    Exception Error,
    bool IsClosing,
    bool IsPlaybackDisposed,
    string ModelName);

public sealed record LiveDetectionErrorWorkflowActions(
    Action<string> ShowDetectionError,
    Action<string, Color, string?> SetLiveDetectionBadge);

public sealed record LiveDetectionErrorWorkflowResult(
    LiveDetectionErrorWorkflowOutcome Outcome)
{
    public bool Handled => Outcome != LiveDetectionErrorWorkflowOutcome.Ignored;
}

public static class LiveDetectionErrorWorkflow
{
    public static LiveDetectionErrorWorkflowResult Execute(
        LiveDetectionErrorWorkflowRequest request,
        LiveDetectionErrorWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Error);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsClosing || request.IsPlaybackDisposed)
            return new LiveDetectionErrorWorkflowResult(LiveDetectionErrorWorkflowOutcome.Ignored);

        var message = request.Error.Message;
        if (message.Length > 200)
            message = message[..200] + "...";

        actions.ShowDetectionError(message);
        actions.SetLiveDetectionBadge(
            "KI Fehler",
            PlayerStatusColors.Error,
            LiveDetectionDisplayPolicy.CompactModelName(request.ModelName));

        return new LiveDetectionErrorWorkflowResult(LiveDetectionErrorWorkflowOutcome.Shown);
    }
}
