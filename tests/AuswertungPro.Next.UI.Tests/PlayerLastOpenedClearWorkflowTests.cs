using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerLastOpenedClearWorkflowTests
{
    [Fact]
    public void Execute_skips_when_closing_window_is_not_last_opened()
    {
        var calls = new List<string>();

        var result = PlayerLastOpenedClearWorkflow.Execute(
            new PlayerLastOpenedClearRequest(IsLastOpenedWindow: false),
            new PlayerLastOpenedClearActions(
                ClearLastOpened: () => calls.Add("clear")));

        Assert.Equal(PlayerLastOpenedClearOutcome.NotLastOpened, result.Outcome);
        Assert.False(result.Cleared);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_clears_when_closing_window_is_last_opened()
    {
        var calls = new List<string>();

        var result = PlayerLastOpenedClearWorkflow.Execute(
            new PlayerLastOpenedClearRequest(IsLastOpenedWindow: true),
            new PlayerLastOpenedClearActions(
                ClearLastOpened: () => calls.Add("clear")));

        Assert.Equal(PlayerLastOpenedClearOutcome.Cleared, result.Outcome);
        Assert.True(result.Cleared);
        Assert.Equal(["clear"], calls);
    }
}
