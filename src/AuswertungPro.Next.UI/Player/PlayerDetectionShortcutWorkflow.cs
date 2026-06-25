namespace AuswertungPro.Next.UI.Player;

public enum PlayerDetectionShortcutWorkflowOutcome
{
    CodingLiveAiToggled,
    LiveDetectionToggled
}

public sealed record PlayerDetectionShortcutWorkflowRequest(
    bool IsCodingMode,
    bool IsCodingLiveAiChecked,
    bool IsLiveDetectionChecked);

public sealed record PlayerDetectionShortcutWorkflowActions(
    Action<bool> SetCodingLiveAiChecked,
    Action InvokeCodingLiveAi,
    Action<bool> SetLiveDetectionChecked,
    Action InvokeLiveDetection);

public sealed record PlayerDetectionShortcutWorkflowResult(
    PlayerDetectionShortcutWorkflowOutcome Outcome);

public static class PlayerDetectionShortcutWorkflow
{
    public static PlayerDetectionShortcutWorkflowResult Execute(
        PlayerDetectionShortcutWorkflowRequest request,
        PlayerDetectionShortcutWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsCodingMode)
        {
            actions.SetCodingLiveAiChecked(!request.IsCodingLiveAiChecked);
            actions.InvokeCodingLiveAi();
            return new PlayerDetectionShortcutWorkflowResult(
                PlayerDetectionShortcutWorkflowOutcome.CodingLiveAiToggled);
        }

        actions.SetLiveDetectionChecked(!request.IsLiveDetectionChecked);
        actions.InvokeLiveDetection();
        return new PlayerDetectionShortcutWorkflowResult(
            PlayerDetectionShortcutWorkflowOutcome.LiveDetectionToggled);
    }
}
