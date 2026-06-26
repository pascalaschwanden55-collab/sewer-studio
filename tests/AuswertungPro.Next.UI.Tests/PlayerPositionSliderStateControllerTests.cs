using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPositionSliderStateControllerTests
{
    [Fact]
    public void Defaults_to_not_dragging_and_not_playing_before_drag()
    {
        var controller = new PlayerPositionSliderStateController();

        Assert.False(controller.IsDragging);
        Assert.False(controller.WasPlayingBeforeDrag);
    }

    [Fact]
    public void Drag_actions_update_internal_drag_state()
    {
        var controller = new PlayerPositionSliderStateController();
        var calls = new List<string>();
        var actions = controller.CreateDragActions(
            setPause: value => calls.Add($"pause:{value}"),
            stopScrubTimer: () => calls.Add("stop-scrub"),
            seekToSlider: () => calls.Add("seek"),
            scrubSeekToSlider: () => calls.Add("scrub"));

        actions.SetWasPlayingBeforeDrag(true);
        actions.SetDragging(true);

        Assert.True(controller.WasPlayingBeforeDrag);
        Assert.True(controller.IsDragging);

        actions.SetDragging(false);
        actions.SetWasPlayingBeforeDrag(false);

        Assert.False(controller.WasPlayingBeforeDrag);
        Assert.False(controller.IsDragging);
    }
}
