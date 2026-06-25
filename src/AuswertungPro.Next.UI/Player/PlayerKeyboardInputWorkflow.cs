using System;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerKeyboardInputWorkflowOutcome
{
    Unhandled,
    Handled
}

public sealed record PlayerKeyboardInputWorkflowRequest(
    PlayerKeyboardAction? Action);

public sealed record PlayerKeyboardInputWorkflowActions(
    Func<PlayerKeyboardAction?, bool> ExecuteAction,
    Action MarkHandled);

public sealed record PlayerKeyboardInputWorkflowResult(
    PlayerKeyboardInputWorkflowOutcome Outcome)
{
    public bool Handled => Outcome == PlayerKeyboardInputWorkflowOutcome.Handled;
}

public static class PlayerKeyboardInputWorkflow
{
    public static PlayerKeyboardInputWorkflowResult Execute(
        PlayerKeyboardInputWorkflowRequest request,
        PlayerKeyboardInputWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!actions.ExecuteAction(request.Action))
            return Result(PlayerKeyboardInputWorkflowOutcome.Unhandled);

        actions.MarkHandled();
        return Result(PlayerKeyboardInputWorkflowOutcome.Handled);
    }

    private static PlayerKeyboardInputWorkflowResult Result(
        PlayerKeyboardInputWorkflowOutcome outcome)
        => new(outcome);
}
