namespace AuswertungPro.Next.UI.Player;

public enum PlayerWindowClosingWorkflowOutcome
{
    AlreadyClosing,
    Cancelled,
    Closed
}

public sealed record PlayerWindowClosingWorkflowRequest(
    bool AlreadyClosing);

public sealed record PlayerWindowClosingWorkflowActions(
    Func<bool> ConfirmCanClose,
    Action MarkClosing,
    Action ClearLastOpened,
    Action StopPlayerTimers,
    Action CancelQuickScan,
    Action CancelLiveDetection,
    Action CancelCodingAnalysis,
    Action StopLiveDetection,
    Action StopPipelineHealthMonitor,
    Action DetachVideoView,
    Action StopPlayer,
    Action Cleanup,
    Action<Exception> LogCleanupError);

public sealed record PlayerWindowClosingWorkflowResult(
    PlayerWindowClosingWorkflowOutcome Outcome)
{
    public bool CancelClose => Outcome == PlayerWindowClosingWorkflowOutcome.Cancelled;
}

public static class PlayerWindowClosingWorkflow
{
    public static PlayerWindowClosingWorkflowResult Execute(
        PlayerWindowClosingWorkflowRequest request,
        PlayerWindowClosingWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.AlreadyClosing)
            return new PlayerWindowClosingWorkflowResult(
                PlayerWindowClosingWorkflowOutcome.AlreadyClosing);

        if (!actions.ConfirmCanClose())
            return new PlayerWindowClosingWorkflowResult(
                PlayerWindowClosingWorkflowOutcome.Cancelled);

        actions.MarkClosing();
        actions.ClearLastOpened();
        actions.StopPlayerTimers();
        actions.CancelQuickScan();
        actions.CancelLiveDetection();
        actions.CancelCodingAnalysis();
        actions.StopLiveDetection();
        actions.StopPipelineHealthMonitor();
        actions.DetachVideoView();
        actions.StopPlayer();

        try
        {
            actions.Cleanup();
        }
        catch (Exception ex)
        {
            actions.LogCleanupError(ex);
        }

        return new PlayerWindowClosingWorkflowResult(
            PlayerWindowClosingWorkflowOutcome.Closed);
    }
}
