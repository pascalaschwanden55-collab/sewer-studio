using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPositionSliderDragWorkflowTests
{
    [Fact]
    public void Start_pauses_records_drag_state_and_scrubs_in_order()
    {
        var calls = new List<string>();

        var result = PlayerPositionSliderDragWorkflow.Start(
            new PlayerPositionSliderDragStartRequest(IsPlayerPlaying: true),
            Actions(calls));

        Assert.Equal(["pause:True", "was:True", "drag:True", "scrub"], calls);
        Assert.Equal(PlayerPositionSliderDragWorkflowOutcome.Started, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Complete_stops_scrub_seeks_clears_drag_and_resumes_when_needed()
    {
        var calls = new List<string>();

        var result = PlayerPositionSliderDragWorkflow.Complete(
            new PlayerPositionSliderDragCompleteRequest(WasPlayingBeforeDrag: true),
            Actions(calls));

        Assert.Equal(["stop-scrub", "seek", "drag:False", "pause:False"], calls);
        Assert.Equal(PlayerPositionSliderDragWorkflowOutcome.Completed, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void PreviewMouseUp_seeks_only_when_not_dragging()
    {
        var calls = new List<string>();

        var result = PlayerPositionSliderDragWorkflow.PreviewMouseUp(
            new PlayerPositionSliderDragPreviewMouseUpRequest(IsDragging: false),
            Actions(calls));

        Assert.Equal(["seek"], calls);
        Assert.Equal(PlayerPositionSliderDragWorkflowOutcome.Seeked, result.Outcome);
    }

    [Fact]
    public void PreviewMouseUp_is_idle_while_dragging()
    {
        var result = PlayerPositionSliderDragWorkflow.PreviewMouseUp(
            new PlayerPositionSliderDragPreviewMouseUpRequest(IsDragging: true),
            NoActions());

        Assert.Equal(PlayerPositionSliderDragWorkflowOutcome.Idle, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void LostMouseCapture_completes_only_when_dragging()
    {
        var calls = new List<string>();

        var result = PlayerPositionSliderDragWorkflow.LostMouseCapture(
            new PlayerPositionSliderDragLostCaptureRequest(
                IsDragging: true,
                WasPlayingBeforeDrag: true),
            Actions(calls));

        Assert.Equal(["stop-scrub", "seek", "drag:False", "pause:False"], calls);
        Assert.Equal(PlayerPositionSliderDragWorkflowOutcome.Completed, result.Outcome);
    }

    [Fact]
    public void LostMouseCapture_is_idle_when_not_dragging()
    {
        var result = PlayerPositionSliderDragWorkflow.LostMouseCapture(
            new PlayerPositionSliderDragLostCaptureRequest(
                IsDragging: false,
                WasPlayingBeforeDrag: true),
            NoActions());

        Assert.Equal(PlayerPositionSliderDragWorkflowOutcome.Idle, result.Outcome);
        Assert.False(result.Handled);
    }

    private static PlayerPositionSliderDragWorkflowActions Actions(List<string> calls)
        => new(
            SetWasPlayingBeforeDrag: value => calls.Add($"was:{value}"),
            SetDragging: value => calls.Add($"drag:{value}"),
            SetPause: value => calls.Add($"pause:{value}"),
            StopScrubTimer: () => calls.Add("stop-scrub"),
            SeekToSlider: () => calls.Add("seek"),
            ScrubSeekToSlider: () => calls.Add("scrub"));

    private static PlayerPositionSliderDragWorkflowActions NoActions()
        => new(
            SetWasPlayingBeforeDrag: _ => throw new InvalidOperationException("SetWas should not run."),
            SetDragging: _ => throw new InvalidOperationException("SetDragging should not run."),
            SetPause: _ => throw new InvalidOperationException("SetPause should not run."),
            StopScrubTimer: () => throw new InvalidOperationException("StopScrub should not run."),
            SeekToSlider: () => throw new InvalidOperationException("Seek should not run."),
            ScrubSeekToSlider: () => throw new InvalidOperationException("Scrub should not run."));
}
