using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCalibrationPointerControllerTests
{
    [Fact]
    public void Start_is_side_effect_free_when_calibration_is_inactive()
    {
        var calls = new List<string>();
        var controller = CreateController(new CodingCalibrationStateController(), calls);

        var handled = controller.Start(new NormalizedPoint(0.2, 0.3));

        Assert.False(handled);
        Assert.Empty(calls);
    }

    [Fact]
    public void Active_pointer_sequence_stores_start_previews_and_applies_same_points()
    {
        var calls = new List<string>();
        var state = new CodingCalibrationStateController();
        state.SetCalibrating(true);
        var controller = CreateController(state, calls);
        var start = new NormalizedPoint(0.2, 0.3);
        var end = new NormalizedPoint(0.7, 0.8);

        var startHandled = controller.Start(start);
        var previewHandled = controller.Preview(end);
        var finishHandled = controller.Finish(end);

        Assert.True(startHandled);
        Assert.True(previewHandled);
        Assert.True(finishHandled);
        Assert.Equal(start, state.Start);
        Assert.Equal(
            [
                "capture",
                "clear-canvas",
                "ai",
                "reference",
                "clear-canvas",
                "ai",
                "reference",
                "render:0.2,0.3->0.7,0.8",
                "preview:Referenzlinie",
                "release",
                "apply:0.2,0.3->0.7,0.8"
            ],
            calls);
    }

    private static CodingCalibrationPointerController CreateController(
        CodingCalibrationStateController state,
        ICollection<string> calls)
        => new(
            state,
            new CodingCalibrationPointerControllerActions(
                CaptureMouse: () => calls.Add("capture"),
                ReleaseMouseCapture: () => calls.Add("release"),
                ClearTransientCodingCanvas: () => calls.Add("clear-canvas"),
                RenderAiOverlays: () => calls.Add("ai"),
                RenderReferenceDn: () => calls.Add("reference"),
                RenderPreview: (start, end) =>
                {
                    calls.Add($"render:{Format(start)}->{Format(end)}");
                    return new CodingCalibrationPreviewState(
                        new Point(start.X, start.Y),
                        new Point(end.X, end.Y),
                        1,
                        "Referenzlinie");
                },
                ApplyPreview: preview => calls.Add($"preview:{preview.HintText}"),
                ApplyCalibration: (start, end) => calls.Add($"apply:{Format(start)}->{Format(end)}")));

    private static string Format(NormalizedPoint point)
        => FormattableString.Invariant($"{point.X:0.0},{point.Y:0.0}");
}
