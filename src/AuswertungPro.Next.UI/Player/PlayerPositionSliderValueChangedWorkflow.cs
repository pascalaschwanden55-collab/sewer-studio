namespace AuswertungPro.Next.UI.Player;

public enum PlayerPositionSliderValueChangedWorkflowOutcome
{
    Idle,
    PreviewUpdated
}

public sealed record PlayerPositionSliderValueChangedWorkflowRequest(
    bool IsDragging);

public sealed record PlayerPositionSliderValueChangedWorkflowActions(
    Action UpdateSeekPreview);

public sealed record PlayerPositionSliderValueChangedWorkflowResult(
    PlayerPositionSliderValueChangedWorkflowOutcome Outcome)
{
    public bool Updated => Outcome == PlayerPositionSliderValueChangedWorkflowOutcome.PreviewUpdated;
}

public static class PlayerPositionSliderValueChangedWorkflow
{
    public static PlayerPositionSliderValueChangedWorkflowResult Execute(
        PlayerPositionSliderValueChangedWorkflowRequest request,
        PlayerPositionSliderValueChangedWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsDragging)
            return new PlayerPositionSliderValueChangedWorkflowResult(
                PlayerPositionSliderValueChangedWorkflowOutcome.Idle);

        actions.UpdateSeekPreview();
        return new PlayerPositionSliderValueChangedWorkflowResult(
            PlayerPositionSliderValueChangedWorkflowOutcome.PreviewUpdated);
    }
}
