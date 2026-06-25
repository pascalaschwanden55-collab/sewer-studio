using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkCompletionCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_stops_when_current_overlay_is_missing()
    {
        var calls = new List<string>();

        var result = await LiveDetectionManualMarkCompletionCommandWorkflow.ExecuteAsync<string>(
            Actions(
                calls,
                getCurrentOverlay: () =>
                {
                    calls.Add("overlay");
                    return null;
                }));

        Assert.Equal(LiveDetectionManualMarkCompletionCommandOutcome.MissingOverlay, result.Outcome);
        Assert.False(result.Completed);
        Assert.Equal(["overlay"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_segments_mask_overrides_clock_and_completes_after_save()
    {
        var calls = new List<string>();
        var overlay = Overlay();
        var frameBytes = new byte[] { 1, 2, 3 };

        var result = await LiveDetectionManualMarkCompletionCommandWorkflow.ExecuteAsync<string>(
            Actions(
                calls,
                getCurrentOverlay: () =>
                {
                    calls.Add("overlay");
                    return overlay;
                },
                getTimestampSeconds: () =>
                {
                    calls.Add("time");
                    return 12.5d;
                },
                captureCurrentFrameAsync: () =>
                {
                    calls.Add("capture");
                    return Task.FromResult<byte[]?>(frameBytes);
                },
                estimateClockPosition: selectedOverlay =>
                {
                    Assert.Same(overlay, selectedOverlay);
                    calls.Add("estimate");
                    return "3";
                },
                segmentMarkAsync: (selectedOverlay, capturedFrame) =>
                {
                    Assert.Same(overlay, selectedOverlay);
                    Assert.Same(frameBytes, capturedFrame);
                    calls.Add("segment");
                    return Task.FromResult<string?>("9");
                },
                getSegmentClockPosition: segment =>
                {
                    calls.Add($"clock:{segment}");
                    return segment;
                },
                showSegment: (segment, selectedOverlay) =>
                {
                    Assert.Same(overlay, selectedOverlay);
                    calls.Add($"show:{segment}");
                },
                delayAfterSegmentAsync: () =>
                {
                    calls.Add("delay");
                    return Task.CompletedTask;
                },
                saveTrainingAsync: (selectedOverlay, timestamp, clockPosition, capturedFrame) =>
                {
                    Assert.Same(overlay, selectedOverlay);
                    Assert.Equal(12.5d, timestamp);
                    Assert.Equal("9", clockPosition);
                    Assert.Same(frameBytes, capturedFrame);
                    calls.Add($"save:{clockPosition}");
                    return Task.FromResult(true);
                },
                completeManualMark: saved => calls.Add($"complete:{saved}")));

        Assert.Equal(LiveDetectionManualMarkCompletionCommandOutcome.Completed, result.Outcome);
        Assert.True(result.Completed);
        Assert.Equal(
            ["overlay", "time", "capture", "estimate", "segment", "clock:9", "show:9", "delay", "save:9", "complete:True"],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_uses_estimated_clock_without_segmentation()
    {
        var calls = new List<string>();
        var overlay = Overlay();

        var result = await LiveDetectionManualMarkCompletionCommandWorkflow.ExecuteAsync<string>(
            Actions(
                calls,
                getCurrentOverlay: () => overlay,
                segmentMarkAsync: (_, _) =>
                {
                    calls.Add("segment");
                    return Task.FromResult<string?>(null);
                },
                saveTrainingAsync: (_, _, clockPosition, _) =>
                {
                    calls.Add($"save:{clockPosition}");
                    return Task.FromResult(false);
                },
                completeManualMark: saved => calls.Add($"complete:{saved}")));

        Assert.Equal(LiveDetectionManualMarkCompletionCommandOutcome.Completed, result.Outcome);
        Assert.True(result.Completed);
        Assert.Equal(["time", "capture", "estimate", "segment", "save:3", "complete:False"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_traces_error_without_completion_when_save_fails()
    {
        var calls = new List<string>();

        var result = await LiveDetectionManualMarkCompletionCommandWorkflow.ExecuteAsync<string>(
            Actions(
                calls,
                saveTrainingAsync: (_, _, _, _) => throw new InvalidOperationException("kaputt")));

        Assert.Equal(LiveDetectionManualMarkCompletionCommandOutcome.Failed, result.Outcome);
        Assert.False(result.Completed);
        Assert.Equal(["time", "capture", "estimate", "segment"], calls.Take(4));
        var trace = Assert.Single(calls.Skip(4));
        Assert.StartsWith("trace:[PlayerWindow] HandleMarkDrawingComplete error:", trace);
        Assert.Contains("kaputt", trace);
    }

    private static LiveDetectionManualMarkCompletionCommandActions<string> Actions(
        List<string> calls,
        Func<OverlayGeometry?>? getCurrentOverlay = null,
        Func<double>? getTimestampSeconds = null,
        Func<Task<byte[]?>>? captureCurrentFrameAsync = null,
        Func<OverlayGeometry, string?>? estimateClockPosition = null,
        Func<OverlayGeometry, byte[]?, Task<string?>>? segmentMarkAsync = null,
        Func<string, string?>? getSegmentClockPosition = null,
        Action<string, OverlayGeometry>? showSegment = null,
        Func<Task>? delayAfterSegmentAsync = null,
        Func<OverlayGeometry, double, string?, byte[]?, Task<bool>>? saveTrainingAsync = null,
        Action<bool>? completeManualMark = null,
        Action<string>? traceError = null)
        => new(
            GetCurrentOverlay: getCurrentOverlay ?? (() => Overlay()),
            GetTimestampSeconds: getTimestampSeconds ?? (() =>
            {
                calls.Add("time");
                return 1d;
            }),
            CaptureCurrentFrameAsync: captureCurrentFrameAsync ?? (() =>
            {
                calls.Add("capture");
                return Task.FromResult<byte[]?>([7]);
            }),
            EstimateClockPosition: estimateClockPosition ?? (_ =>
            {
                calls.Add("estimate");
                return "3";
            }),
            SegmentMarkAsync: segmentMarkAsync ?? ((_, _) =>
            {
                calls.Add("segment");
                return Task.FromResult<string?>(null);
            }),
            GetSegmentClockPosition: getSegmentClockPosition ?? (_ => null),
            ShowSegment: showSegment ?? ((_, _) => calls.Add("show")),
            DelayAfterSegmentAsync: delayAfterSegmentAsync ?? (() =>
            {
                calls.Add("delay");
                return Task.CompletedTask;
            }),
            SaveTrainingAsync: saveTrainingAsync ?? ((_, _, clockPosition, _) =>
            {
                calls.Add($"save:{clockPosition}");
                return Task.FromResult(true);
            }),
            CompleteManualMark: completeManualMark ?? (saved => calls.Add($"complete:{saved}")),
            TraceError: traceError ?? (message => calls.Add($"trace:[PlayerWindow] HandleMarkDrawingComplete error: {message}")));

    private static OverlayGeometry Overlay()
        => new()
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.3, 0.4)]
        };
}
