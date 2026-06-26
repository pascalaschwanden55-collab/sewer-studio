using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayViewportRefreshWorkflowTests
{
    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    public void Execute_updates_viewport_when_canvas_size_is_not_usable(
        double actualWidth,
        double actualHeight)
    {
        var calls = new List<string>();

        var result = CodingOverlayViewportRefreshWorkflow.Execute(
            new CodingOverlayViewportRefreshRequest(actualWidth, actualHeight),
            new CodingOverlayViewportRefreshActions(
                UpdateViewport: () => calls.Add("update")));

        Assert.Equal(CodingOverlayViewportRefreshOutcome.Updated, result.Outcome);
        Assert.True(result.Updated);
        Assert.Equal(["update"], calls);
    }

    [Fact]
    public void Execute_skips_update_when_canvas_size_is_usable()
    {
        var calls = new List<string>();

        var result = CodingOverlayViewportRefreshWorkflow.Execute(
            new CodingOverlayViewportRefreshRequest(640, 480),
            new CodingOverlayViewportRefreshActions(
                UpdateViewport: () => calls.Add("update")));

        Assert.Equal(CodingOverlayViewportRefreshOutcome.NotNeeded, result.Outcome);
        Assert.False(result.Updated);
        Assert.Empty(calls);
    }
}
