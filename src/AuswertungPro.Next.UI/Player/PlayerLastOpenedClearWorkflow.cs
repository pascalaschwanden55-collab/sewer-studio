using System;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerLastOpenedClearOutcome
{
    NotLastOpened,
    Cleared
}

public sealed record PlayerLastOpenedClearRequest(
    bool IsLastOpenedWindow);

public sealed record PlayerLastOpenedClearActions(
    Action ClearLastOpened);

public sealed record PlayerLastOpenedClearResult(
    PlayerLastOpenedClearOutcome Outcome)
{
    public bool Cleared => Outcome == PlayerLastOpenedClearOutcome.Cleared;
}

public static class PlayerLastOpenedClearWorkflow
{
    public static PlayerLastOpenedClearResult Execute(
        PlayerLastOpenedClearRequest request,
        PlayerLastOpenedClearActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsLastOpenedWindow)
            return Result(PlayerLastOpenedClearOutcome.NotLastOpened);

        actions.ClearLastOpened();
        return Result(PlayerLastOpenedClearOutcome.Cleared);
    }

    private static PlayerLastOpenedClearResult Result(
        PlayerLastOpenedClearOutcome outcome)
        => new(outcome);
}
