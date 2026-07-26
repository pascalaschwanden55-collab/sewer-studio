using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayInputVisibilityWorkflowTests
{
    [Fact]
    public void Suspend_increments_nested_depth_without_suspending_canvas_again()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputVisibilityWorkflow.Suspend(
            new CodingOverlayInputSuspendRequest(
                SuspendDepth: 1,
                IsPopupOpen: true),
            SuspendActions(calls));

        Assert.Equal(["depth:2"], calls);
        Assert.Equal(CodingOverlayInputVisibilityWorkflowOutcome.NestedSuspend, result.Outcome);
    }

    [Fact]
    public void Suspend_first_depth_ends_current_overlay_actions_and_hides_canvas()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputVisibilityWorkflow.Suspend(
            new CodingOverlayInputSuspendRequest(
                SuspendDepth: 0,
                IsPopupOpen: true),
            SuspendActions(calls));

        Assert.Equal(
            ["depth:1", "end-drag", "cancel-draw", "remember-suspend:True", "suspend-canvas"],
            calls);
        Assert.Equal(CodingOverlayInputVisibilityWorkflowOutcome.Suspended, result.Outcome);
    }

    [Fact]
    public void Resume_skips_when_not_suspended()
    {
        var result = CodingOverlayInputVisibilityWorkflow.Resume(
            new CodingOverlayInputResumeRequest(
                SuspendDepth: 0,
                WasOpenBeforeSuspend: true,
                HasCurrentOverlay: true),
            ResumeActions(new List<string>()));

        Assert.Equal(CodingOverlayInputVisibilityWorkflowOutcome.NotSuspended, result.Outcome);
    }

    [Fact]
    public void Resume_nested_depth_only_decrements()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputVisibilityWorkflow.Resume(
            new CodingOverlayInputResumeRequest(
                SuspendDepth: 2,
                WasOpenBeforeSuspend: true,
                HasCurrentOverlay: true),
            ResumeActions(calls));

        Assert.Equal(["depth:1"], calls);
        Assert.Equal(CodingOverlayInputVisibilityWorkflowOutcome.NestedResume, result.Outcome);
    }

    [Fact]
    public void Resume_final_depth_restores_canvas_popup_and_cursor()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputVisibilityWorkflow.Resume(
            new CodingOverlayInputResumeRequest(
                SuspendDepth: 1,
                WasOpenBeforeSuspend: true,
                HasCurrentOverlay: true),
            ResumeActions(calls));

        Assert.Equal(
            ["depth:0", "resume-canvas", "open-popup", "viewport", "redraw:True", "cursor", "remember-suspend:False"],
            calls);
        Assert.Equal(CodingOverlayInputVisibilityWorkflowOutcome.Resumed, result.Outcome);
    }

    [Fact]
    public void HideForExternalWindow_remembers_popup_state_suspends_and_closes_open_popup()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputVisibilityWorkflow.HideForExternalWindow(
            new CodingOverlayInputExternalWindowRequest(IsPopupOpen: true),
            new CodingOverlayInputExternalWindowHideActions(
                RememberOpenBeforeExternalHide: isOpen => calls.Add($"remember-external:{isOpen}"),
                Suspend: () => calls.Add("suspend"),
                ClosePopup: () => calls.Add("close-popup")));

        Assert.Equal(["remember-external:True", "suspend", "close-popup"], calls);
        Assert.Equal(CodingOverlayInputVisibilityWorkflowOutcome.HiddenForExternalWindow, result.Outcome);
    }

    [Fact]
    public void RestoreAfterExternalWindow_resumes_and_reopens_previously_open_popup()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputVisibilityWorkflow.RestoreAfterExternalWindow(
            new CodingOverlayInputExternalWindowRestoreRequest(
                WasOpenBeforeExternalHide: true,
                HasCurrentOverlay: true),
            new CodingOverlayInputExternalWindowRestoreActions(
                Resume: () => calls.Add("resume"),
                OpenPopup: () => calls.Add("open-popup"),
                UpdateViewport: () => calls.Add("viewport"),
                RedrawCanvas: includeOverlay => calls.Add($"redraw:{includeOverlay}"),
                RememberOpenBeforeExternalHide: isOpen => calls.Add($"remember-external:{isOpen}")));

        Assert.Equal(["resume", "open-popup", "viewport", "redraw:True", "remember-external:False"], calls);
        Assert.Equal(CodingOverlayInputVisibilityWorkflowOutcome.RestoredAfterExternalWindow, result.Outcome);
    }

    private static CodingOverlayInputSuspendActions SuspendActions(List<string> calls)
        => new(
            SetSuspendDepth: depth => calls.Add($"depth:{depth}"),
            EndDrag: () => calls.Add("end-drag"),
            CancelDraw: () => calls.Add("cancel-draw"),
            RememberOpenBeforeSuspend: isOpen => calls.Add($"remember-suspend:{isOpen}"),
            SuspendCanvas: () => calls.Add("suspend-canvas"));

    private static CodingOverlayInputResumeActions ResumeActions(List<string> calls)
        => new(
            SetSuspendDepth: depth => calls.Add($"depth:{depth}"),
            ResumeCanvas: () => calls.Add("resume-canvas"),
            OpenPopup: () => calls.Add("open-popup"),
            UpdateViewport: () => calls.Add("viewport"),
            RedrawCanvas: includeOverlay => calls.Add($"redraw:{includeOverlay}"),
            UpdateCursor: () => calls.Add("cursor"),
            RememberOpenBeforeSuspend: isOpen => calls.Add($"remember-suspend:{isOpen}"));
}
