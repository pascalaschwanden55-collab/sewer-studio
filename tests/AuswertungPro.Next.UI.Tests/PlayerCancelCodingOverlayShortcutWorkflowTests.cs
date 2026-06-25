using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerCancelCodingOverlayShortcutWorkflowTests
{
    [Fact]
    public void Execute_cancels_overlay_releases_capture_resets_view_model_and_redraws_when_needed()
    {
        var calls = new List<string>();

        var result = PlayerCancelCodingOverlayShortcutWorkflow.Execute(
            new PlayerCancelCodingOverlayShortcutWorkflowRequest(
                IsMouseCaptured: true,
                HasCodingViewModel: true,
                IsCodingOverlayOpen: true),
            Actions(calls));

        Assert.Equal(
            [
                "cancel-draw",
                "cancel-schema",
                "release-capture",
                "clear-overlay",
                "disable-create",
                "clear-info",
                "redraw"
            ],
            calls);
        Assert.Equal(PlayerCancelCodingOverlayShortcutWorkflowOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public void Execute_skips_optional_actions_when_not_needed()
    {
        var calls = new List<string>();

        var result = PlayerCancelCodingOverlayShortcutWorkflow.Execute(
            new PlayerCancelCodingOverlayShortcutWorkflowRequest(
                IsMouseCaptured: false,
                HasCodingViewModel: false,
                IsCodingOverlayOpen: false),
            Actions(calls));

        Assert.Equal(["cancel-draw", "cancel-schema"], calls);
        Assert.Equal(PlayerCancelCodingOverlayShortcutWorkflowOutcome.Cancelled, result.Outcome);
    }

    private static PlayerCancelCodingOverlayShortcutWorkflowActions Actions(List<string> calls)
        => new(
            CancelDraw: () => calls.Add("cancel-draw"),
            CancelSchema: () => calls.Add("cancel-schema"),
            ReleaseMouseCapture: () => calls.Add("release-capture"),
            ClearCurrentOverlay: () => calls.Add("clear-overlay"),
            DisableCreateEvent: () => calls.Add("disable-create"),
            ClearOverlayInfo: () => calls.Add("clear-info"),
            RedrawCodingCanvasWithoutManualOverlay: () => calls.Add("redraw"));
}
