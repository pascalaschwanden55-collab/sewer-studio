using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCalibrationPointerWorkflowTests
{
    [Fact]
    public void Start_skips_when_not_calibrating()
    {
        var result = CodingCalibrationPointerWorkflow.Start(
            new CodingCalibrationPointerStartRequest(IsCalibrating: false),
            StartActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingCalibrationPointerWorkflowOutcome.NotCalibrating, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Start_sets_start_and_rerenders_reference_overlays()
    {
        var calls = new List<string>();

        var result = CodingCalibrationPointerWorkflow.Start(
            new CodingCalibrationPointerStartRequest(IsCalibrating: true),
            StartActions(calls.Add));

        Assert.Equal(CodingCalibrationPointerWorkflowOutcome.Started, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            [
                "set-start",
                "capture",
                "clear",
                "ai",
                "reference-dn"
            ],
            calls);
    }

    [Theory]
    [InlineData(false, true, CodingCalibrationPointerWorkflowOutcome.NotCalibrating)]
    [InlineData(true, false, CodingCalibrationPointerWorkflowOutcome.MissingStart)]
    public void Preview_skips_without_active_start(
        bool isCalibrating,
        bool hasCalibrationStart,
        CodingCalibrationPointerWorkflowOutcome expectedOutcome)
    {
        var result = CodingCalibrationPointerWorkflow.Preview(
            new CodingCalibrationPointerPreviewRequest(isCalibrating, hasCalibrationStart),
            PreviewActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Preview_rerenders_preview_after_reference_overlays()
    {
        var calls = new List<string>();

        var result = CodingCalibrationPointerWorkflow.Preview(
            new CodingCalibrationPointerPreviewRequest(
                IsCalibrating: true,
                HasCalibrationStart: true),
            PreviewActions(calls.Add));

        Assert.Equal(CodingCalibrationPointerWorkflowOutcome.Previewed, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            [
                "clear",
                "ai",
                "reference-dn",
                "preview"
            ],
            calls);
    }

    [Theory]
    [InlineData(false, true, CodingCalibrationPointerWorkflowOutcome.NotCalibrating)]
    [InlineData(true, false, CodingCalibrationPointerWorkflowOutcome.MissingStart)]
    public void Finish_skips_without_active_start(
        bool isCalibrating,
        bool hasCalibrationStart,
        CodingCalibrationPointerWorkflowOutcome expectedOutcome)
    {
        var result = CodingCalibrationPointerWorkflow.Finish(
            new CodingCalibrationPointerFinishRequest(isCalibrating, hasCalibrationStart),
            FinishActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Finish_releases_mouse_and_applies_calibration()
    {
        var calls = new List<string>();

        var result = CodingCalibrationPointerWorkflow.Finish(
            new CodingCalibrationPointerFinishRequest(
                IsCalibrating: true,
                HasCalibrationStart: true),
            FinishActions(calls.Add));

        Assert.Equal(CodingCalibrationPointerWorkflowOutcome.Finished, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["release", "apply"], calls);
    }

    private static CodingCalibrationPointerStartActions StartActions(Action<string> calls)
        => new(
            SetCalibrationStart: () => calls("set-start"),
            CaptureMouse: () => calls("capture"),
            ClearTransientCodingCanvas: () => calls("clear"),
            RenderAiOverlays: () => calls("ai"),
            RenderReferenceDn: () => calls("reference-dn"));

    private static CodingCalibrationPointerPreviewActions PreviewActions(Action<string> calls)
        => new(
            ClearTransientCodingCanvas: () => calls("clear"),
            RenderAiOverlays: () => calls("ai"),
            RenderReferenceDn: () => calls("reference-dn"),
            RenderPreview: () => calls("preview"));

    private static CodingCalibrationPointerFinishActions FinishActions(Action<string> calls)
        => new(
            ReleaseMouseCapture: () => calls("release"),
            ApplyCalibration: () => calls("apply"));
}
