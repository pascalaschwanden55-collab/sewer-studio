using System.Windows;
using System.Globalization;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerCanvasInputWorkflowTests
{
    [Fact]
    public void MouseDown_ignores_when_marker_is_not_drawing()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerCanvasInputWorkflow.MouseDown(
            new CodingEingabemarkerCanvasMouseDownRequest(
                IsDrawing: false,
                CanvasPosition: new Point(10, 20)),
            MouseDownActions(calls));

        Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.Ignored, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void MouseDown_starts_drag_preview_when_marker_is_drawing()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerCanvasInputWorkflow.MouseDown(
            new CodingEingabemarkerCanvasMouseDownRequest(
                IsDrawing: true,
                CanvasPosition: new Point(10, 20)),
            MouseDownActions(calls));

        Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.DragStarted, result.Outcome);
        Assert.Equal(["start:10:20", "capture", "preview:10:20"], calls);
    }

    [Fact]
    public void MouseMove_updates_preview_only_while_drawing_with_preview()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerCanvasInputWorkflow.MouseMove(
            new CodingEingabemarkerCanvasMouseMoveRequest(
                IsDrawing: true,
                HasPreview: true,
                DragStart: new Point(80, 70),
                CanvasPosition: new Point(20, 10)),
            new CodingEingabemarkerCanvasMouseMoveActions(
                UpdatePreview: rect => calls.Add($"preview:{rect.X}:{rect.Y}:{rect.Width}:{rect.Height}")));

        Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.PreviewUpdated, result.Outcome);
        Assert.Equal(["preview:20:10:60:60"], calls);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MouseMove_ignores_when_marker_is_not_drawing_or_preview_is_missing(
        bool isDrawing,
        bool hasPreview)
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerCanvasInputWorkflow.MouseMove(
            new CodingEingabemarkerCanvasMouseMoveRequest(
                isDrawing,
                hasPreview,
                new Point(80, 70),
                new Point(20, 10)),
            new CodingEingabemarkerCanvasMouseMoveActions(
                UpdatePreview: rect => calls.Add("preview")));

        Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.Ignored, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void MouseUp_cancels_marker_after_releasing_capture_when_selection_is_invalid()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerCanvasInputWorkflow.MouseUp(
            new CodingEingabemarkerCanvasMouseUpRequest(
                IsDrawing: true,
                DragStart: new Point(10, 10),
                CanvasPosition: new Point(11, 50),
                CanvasSize: new Size(100, 100)),
            MouseUpActions(calls));

        Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.Cancelled, result.Outcome);
        Assert.Equal(["release", "cancel"], calls);
    }

    [Fact]
    public void MouseUp_completes_selection_and_opens_input_when_selection_is_valid()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerCanvasInputWorkflow.MouseUp(
            new CodingEingabemarkerCanvasMouseUpRequest(
                IsDrawing: true,
                DragStart: new Point(80, 70),
                CanvasPosition: new Point(20, 10),
                CanvasSize: new Size(100, 200)),
            MouseUpActions(calls));

        Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.Completed, result.Outcome);
        Assert.Equal(
            [
                "release",
                "selection:0.2:0.05:0.6:0.3",
                "phase:input",
                "disable-canvas",
                "show-input",
                "focus",
                "status"
            ],
            calls);
    }

    [Fact]
    public void MouseUp_ignores_when_marker_is_not_drawing()
    {
        var calls = new List<string>();

        var result = CodingEingabemarkerCanvasInputWorkflow.MouseUp(
            new CodingEingabemarkerCanvasMouseUpRequest(
                IsDrawing: false,
                DragStart: new Point(80, 70),
                CanvasPosition: new Point(20, 10),
                CanvasSize: new Size(100, 200)),
            MouseUpActions(calls));

        Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.Ignored, result.Outcome);
        Assert.Empty(calls);
    }

    private static CodingEingabemarkerCanvasMouseDownActions MouseDownActions(List<string> calls)
        => new(
            StoreDragStart: point => calls.Add($"start:{point.X}:{point.Y}"),
            CaptureMouse: () => calls.Add("capture"),
            CreatePreview: point => calls.Add($"preview:{point.X}:{point.Y}"));

    private static CodingEingabemarkerCanvasMouseUpActions MouseUpActions(List<string> calls)
        => new(
            ReleaseMouseCapture: () => calls.Add("release"),
            CancelMarker: () => calls.Add("cancel"),
            StoreNormalizedSelection: rect => calls.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "selection:{0:0.######}:{1:0.######}:{2:0.######}:{3:0.######}",
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height)),
            SetInputPhase: () => calls.Add("phase:input"),
            DisableDrawingCanvas: () => calls.Add("disable-canvas"),
            ShowInputPopup: () => calls.Add("show-input"),
            FocusInput: () => calls.Add("focus"),
            ShowInputStatus: () => calls.Add("status"));
}
