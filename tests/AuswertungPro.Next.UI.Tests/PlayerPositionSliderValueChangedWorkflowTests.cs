using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPositionSliderValueChangedWorkflowTests
{
    [Fact]
    public void Execute_is_idle_when_not_dragging()
    {
        var result = PlayerPositionSliderValueChangedWorkflow.Execute(
            new PlayerPositionSliderValueChangedWorkflowRequest(IsDragging: false),
            new PlayerPositionSliderValueChangedWorkflowActions(
                UpdateSeekPreview: () => throw new InvalidOperationException("Preview should not run.")));

        Assert.Equal(PlayerPositionSliderValueChangedWorkflowOutcome.Idle, result.Outcome);
        Assert.False(result.Updated);
    }

    [Fact]
    public void Execute_updates_seek_preview_while_dragging()
    {
        var calls = new List<string>();

        var result = PlayerPositionSliderValueChangedWorkflow.Execute(
            new PlayerPositionSliderValueChangedWorkflowRequest(IsDragging: true),
            new PlayerPositionSliderValueChangedWorkflowActions(
                UpdateSeekPreview: () => calls.Add("preview")));

        Assert.Equal(["preview"], calls);
        Assert.Equal(PlayerPositionSliderValueChangedWorkflowOutcome.PreviewUpdated, result.Outcome);
        Assert.True(result.Updated);
    }
}
