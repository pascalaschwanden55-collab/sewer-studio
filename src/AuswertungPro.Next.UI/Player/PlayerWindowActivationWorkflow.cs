namespace AuswertungPro.Next.UI.Player;

public enum PlayerWindowActivationWorkflowOutcome
{
    Idle,
    Deactivated,
    Activated
}

public sealed record PlayerWindowDeactivationRequest(
    int CodingOverlaySuspendDepth);

public sealed record PlayerWindowActivationRequest(
    bool WasDeactivatedByExternalWindow);

public sealed record PlayerWindowActivationWorkflowActions(
    Action<bool> SetDeactivatedByExternalWindow,
    Action HideCodingOverlayForExternalWindow,
    Action RestoreCodingOverlayAfterExternalWindow);

public sealed record PlayerWindowActivationWorkflowResult(
    PlayerWindowActivationWorkflowOutcome Outcome)
{
    public bool Handled => Outcome != PlayerWindowActivationWorkflowOutcome.Idle;
}

public static class PlayerWindowActivationWorkflow
{
    public static PlayerWindowActivationWorkflowResult Deactivate(
        PlayerWindowDeactivationRequest request,
        PlayerWindowActivationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.CodingOverlaySuspendDepth > 0)
            return new PlayerWindowActivationWorkflowResult(PlayerWindowActivationWorkflowOutcome.Idle);

        actions.SetDeactivatedByExternalWindow(true);
        actions.HideCodingOverlayForExternalWindow();
        return new PlayerWindowActivationWorkflowResult(PlayerWindowActivationWorkflowOutcome.Deactivated);
    }

    public static PlayerWindowActivationWorkflowResult Activate(
        PlayerWindowActivationRequest request,
        PlayerWindowActivationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.WasDeactivatedByExternalWindow)
            return new PlayerWindowActivationWorkflowResult(PlayerWindowActivationWorkflowOutcome.Idle);

        actions.SetDeactivatedByExternalWindow(false);
        actions.RestoreCodingOverlayAfterExternalWindow();
        return new PlayerWindowActivationWorkflowResult(PlayerWindowActivationWorkflowOutcome.Activated);
    }
}
