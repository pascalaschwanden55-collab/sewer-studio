using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayInputVisibilityStateControllerTests
{
    [Fact]
    public void Suspend_state_can_be_updated_and_reset_without_clearing_external_window_state()
    {
        var state = new CodingOverlayInputVisibilityStateController();

        state.SetSuspendDepth(2);
        state.RememberOpenBeforeSuspend(true);
        state.RememberOpenBeforeExternalHide(true);

        Assert.Equal(2, state.SuspendDepth);
        Assert.True(state.WasOpenBeforeSuspend);
        Assert.True(state.WasOpenBeforeExternalHide);

        state.ResetSuspendState();

        Assert.Equal(0, state.SuspendDepth);
        Assert.False(state.WasOpenBeforeSuspend);
        Assert.True(state.WasOpenBeforeExternalHide);
    }

    [Fact]
    public void Deactivated_state_is_tracked_for_window_activation_workflow()
    {
        var state = new CodingOverlayInputVisibilityStateController();

        state.SetDeactivatedByExternalWindow(true);

        Assert.True(state.DeactivatedByExternalWindow);

        state.SetDeactivatedByExternalWindow(false);

        Assert.False(state.DeactivatedByExternalWindow);
    }
}
