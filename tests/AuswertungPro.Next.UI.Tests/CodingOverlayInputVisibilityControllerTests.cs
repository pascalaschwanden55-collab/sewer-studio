using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayInputVisibilityControllerTests
{
    [Fact]
    public void Nested_run_suspends_once_and_restores_shared_state_after_outer_callback()
    {
        var calls = new List<string>();
        var state = new CodingOverlayInputVisibilityStateController();
        var controller = CreateController(
            state,
            calls,
            isPopupOpen: () => true,
            hasCurrentOverlay: () => true);

        var result = controller.Run(() =>
        {
            calls.Add("outer-start");
            var nested = controller.Run(() =>
            {
                calls.Add("inner");
                return 21;
            });
            calls.Add("outer-end");
            return nested * 2;
        });

        Assert.Equal(42, result);
        Assert.Equal(0, controller.SuspendDepth);
        Assert.Equal(0, state.SuspendDepth);
        Assert.False(state.WasOpenBeforeSuspend);
        Assert.Equal(
            [
                "end-drag",
                "cancel-draw",
                "suspend-canvas",
                "outer-start",
                "inner",
                "outer-end",
                "resume-canvas",
                "open-popup",
                "viewport",
                "redraw:True",
                "cursor"
            ],
            calls);
    }

    [Fact]
    public async Task RunAsync_resumes_shared_state_when_awaited_callback_throws()
    {
        var calls = new List<string>();
        var state = new CodingOverlayInputVisibilityStateController();
        var controller = CreateController(
            state,
            calls,
            isPopupOpen: () => false,
            hasCurrentOverlay: () => true);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.RunAsync(async () =>
            {
                calls.Add("callback-start");
                await Task.Yield();
                calls.Add("callback-error");
                throw new InvalidOperationException("dialog failed");
            }));

        Assert.Equal("dialog failed", error.Message);
        Assert.Equal(0, controller.SuspendDepth);
        Assert.False(state.WasOpenBeforeSuspend);
        Assert.Equal(
            [
                "end-drag",
                "cancel-draw",
                "suspend-canvas",
                "callback-start",
                "callback-error",
                "resume-canvas",
                "cursor"
            ],
            calls);
    }

    [Fact]
    public void External_window_roundtrip_preserves_existing_restore_sequence_and_state()
    {
        var calls = new List<string>();
        var popupOpen = true;
        var state = new CodingOverlayInputVisibilityStateController();
        var controller = CreateController(
            state,
            calls,
            isPopupOpen: () => popupOpen,
            hasCurrentOverlay: () => true,
            openPopup: () =>
            {
                popupOpen = true;
                calls.Add("open-popup");
            },
            closePopup: () =>
            {
                popupOpen = false;
                calls.Add("close-popup");
            });

        controller.HideForExternalWindow();

        Assert.Equal(1, controller.SuspendDepth);
        Assert.True(state.WasOpenBeforeSuspend);
        Assert.True(state.WasOpenBeforeExternalHide);
        Assert.False(popupOpen);

        controller.RestoreAfterExternalWindow();

        Assert.Equal(0, controller.SuspendDepth);
        Assert.False(state.WasOpenBeforeSuspend);
        Assert.False(state.WasOpenBeforeExternalHide);
        Assert.True(popupOpen);
        Assert.Equal(
            [
                "end-drag",
                "cancel-draw",
                "suspend-canvas",
                "close-popup",
                "resume-canvas",
                "open-popup",
                "viewport",
                "redraw:True",
                "cursor",
                "open-popup",
                "viewport",
                "redraw:True"
            ],
            calls);
    }

    [Fact]
    public void External_window_roundtrip_keeps_previously_closed_popup_closed()
    {
        var calls = new List<string>();
        var popupOpen = false;
        var state = new CodingOverlayInputVisibilityStateController();
        var controller = CreateController(
            state,
            calls,
            isPopupOpen: () => popupOpen,
            hasCurrentOverlay: () => true,
            openPopup: () =>
            {
                popupOpen = true;
                calls.Add("open-popup");
            },
            closePopup: () =>
            {
                popupOpen = false;
                calls.Add("close-popup");
            });

        controller.HideForExternalWindow();
        controller.RestoreAfterExternalWindow();

        Assert.False(popupOpen);
        Assert.Equal(0, controller.SuspendDepth);
        Assert.False(state.WasOpenBeforeSuspend);
        Assert.False(state.WasOpenBeforeExternalHide);
        Assert.Equal(
            [
                "end-drag",
                "cancel-draw",
                "suspend-canvas",
                "resume-canvas",
                "cursor"
            ],
            calls);
    }

    [Fact]
    public void Activation_and_reset_state_are_forwarded_to_the_same_state_controller()
    {
        var state = new CodingOverlayInputVisibilityStateController();
        var controller = CreateController(
            state,
            [],
            isPopupOpen: () => true,
            hasCurrentOverlay: () => false);

        controller.SetDeactivatedByExternalWindow(true);
        controller.Run(() => { });
        state.SetSuspendDepth(3);
        state.RememberOpenBeforeSuspend(true);

        controller.ResetSuspendState();

        Assert.True(controller.DeactivatedByExternalWindow);
        Assert.True(state.DeactivatedByExternalWindow);
        Assert.Equal(0, controller.SuspendDepth);
        Assert.False(state.WasOpenBeforeSuspend);
    }

    private static CodingOverlayInputVisibilityController CreateController(
        CodingOverlayInputVisibilityStateController state,
        List<string> calls,
        Func<bool> isPopupOpen,
        Func<bool> hasCurrentOverlay,
        Action? openPopup = null,
        Action? closePopup = null)
        => new(
            state,
            new CodingOverlayInputVisibilityControllerBindings(
                IsPopupOpen: isPopupOpen,
                HasCurrentOverlay: hasCurrentOverlay,
                EndDrag: () => calls.Add("end-drag"),
                CancelDraw: () => calls.Add("cancel-draw"),
                SuspendCanvas: () => calls.Add("suspend-canvas"),
                ResumeCanvas: () => calls.Add("resume-canvas"),
                OpenPopup: openPopup ?? (() => calls.Add("open-popup")),
                ClosePopup: closePopup ?? (() => calls.Add("close-popup")),
                UpdateViewport: () => calls.Add("viewport"),
                RedrawCanvas: includeManualOverlay => calls.Add($"redraw:{includeManualOverlay}"),
                UpdateCursor: () => calls.Add("cursor")));
}
