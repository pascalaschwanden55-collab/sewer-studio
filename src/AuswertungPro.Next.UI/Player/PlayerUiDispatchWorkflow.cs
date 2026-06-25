namespace AuswertungPro.Next.UI.Player;

public enum PlayerUiDispatchWorkflowOutcome
{
    Applied,
    Dispatched
}

public sealed record PlayerUiDispatchWorkflowRequest(
    bool HasDispatcherAccess);

public sealed record PlayerUiDispatchWorkflowActions(
    Action Apply,
    Action<Action> DispatchToUi);

public sealed record PlayerUiDispatchWorkflowResult(
    PlayerUiDispatchWorkflowOutcome Outcome);

public static class PlayerUiDispatchWorkflow
{
    public static PlayerUiDispatchWorkflowResult Execute(
        PlayerUiDispatchWorkflowRequest request,
        PlayerUiDispatchWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasDispatcherAccess)
        {
            actions.DispatchToUi(actions.Apply);
            return Result(PlayerUiDispatchWorkflowOutcome.Dispatched);
        }

        actions.Apply();
        return Result(PlayerUiDispatchWorkflowOutcome.Applied);
    }

    private static PlayerUiDispatchWorkflowResult Result(PlayerUiDispatchWorkflowOutcome outcome)
        => new(outcome);
}
