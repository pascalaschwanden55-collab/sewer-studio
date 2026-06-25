using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerLastOverlayDisplayWorkflowTests
{
    [Fact]
    public void Show_skips_when_no_last_window_exists()
    {
        var calls = new List<string>();

        var result = PlayerLastOverlayDisplayWorkflow.Show(
            new PlayerLastOverlayDisplayWorkflowRequest(HasLastWindow: false),
            new PlayerLastOverlayDisplayWorkflowActions(
                ShowOverlay: () => calls.Add("show")));

        Assert.Equal(PlayerLastOverlayDisplayWorkflowOutcome.NoWindow, result.Outcome);
        Assert.False(result.Handled);
        Assert.Empty(calls);
    }

    [Fact]
    public void Show_invokes_overlay_when_last_window_exists()
    {
        var calls = new List<string>();

        var result = PlayerLastOverlayDisplayWorkflow.Show(
            new PlayerLastOverlayDisplayWorkflowRequest(HasLastWindow: true),
            new PlayerLastOverlayDisplayWorkflowActions(
                ShowOverlay: () => calls.Add("show")));

        Assert.Equal(PlayerLastOverlayDisplayWorkflowOutcome.Shown, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["show"], calls);
    }
}
