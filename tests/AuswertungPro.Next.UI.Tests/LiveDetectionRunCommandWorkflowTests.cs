using System.Threading;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionRunCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_without_starting_when_tick_gate_blocks()
    {
        var calls = new List<string>();

        var result = await LiveDetectionRunCommandWorkflow.ExecuteAsync(
            Actions(
                calls,
                shouldRunTick: () =>
                {
                    calls.Add("should");
                    return false;
                },
                beginDetection: () => throw new InvalidOperationException("Begin should not run."),
                captureCurrentFrameAsync: () => throw new InvalidOperationException("Capture should not run.")));

        Assert.Equal(LiveDetectionRunCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Completed);
        Assert.Equal(["should"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_runs_snapshot_inference_result_and_finally_ends_detection()
    {
        var calls = new List<string>();
        var snapshot = new byte[] { 1, 2, 3 };
        var detection = Detection([Finding("Riss", 3)]);

        var result = await LiveDetectionRunCommandWorkflow.ExecuteAsync(
            Actions(
                calls,
                captureCurrentFrameAsync: () =>
                {
                    calls.Add("capture");
                    return Task.FromResult<byte[]?>(snapshot);
                },
                getTimestampSeconds: () =>
                {
                    calls.Add("time");
                    return 7.5d;
                },
                createAnalyzeFrameAsync: () =>
                {
                    calls.Add("create-analyze");
                    return (frame, timestamp, _) =>
                    {
                        calls.Add($"analyze:{frame.Length}:{timestamp:F2}");
                        Assert.Same(snapshot, frame);
                        Assert.Equal(7.5d, timestamp);
                        return Task.FromResult(detection);
                    };
                }));

        Assert.Equal(LiveDetectionRunCommandOutcome.Completed, result.Outcome);
        Assert.True(result.Completed);
        Assert.Equal(
            [
                "should",
                "begin",
                "badge:KI aktiv|qwen2.5-vl:7b | Snapshot",
                "capture",
                "time",
                "create-analyze",
                "badge:KI aktiv|qwen2.5-vl:7b | Inferenz",
                "analyze:3:7.50",
                "ui",
                "apply",
                "render:1:12.5",
                "status",
                "badge:KI aktiv|qwen2.5-vl:7b | Overlay",
                "store:1:12.5",
                "confirm:1",
                "badge:Befund erkannt|qwen2.5-vl:7b | Warte auf Bestaetigung",
                "end"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_dispatches_error_and_ends_detection_when_inference_fails()
    {
        var calls = new List<string>();

        var result = await LiveDetectionRunCommandWorkflow.ExecuteAsync(
            Actions(
                calls,
                createAnalyzeFrameAsync: () => (_, _, _) =>
                    throw new InvalidOperationException("kaputt")));

        Assert.Equal(LiveDetectionRunCommandOutcome.Failed, result.Outcome);
        Assert.False(result.Completed);
        Assert.Contains("error:kaputt", calls);
        Assert.Contains("badge:KI Fehler|qwen2.5-vl:7b", calls);
        Assert.Equal("end", calls[^1]);
    }

    private static LiveDetectionRunCommandActions Actions(
        List<string> calls,
        Func<bool>? shouldRunTick = null,
        Action? beginDetection = null,
        Action? endDetection = null,
        Func<Task<byte[]?>>? captureCurrentFrameAsync = null,
        Func<double>? getTimestampSeconds = null,
        Func<Func<byte[], double, CancellationToken, Task<LiveDetection>>?>? createAnalyzeFrameAsync = null)
        => new(
            ShouldRunTick: shouldRunTick ?? (() =>
            {
                calls.Add("should");
                return true;
            }),
            GetModelName: () => "models/qwen2.5-vl:7b",
            BeginDetection: beginDetection ?? (() => calls.Add("begin")),
            EndDetection: endDetection ?? (() => calls.Add("end")),
            CaptureCurrentFrameAsync: captureCurrentFrameAsync ?? (() =>
            {
                calls.Add("capture");
                return Task.FromResult<byte[]?>([1, 2, 3]);
            }),
            GetTimestampSeconds: getTimestampSeconds ?? (() =>
            {
                calls.Add("time");
                return 7.5d;
            }),
            GetDetectionCancellationToken: () => CancellationToken.None,
            CreateAnalyzeFrameAsync: createAnalyzeFrameAsync ?? (() =>
            {
                calls.Add("create-analyze");
                return (_, _, _) => Task.FromResult(Detection([]));
            }),
            IsClosing: () => false,
            IsPlaybackDisposed: () => false,
            IsDetecting: () => true,
            InvokeOnUi: action =>
            {
                calls.Add("ui");
                action();
            },
            ApplyDetectionResult: _ => calls.Add("apply"),
            RenderDetectionOverlay: (findings, timestamp) => calls.Add($"render:{findings.Count}:{timestamp:F1}"),
            UpdateDetectionStatus: _ => calls.Add("status"),
            SetLiveDetectionBadge: (status, _, detail) => calls.Add($"badge:{status}|{detail}"),
            StoreFindings: (findings, _, timestamp) => calls.Add($"store:{findings.Count}:{timestamp:F1}"),
            ShowDetectionConfirmation: findings => calls.Add($"confirm:{findings.Count}"),
            ShowDetectionError: message => calls.Add($"error:{message}"));

    private static LiveDetection Detection(IReadOnlyList<LiveFrameFinding> findings)
        => new(
            TimestampSeconds: 12.5,
            Findings: findings,
            MeterReading: null,
            Error: null);

    private static LiveFrameFinding Finding(string label, int severity)
        => new(label, severity, PositionClock: null, ExtentPercent: null);
}
