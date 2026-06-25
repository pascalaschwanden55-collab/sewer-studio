using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiPointOverlayDrawWorkflowTests
{
    [Fact]
    public void MouseDown_skips_without_prerequisites()
    {
        var result = CodingMultiPointOverlayDrawWorkflow.MouseDown(
            new CodingMultiPointOverlayMouseDownRequest(
                HasOverlayService: false,
                HasViewModel: true,
                DrawPointCount: 0,
                IsLiveAiChecked: false),
            MouseDownActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingMultiPointOverlayDrawWorkflowOutcome.PrerequisitesMissing, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void MouseDown_resets_current_overlay_before_first_point()
    {
        var calls = new List<string>();

        var result = CodingMultiPointOverlayDrawWorkflow.MouseDown(
            new CodingMultiPointOverlayMouseDownRequest(
                HasOverlayService: true,
                HasViewModel: true,
                DrawPointCount: 0,
                IsLiveAiChecked: false),
            MouseDownActions(
                calls.Add,
                addPoint: () =>
                {
                    calls.Add("add:false");
                    return false;
                }));

        Assert.Equal(CodingMultiPointOverlayDrawWorkflowOutcome.PointAdded, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            [
                "clear-overlay",
                "create:false",
                "info:null",
                "add:false",
                "clear-canvas",
                "render-ai",
                "render-ref",
                "badge",
                "has-overlay:true",
                "render:preview"
            ],
            calls);
    }

    [Fact]
    public void MouseDown_does_not_reset_after_first_point()
    {
        var calls = new List<string>();

        var result = CodingMultiPointOverlayDrawWorkflow.MouseDown(
            new CodingMultiPointOverlayMouseDownRequest(
                HasOverlayService: true,
                HasViewModel: true,
                DrawPointCount: 2,
                IsLiveAiChecked: false),
            MouseDownActions(calls.Add, addPoint: () => false));

        Assert.Equal(CodingMultiPointOverlayDrawWorkflowOutcome.PointAdded, result.Outcome);
        Assert.DoesNotContain("clear-overlay", calls);
        Assert.DoesNotContain("create:false", calls);
        Assert.DoesNotContain("info:null", calls);
    }

    [Fact]
    public void MouseDown_completes_and_runs_live_ai_when_overlay_exists()
    {
        var calls = new List<string>();

        var result = CodingMultiPointOverlayDrawWorkflow.MouseDown(
            new CodingMultiPointOverlayMouseDownRequest(
                HasOverlayService: true,
                HasViewModel: true,
                DrawPointCount: 1,
                IsLiveAiChecked: true),
            MouseDownActions(calls.Add, addPoint: () => true));

        Assert.Equal(CodingMultiPointOverlayDrawWorkflowOutcome.Completed, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            [
                "clear-canvas",
                "render-ai",
                "render-ref",
                "badge",
                "has-overlay:true",
                "render:final",
                "info:overlay",
                "create:true",
                "live-ai"
            ],
            calls);
    }

    [Fact]
    public void MouseDown_completes_without_live_ai_when_overlay_is_missing()
    {
        var calls = new List<string>();

        var result = CodingMultiPointOverlayDrawWorkflow.MouseDown(
            new CodingMultiPointOverlayMouseDownRequest(
                HasOverlayService: true,
                HasViewModel: true,
                DrawPointCount: 1,
                IsLiveAiChecked: true),
            MouseDownActions(
                calls.Add,
                addPoint: () => true,
                hasCurrentOverlay: () =>
                {
                    calls.Add("has-overlay:false");
                    return false;
                }));

        Assert.Equal(CodingMultiPointOverlayDrawWorkflowOutcome.CompletedWithoutOverlay, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["clear-canvas", "render-ai", "render-ref", "badge", "has-overlay:false", "info:overlay", "create:true"], calls);
    }

    [Fact]
    public void MouseMove_skips_when_not_multipoint_or_no_points()
    {
        var result = CodingMultiPointOverlayDrawWorkflow.MouseMove(
            new CodingMultiPointOverlayMouseMoveRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsMultiPointTool: false,
                DrawPointCount: 2),
            MouseMoveActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingMultiPointOverlayDrawWorkflowOutcome.NotDrawing, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void MouseMove_updates_preview_when_overlay_exists()
    {
        var calls = new List<string>();

        var result = CodingMultiPointOverlayDrawWorkflow.MouseMove(
            new CodingMultiPointOverlayMouseMoveRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsMultiPointTool: true,
                DrawPointCount: 1),
            MouseMoveActions(calls.Add));

        Assert.Equal(CodingMultiPointOverlayDrawWorkflowOutcome.PreviewRendered, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["update", "clear-canvas", "render-ai", "render-ref", "badge", "has-overlay:true", "render:preview"], calls);
    }

    private static CodingMultiPointOverlayMouseDownActions MouseDownActions(
        Action<string> calls,
        Func<bool>? addPoint = null,
        Func<bool>? hasCurrentOverlay = null)
        => new(
            ClearCurrentOverlay: () => calls("clear-overlay"),
            SetCreateEventEnabled: enabled => calls($"create:{BoolText(enabled)}"),
            UpdateOverlayInfoEmpty: () => calls("info:null"),
            AddMultiPointOverlayPoint: addPoint ?? (() => false),
            ClearTransientCodingCanvas: () => calls("clear-canvas"),
            RenderAiOverlays: () => calls("render-ai"),
            RenderReferenceDn: () => calls("render-ref"),
            UpdateToolBadge: () => calls("badge"),
            HasCurrentOverlay: hasCurrentOverlay ?? (() =>
            {
                calls("has-overlay:true");
                return true;
            }),
            RenderPreviewOverlay: () => calls("render:preview"),
            RenderFinalOverlay: () => calls("render:final"),
            UpdateOverlayInfoCurrent: () => calls("info:overlay"),
            AnalyzeWithOverlayHint: () => calls("live-ai"));

    private static CodingMultiPointOverlayMouseMoveActions MouseMoveActions(
        Action<string> calls,
        Func<bool>? hasCurrentOverlay = null)
        => new(
            UpdateMultiPointOverlayPreview: () => calls("update"),
            ClearTransientCodingCanvas: () => calls("clear-canvas"),
            RenderAiOverlays: () => calls("render-ai"),
            RenderReferenceDn: () => calls("render-ref"),
            UpdateToolBadge: () => calls("badge"),
            HasCurrentOverlay: hasCurrentOverlay ?? (() =>
            {
                calls("has-overlay:true");
                return true;
            }),
            RenderPreviewOverlay: () => calls("render:preview"));

    private static string BoolText(bool value)
        => value ? "true" : "false";
}
