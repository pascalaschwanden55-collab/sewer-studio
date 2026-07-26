using System.Windows;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerStateControllerTests
{
    [Fact]
    public void State_starts_inactive_without_preview()
    {
        var state = new CodingEingabemarkerStateController();

        Assert.Equal(CodingEingabemarkerPhase.Inactive, state.Phase);
        Assert.False(state.IsDrawing);
        Assert.False(state.HasPreview);
        Assert.Equal(CodingOverlayInputEingabemarkerState.Inactive, state.OverlayInputState);
    }

    [Fact]
    public void Drawing_phase_with_preview_reports_drawing_overlay_state()
    {
        RunOnStaThread(() =>
        {
            var state = new CodingEingabemarkerStateController();
            var preview = new Rectangle();

            state.SetDrawingPhase();
            state.SetPreview(preview);

            Assert.True(state.IsDrawing);
            Assert.True(state.HasPreview);
            Assert.Same(preview, state.PreviewRect);
            Assert.Equal(CodingOverlayInputEingabemarkerState.Drawing, state.OverlayInputState);
        });
    }

    [Fact]
    public void Input_and_analyzing_phase_block_overlay_input()
    {
        var state = new CodingEingabemarkerStateController();

        state.SetInputPhase();

        Assert.Equal(CodingOverlayInputEingabemarkerState.InputBlocked, state.OverlayInputState);

        state.SetAnalyzingPhase();

        Assert.Equal(CodingOverlayInputEingabemarkerState.InputBlocked, state.OverlayInputState);
    }

    [Fact]
    public void ClearPreview_removes_preview_reference()
    {
        RunOnStaThread(() =>
        {
            var state = new CodingEingabemarkerStateController();
            state.SetPreview(new Rectangle());

            state.ClearPreview();

            Assert.False(state.HasPreview);
            Assert.Null(state.PreviewRect);
        });
    }

    [Fact]
    public void Stores_drag_start_and_normalized_selection()
    {
        var state = new CodingEingabemarkerStateController();
        var dragStart = new Point(12, 34);
        var selection = new Rect(0.1, 0.2, 0.3, 0.4);

        state.StoreDragStart(dragStart);
        state.StoreNormalizedSelection(selection);

        Assert.Equal(dragStart, state.DragStart);
        Assert.Equal(selection, state.NormalizedSelection);
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
