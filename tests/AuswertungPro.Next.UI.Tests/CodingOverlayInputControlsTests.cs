using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayInputControlsTests
{
    [Fact]
    public void ApplyActiveToolSelection_sets_label_and_disables_create_event()
    {
        RunOnStaThread(() =>
        {
            var label = new TextBlock { Text = "alt" };
            var createEvent = new Button { IsEnabled = true };

            CodingOverlayInputControls.ApplyActiveToolSelection(label, createEvent, "Kalibrierung");

            Assert.Equal("Kalibrierung", label.Text);
            Assert.False(createEvent.IsEnabled);
        });
    }

    [Fact]
    public void SetCreateEventEnabled_updates_create_event_button()
    {
        RunOnStaThread(() =>
        {
            var createEvent = new Button { IsEnabled = false };

            CodingOverlayInputControls.SetCreateEventEnabled(createEvent, true);

            Assert.True(createEvent.IsEnabled);

            CodingOverlayInputControls.SetCreateEventEnabled(createEvent, false);

            Assert.False(createEvent.IsEnabled);
        });
    }

    [Fact]
    public void SuspendCanvas_hides_canvas_and_disables_input()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas
            {
                Visibility = Visibility.Visible,
                IsHitTestVisible = true,
                Cursor = Cursors.Cross
            };

            CodingOverlayInputControls.SuspendCanvas(canvas);

            Assert.Equal(Visibility.Hidden, canvas.Visibility);
            Assert.False(canvas.IsHitTestVisible);
            Assert.Same(Cursors.Arrow, canvas.Cursor);
        });
    }

    [Fact]
    public void ResumeCanvas_shows_canvas_and_enables_input()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas
            {
                Visibility = Visibility.Hidden,
                IsHitTestVisible = false
            };

            CodingOverlayInputControls.ResumeCanvas(canvas);

            Assert.Equal(Visibility.Visible, canvas.Visibility);
            Assert.True(canvas.IsHitTestVisible);
        });
    }

    [Fact]
    public void EnableDrawingCanvas_enables_hit_testing_and_cross_cursor()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas
            {
                IsHitTestVisible = false,
                Cursor = Cursors.Arrow
            };

            CodingOverlayInputControls.EnableDrawingCanvas(canvas);

            Assert.True(canvas.IsHitTestVisible);
            Assert.Same(Cursors.Cross, canvas.Cursor);
        });
    }

    [Fact]
    public void DisableDrawingCanvas_disables_hit_testing_and_arrow_cursor()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas
            {
                IsHitTestVisible = true,
                Cursor = Cursors.Cross
            };

            CodingOverlayInputControls.DisableDrawingCanvas(canvas);

            Assert.False(canvas.IsHitTestVisible);
            Assert.Same(Cursors.Arrow, canvas.Cursor);
        });
    }

    [Fact]
    public void ResetCanvasCursor_sets_arrow_cursor_without_changing_hit_testing()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas
            {
                IsHitTestVisible = true,
                Cursor = Cursors.Cross
            };

            CodingOverlayInputControls.ResetCanvasCursor(canvas);

            Assert.True(canvas.IsHitTestVisible);
            Assert.Same(Cursors.Arrow, canvas.Cursor);
        });
    }

    [Fact]
    public void ApplyCanvasCursor_sets_cross_or_arrow_cursor()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas { Cursor = Cursors.Arrow };

            CodingOverlayInputControls.ApplyCanvasCursor(canvas, useCrossCursor: true);

            Assert.Same(Cursors.Cross, canvas.Cursor);

            CodingOverlayInputControls.ApplyCanvasCursor(canvas, useCrossCursor: false);

            Assert.Same(Cursors.Arrow, canvas.Cursor);
        });
    }

    [Fact]
    public void Mouse_capture_methods_delegate_to_canvas_without_changing_visual_state()
    {
        RunOnStaThread(() =>
        {
            var canvas = new Canvas
            {
                IsHitTestVisible = true,
                Visibility = Visibility.Visible,
                Cursor = Cursors.Cross
            };

            CodingOverlayInputControls.CaptureCanvasMouse(canvas);
            CodingOverlayInputControls.ReleaseCanvasMouse(canvas);

            Assert.True(canvas.IsHitTestVisible);
            Assert.Equal(Visibility.Visible, canvas.Visibility);
            Assert.Same(Cursors.Cross, canvas.Cursor);
        });
    }

    [Fact]
    public void Canvas_metric_methods_read_and_update_canvas_surface_state()
    {
        RunOnStaThread(() =>
        {
            var sizedCanvas = new Canvas
            {
                Width = 120,
                Height = 80
            };

            CodingOverlayInputControls.SetCanvasSize(sizedCanvas, 320, 180);

            Assert.Equal(new Size(320, 180), CodingOverlayInputControls.GetCanvasSize(sizedCanvas));

            var actualCanvas = new Canvas();
            actualCanvas.Measure(new Size(640, 360));
            actualCanvas.Arrange(new Rect(0, 0, 640, 360));

            Assert.Equal(new Size(640, 360), CodingOverlayInputControls.GetCanvasActualSize(actualCanvas));
            Assert.False(CodingOverlayInputControls.IsCanvasMouseCaptured(actualCanvas));
        });
    }

    [Fact]
    public void Popup_methods_read_and_update_popup_state()
    {
        RunOnStaThread(() =>
        {
            var popup = new Popup { IsOpen = false };

            Assert.False(CodingOverlayInputControls.IsPopupOpen(popup));

            CodingOverlayInputControls.OpenPopup(popup);

            Assert.True(CodingOverlayInputControls.IsPopupOpen(popup));

            CodingOverlayInputControls.ClosePopup(popup);

            Assert.False(CodingOverlayInputControls.IsPopupOpen(popup));
        });
    }

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
