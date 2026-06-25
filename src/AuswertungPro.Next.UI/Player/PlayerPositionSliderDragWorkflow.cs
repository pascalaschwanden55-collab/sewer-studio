namespace AuswertungPro.Next.UI.Player;

public enum PlayerPositionSliderDragWorkflowOutcome
{
    Idle,
    Started,
    Completed,
    Seeked
}

public sealed record PlayerPositionSliderDragStartRequest(
    bool IsPlayerPlaying);

public sealed record PlayerPositionSliderDragCompleteRequest(
    bool WasPlayingBeforeDrag);

public sealed record PlayerPositionSliderDragPreviewMouseUpRequest(
    bool IsDragging);

public sealed record PlayerPositionSliderDragLostCaptureRequest(
    bool IsDragging,
    bool WasPlayingBeforeDrag);

public sealed record PlayerPositionSliderDragWorkflowActions(
    Action<bool> SetWasPlayingBeforeDrag,
    Action<bool> SetDragging,
    Action<bool> SetPause,
    Action StopScrubTimer,
    Action SeekToSlider,
    Action ScrubSeekToSlider);

public sealed record PlayerPositionSliderDragWorkflowResult(
    PlayerPositionSliderDragWorkflowOutcome Outcome)
{
    public bool Handled => Outcome != PlayerPositionSliderDragWorkflowOutcome.Idle;
}

public static class PlayerPositionSliderDragWorkflow
{
    public static PlayerPositionSliderDragWorkflowResult Start(
        PlayerPositionSliderDragStartRequest request,
        PlayerPositionSliderDragWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var wasPlayingBeforeDrag = PlayerPositionSliderDragPlayback.Start(
            request.IsPlayerPlaying,
            actions.SetPause);
        actions.SetWasPlayingBeforeDrag(wasPlayingBeforeDrag);
        actions.SetDragging(true);
        actions.ScrubSeekToSlider();

        return new PlayerPositionSliderDragWorkflowResult(
            PlayerPositionSliderDragWorkflowOutcome.Started);
    }

    public static PlayerPositionSliderDragWorkflowResult Complete(
        PlayerPositionSliderDragCompleteRequest request,
        PlayerPositionSliderDragWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        CompleteDrag(request.WasPlayingBeforeDrag, actions);
        return new PlayerPositionSliderDragWorkflowResult(
            PlayerPositionSliderDragWorkflowOutcome.Completed);
    }

    public static PlayerPositionSliderDragWorkflowResult PreviewMouseUp(
        PlayerPositionSliderDragPreviewMouseUpRequest request,
        PlayerPositionSliderDragWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsDragging)
            return new PlayerPositionSliderDragWorkflowResult(
                PlayerPositionSliderDragWorkflowOutcome.Idle);

        actions.SeekToSlider();
        return new PlayerPositionSliderDragWorkflowResult(
            PlayerPositionSliderDragWorkflowOutcome.Seeked);
    }

    public static PlayerPositionSliderDragWorkflowResult LostMouseCapture(
        PlayerPositionSliderDragLostCaptureRequest request,
        PlayerPositionSliderDragWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsDragging)
            return new PlayerPositionSliderDragWorkflowResult(
                PlayerPositionSliderDragWorkflowOutcome.Idle);

        CompleteDrag(request.WasPlayingBeforeDrag, actions);
        return new PlayerPositionSliderDragWorkflowResult(
            PlayerPositionSliderDragWorkflowOutcome.Completed);
    }

    private static void CompleteDrag(
        bool wasPlayingBeforeDrag,
        PlayerPositionSliderDragWorkflowActions actions)
    {
        actions.StopScrubTimer();
        actions.SeekToSlider();
        actions.SetDragging(false);
        PlayerPositionSliderDragPlayback.Complete(
            wasPlayingBeforeDrag,
            actions.SetPause);
    }
}
