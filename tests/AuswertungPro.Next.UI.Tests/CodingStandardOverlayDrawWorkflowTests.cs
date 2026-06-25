using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStandardOverlayDrawWorkflowTests
{
    [Fact]
    public void MouseDown_skips_without_view_model()
    {
        var result = CodingStandardOverlayDrawWorkflow.MouseDown(
            new CodingStandardOverlayMouseDownRequest(HasViewModel: false),
            MouseDownActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingStandardOverlayDrawWorkflowOutcome.NoViewModel, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void MouseDown_starts_standard_overlay_draw()
    {
        var calls = new List<string>();

        var result = CodingStandardOverlayDrawWorkflow.MouseDown(
            new CodingStandardOverlayMouseDownRequest(HasViewModel: true),
            MouseDownActions(calls.Add));

        Assert.Equal(CodingStandardOverlayDrawWorkflowOutcome.Started, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            [
                "clear-overlay",
                "create:false",
                "info:null",
                "begin",
                "capture",
                "clear-canvas",
                "render-ai",
                "render-ref",
                "badge"
            ],
            calls);
    }

    [Fact]
    public void MouseMove_updates_draw_and_stops_when_current_overlay_is_missing()
    {
        var calls = new List<string>();

        var result = CodingStandardOverlayDrawWorkflow.MouseMove(
            new CodingStandardOverlayMouseMoveRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsDrawing: true),
            MouseMoveActions(
                calls.Add,
                hasCurrentOverlay: () =>
                {
                    calls.Add("has-overlay:false");
                    return false;
                }));

        Assert.Equal(CodingStandardOverlayDrawWorkflowOutcome.UpdatedWithoutOverlay, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["update", "has-overlay:false"], calls);
    }

    [Fact]
    public void MouseMove_renders_preview_when_current_overlay_exists()
    {
        var calls = new List<string>();

        var result = CodingStandardOverlayDrawWorkflow.MouseMove(
            new CodingStandardOverlayMouseMoveRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsDrawing: true),
            MouseMoveActions(
                calls.Add,
                hasCurrentOverlay: () => true));

        Assert.Equal(CodingStandardOverlayDrawWorkflowOutcome.PreviewRendered, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["update", "clear-canvas", "render-ai", "render-ref", "badge", "preview"], calls);
    }

    [Fact]
    public void MouseUp_finishes_without_overlay_and_disables_create_event()
    {
        var calls = new List<string>();

        var result = CodingStandardOverlayDrawWorkflow.MouseUp(
            new CodingStandardOverlayMouseUpRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsDrawing: true,
                IsMarkToolActive: false,
                IsLiveAiChecked: true),
            MouseUpActions(
                calls.Add,
                hasCurrentOverlay: () =>
                {
                    calls.Add("has-overlay:false");
                    return false;
                }));

        Assert.Equal(CodingStandardOverlayDrawWorkflowOutcome.CompletedWithoutOverlay, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            [
                "complete",
                "release",
                "clear-canvas",
                "render-ai",
                "render-ref",
                "badge",
                "has-overlay:false",
                "info:null",
                "create:false"
            ],
            calls);
    }

    [Fact]
    public void MouseUp_finishes_mark_tool_after_final_render()
    {
        var calls = new List<string>();

        var result = CodingStandardOverlayDrawWorkflow.MouseUp(
            new CodingStandardOverlayMouseUpRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsDrawing: true,
                IsMarkToolActive: true,
                IsLiveAiChecked: true),
            MouseUpActions(calls.Add));

        Assert.Equal(CodingStandardOverlayDrawWorkflowOutcome.MarkCompleted, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            ["complete", "release", "clear-canvas", "render-ai", "render-ref", "badge", "final", "mark"],
            calls);
    }

    [Fact]
    public void MouseUp_enables_create_event_and_runs_live_ai_hint_for_overlay()
    {
        var calls = new List<string>();

        var result = CodingStandardOverlayDrawWorkflow.MouseUp(
            new CodingStandardOverlayMouseUpRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsDrawing: true,
                IsMarkToolActive: false,
                IsLiveAiChecked: true),
            MouseUpActions(calls.Add));

        Assert.Equal(CodingStandardOverlayDrawWorkflowOutcome.Completed, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            [
                "complete",
                "release",
                "clear-canvas",
                "render-ai",
                "render-ref",
                "badge",
                "final",
                "info:overlay",
                "create:true",
                "live-ai"
            ],
            calls);
    }

    private static CodingStandardOverlayMouseDownActions MouseDownActions(Action<string> calls)
        => new(
            ClearCurrentOverlay: () => calls("clear-overlay"),
            SetCreateEventEnabled: enabled => calls($"create:{BoolText(enabled)}"),
            UpdateOverlayInfoEmpty: () => calls("info:null"),
            BeginOverlayDraw: () => calls("begin"),
            CaptureMouse: () => calls("capture"),
            ClearTransientCodingCanvas: () => calls("clear-canvas"),
            RenderAiOverlays: () => calls("render-ai"),
            RenderReferenceDn: () => calls("render-ref"),
            UpdateToolBadge: () => calls("badge"));

    private static CodingStandardOverlayMouseMoveActions MouseMoveActions(
        Action<string> calls,
        Func<bool>? hasCurrentOverlay = null)
        => new(
            UpdateOverlayDraw: () => calls("update"),
            HasCurrentOverlay: hasCurrentOverlay ?? (() => true),
            ClearTransientCodingCanvas: () => calls("clear-canvas"),
            RenderAiOverlays: () => calls("render-ai"),
            RenderReferenceDn: () => calls("render-ref"),
            UpdateToolBadge: () => calls("badge"),
            RenderPreviewOverlay: () => calls("preview"));

    private static CodingStandardOverlayMouseUpActions MouseUpActions(
        Action<string> calls,
        Func<bool>? hasCurrentOverlay = null)
        => new(
            CompleteOverlayDraw: () => calls("complete"),
            ReleaseMouseCapture: () => calls("release"),
            ClearTransientCodingCanvas: () => calls("clear-canvas"),
            RenderAiOverlays: () => calls("render-ai"),
            RenderReferenceDn: () => calls("render-ref"),
            UpdateToolBadge: () => calls("badge"),
            HasCurrentOverlay: hasCurrentOverlay ?? (() => true),
            RenderFinalOverlay: () => calls("final"),
            HandleMarkDrawingComplete: () => calls("mark"),
            UpdateOverlayInfoEmpty: () => calls("info:null"),
            UpdateOverlayInfoCurrent: () => calls("info:overlay"),
            SetCreateEventEnabled: enabled => calls($"create:{BoolText(enabled)}"),
            AnalyzeWithOverlayHint: () => calls("live-ai"));

    private static string BoolText(bool value)
        => value.ToString().ToLowerInvariant();
}
