using System.Windows;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerInteractionControllerTests
{
    [Fact]
    public void Toggle_and_cancel_preserve_action_order_and_state()
    {
        var calls = new List<string>();
        var controller = new CodingEingabemarkerInteractionController(Bindings(calls));

        var activated = controller.Toggle(isChecked: true);

        Assert.Equal(CodingEingabemarkerToggleWorkflowOutcome.Activated, activated.Outcome);
        Assert.True(controller.IsDrawing);
        Assert.Equal(
            ["pause", "ensure-overlay", "open-popup", "update-viewport", "enable-canvas", "drawing-status"],
            calls);

        calls.Clear();
        var cancelled = controller.Cancel();

        Assert.Equal(CodingEingabemarkerToggleWorkflowOutcome.Cancelled, cancelled.Outcome);
        Assert.False(controller.IsDrawing);
        Assert.Equal(CodingOverlayInputEingabemarkerState.Inactive, controller.OverlayInputState);
        Assert.Equal(["uncheck", "hide-popup", "clear-preview", "reset-cursor"], calls);
    }

    [Fact]
    public void Pointer_drag_uses_shared_state_and_preserves_completion_order()
    {
        RunOnStaThread(() =>
        {
            var calls = new List<string>();
            var controller = new CodingEingabemarkerInteractionController(Bindings(calls));
            controller.Toggle(isChecked: true);
            calls.Clear();

            var down = controller.MouseDown(new Point(80, 70));
            var move = controller.MouseMove(new Point(20, 10));
            var up = controller.MouseUp(new Point(20, 10));

            Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.DragStarted, down.Outcome);
            Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.PreviewUpdated, move.Outcome);
            Assert.Equal(CodingEingabemarkerCanvasInputWorkflowOutcome.Completed, up.Outcome);
            Assert.False(controller.IsDrawing);
            Assert.Equal(CodingOverlayInputEingabemarkerState.InputBlocked, controller.OverlayInputState);
            Assert.Equal(
                [
                    "capture",
                    "create-preview:80:70",
                    "update-preview:20:10:60:60",
                    "release",
                    "disable-canvas",
                    "show-input",
                    "focus",
                    "input-status"
                ],
                calls);
        });
    }

    private static CodingEingabemarkerInteractionControllerBindings Bindings(List<string> calls)
        => new(
            PauseForCodingInteraction: () => calls.Add("pause"),
            EnsureMarkOverlayReady: () => calls.Add("ensure-overlay"),
            OpenCodingOverlayPopup: () => calls.Add("open-popup"),
            UpdateCodingOverlayViewport: () => calls.Add("update-viewport"),
            EnableDrawingCanvas: () => calls.Add("enable-canvas"),
            ShowDrawingStatus: () => calls.Add("drawing-status"),
            UncheckButton: () => calls.Add("uncheck"),
            HideInputPopup: () => calls.Add("hide-popup"),
            ClearPreview: _ =>
            {
                calls.Add("clear-preview");
                return null;
            },
            ResetCanvasCursor: () => calls.Add("reset-cursor"),
            CaptureMouse: () => calls.Add("capture"),
            CreatePreview: point =>
            {
                calls.Add($"create-preview:{point.X}:{point.Y}");
                return new Rectangle();
            },
            UpdatePreview: (_, rect) => calls.Add(
                $"update-preview:{rect.X}:{rect.Y}:{rect.Width}:{rect.Height}"),
            ReleaseMouseCapture: () => calls.Add("release"),
            ResolveCanvasSize: () => new Size(100, 200),
            DisableDrawingCanvas: () => calls.Add("disable-canvas"),
            ShowInputPopup: () => calls.Add("show-input"),
            FocusInput: () => calls.Add("focus"),
            ShowInputStatus: () => calls.Add("input-status"));

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
