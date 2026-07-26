using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionResultWorkflowOutcome
{
    Ignored,
    OverlayShown,
    ConfirmationRequested
}

public sealed record LiveDetectionResultWorkflowRequest(
    LiveDetection Result,
    byte[] Snapshot,
    bool IsClosing,
    bool IsPlaybackDisposed,
    bool IsDetecting,
    string ModelName);

public sealed record LiveDetectionResultWorkflowActions(
    Action<LiveDetection> ApplyDetectionResult,
    Action<IReadOnlyList<LiveFrameFinding>, double> RenderDetectionOverlay,
    Action<LiveDetection> UpdateDetectionStatus,
    Action<string, Color, string?> SetLiveDetectionBadge,
    Action<IReadOnlyList<LiveFrameFinding>, byte[], double> StoreFindings,
    Action<IReadOnlyList<LiveFrameFinding>> ShowDetectionConfirmation);

public sealed record LiveDetectionResultWorkflowResult(
    LiveDetectionResultWorkflowOutcome Outcome)
{
    public bool Handled => Outcome != LiveDetectionResultWorkflowOutcome.Ignored;
}

public static class LiveDetectionResultWorkflow
{
    public static LiveDetectionResultWorkflowResult Execute(
        LiveDetectionResultWorkflowRequest request,
        LiveDetectionResultWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(request.Snapshot);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsClosing || request.IsPlaybackDisposed || !request.IsDetecting)
            return new LiveDetectionResultWorkflowResult(LiveDetectionResultWorkflowOutcome.Ignored);

        actions.ApplyDetectionResult(request.Result);
        actions.RenderDetectionOverlay(
            request.Result.Findings,
            request.Result.TimestampSeconds);
        actions.UpdateDetectionStatus(request.Result);

        var compactModelName = LiveDetectionDisplayPolicy.CompactModelName(request.ModelName);
        actions.SetLiveDetectionBadge(
            "KI aktiv",
            PlayerStatusColors.Success,
            $"{compactModelName} | Overlay");

        var significantFindings = LiveDetectionConfirmationPolicy.SelectSignificantFindings(
            request.Result.Findings);
        if (significantFindings.Count == 0)
            return new LiveDetectionResultWorkflowResult(LiveDetectionResultWorkflowOutcome.OverlayShown);

        actions.StoreFindings(
            significantFindings,
            request.Snapshot,
            request.Result.TimestampSeconds);
        actions.ShowDetectionConfirmation(significantFindings);
        actions.SetLiveDetectionBadge(
            "Befund erkannt",
            PlayerStatusColors.Warning,
            $"{compactModelName} | Warte auf Bestaetigung");

        return new LiveDetectionResultWorkflowResult(
            LiveDetectionResultWorkflowOutcome.ConfirmationRequested);
    }
}
