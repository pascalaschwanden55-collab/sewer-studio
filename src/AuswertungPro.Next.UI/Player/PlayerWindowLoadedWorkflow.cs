namespace AuswertungPro.Next.UI.Player;

public enum PlayerWindowLoadedWorkflowOutcome
{
    Loaded
}

public sealed record PlayerWindowLoadedWorkflowRequest(
    string? InitialOverlayText,
    TimeSpan InitialOverlayDuration);

public sealed record PlayerWindowLoadedWorkflowActions(
    Action Play,
    Action UpdateCodingOverlayViewport,
    Action ScheduleLoadedViewportUpdate,
    Action<string, TimeSpan> ShowOverlay,
    Action BuildDamageMarkerTimeline,
    Action EnableFocusable,
    Action ScheduleFocusWindow);

public sealed record PlayerWindowLoadedWorkflowResult(
    PlayerWindowLoadedWorkflowOutcome Outcome,
    bool ShowedInitialOverlay);

public static class PlayerWindowLoadedWorkflow
{
    public static PlayerWindowLoadedWorkflowResult Execute(
        PlayerWindowLoadedWorkflowRequest request,
        PlayerWindowLoadedWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.Play();
        actions.UpdateCodingOverlayViewport();
        actions.ScheduleLoadedViewportUpdate();

        var showedInitialOverlay = !string.IsNullOrWhiteSpace(request.InitialOverlayText);
        if (showedInitialOverlay)
            actions.ShowOverlay(request.InitialOverlayText!, request.InitialOverlayDuration);

        actions.BuildDamageMarkerTimeline();
        actions.EnableFocusable();
        actions.ScheduleFocusWindow();

        return new PlayerWindowLoadedWorkflowResult(
            PlayerWindowLoadedWorkflowOutcome.Loaded,
            showedInitialOverlay);
    }
}
