using System;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerLastOverlayDisplayWorkflowOutcome
{
    NoWindow,
    Shown
}

public sealed record PlayerLastOverlayDisplayWorkflowRequest(
    bool HasLastWindow);

public sealed record PlayerLastOverlayDisplayWorkflowActions(
    Action ShowOverlay);

public sealed record PlayerLastOverlayDisplayWorkflowResult(
    PlayerLastOverlayDisplayWorkflowOutcome Outcome)
{
    public bool Handled => Outcome == PlayerLastOverlayDisplayWorkflowOutcome.Shown;
}

public static class PlayerLastOverlayDisplayWorkflow
{
    public static PlayerLastOverlayDisplayWorkflowResult Show(
        PlayerLastOverlayDisplayWorkflowRequest request,
        PlayerLastOverlayDisplayWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasLastWindow)
            return Result(PlayerLastOverlayDisplayWorkflowOutcome.NoWindow);

        actions.ShowOverlay();
        return Result(PlayerLastOverlayDisplayWorkflowOutcome.Shown);
    }

    private static PlayerLastOverlayDisplayWorkflowResult Result(
        PlayerLastOverlayDisplayWorkflowOutcome outcome)
        => new(outcome);
}
