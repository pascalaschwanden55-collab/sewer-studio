using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowActivationWorkflowTests
{
    [Fact]
    public void Deactivate_ignores_internal_overlay_suspend()
    {
        var result = PlayerWindowActivationWorkflow.Deactivate(
            new PlayerWindowDeactivationRequest(CodingOverlaySuspendDepth: 1),
            NoActions());

        Assert.Equal(PlayerWindowActivationWorkflowOutcome.Idle, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Deactivate_marks_external_deactivation_and_hides_overlay()
    {
        var calls = new List<string>();

        var result = PlayerWindowActivationWorkflow.Deactivate(
            new PlayerWindowDeactivationRequest(CodingOverlaySuspendDepth: 0),
            Actions(calls));

        Assert.Equal(["external:True", "hide"], calls);
        Assert.Equal(PlayerWindowActivationWorkflowOutcome.Deactivated, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Activate_is_idle_without_previous_external_deactivation()
    {
        var result = PlayerWindowActivationWorkflow.Activate(
            new PlayerWindowActivationRequest(WasDeactivatedByExternalWindow: false),
            NoActions());

        Assert.Equal(PlayerWindowActivationWorkflowOutcome.Idle, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Activate_clears_external_deactivation_and_restores_overlay()
    {
        var calls = new List<string>();

        var result = PlayerWindowActivationWorkflow.Activate(
            new PlayerWindowActivationRequest(WasDeactivatedByExternalWindow: true),
            Actions(calls));

        Assert.Equal(["external:False", "restore"], calls);
        Assert.Equal(PlayerWindowActivationWorkflowOutcome.Activated, result.Outcome);
        Assert.True(result.Handled);
    }

    private static PlayerWindowActivationWorkflowActions Actions(List<string> calls)
        => new(
            SetDeactivatedByExternalWindow: value => calls.Add($"external:{value}"),
            HideCodingOverlayForExternalWindow: () => calls.Add("hide"),
            RestoreCodingOverlayAfterExternalWindow: () => calls.Add("restore"));

    private static PlayerWindowActivationWorkflowActions NoActions()
        => new(
            SetDeactivatedByExternalWindow: _ => throw new InvalidOperationException("Flag should not change."),
            HideCodingOverlayForExternalWindow: () => throw new InvalidOperationException("Overlay should not hide."),
            RestoreCodingOverlayAfterExternalWindow: () => throw new InvalidOperationException("Overlay should not restore."));
}
