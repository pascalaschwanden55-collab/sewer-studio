using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingNavigationPendingStateTests
{
    [Fact]
    public void MarkPending_sets_state_until_replaced_by_workflow_result()
    {
        var state = new CodingNavigationPendingState();

        state.MarkPending();

        Assert.True(state.IsPending);

        state.Set(false);

        Assert.False(state.IsPending);
    }

    [Fact]
    public void Set_accepts_initial_pending_state_from_coding_mode_enter()
    {
        var state = new CodingNavigationPendingState();

        state.Set(true);

        Assert.True(state.IsPending);
    }
}
