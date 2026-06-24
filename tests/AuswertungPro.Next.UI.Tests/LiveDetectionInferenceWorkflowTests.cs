using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionInferenceWorkflowTests
{
    public static TheoryData<bool, bool, bool, bool> BlockedCases()
        => new()
        {
            { true, false, true, true },
            { false, true, true, true },
            { false, false, false, true },
            { false, false, true, false }
        };

    [Theory]
    [MemberData(nameof(BlockedCases))]
    public async Task ExecuteAsync_skips_without_actions_when_inference_gate_blocks(
        bool isClosing,
        bool isPlaybackDisposed,
        bool hasAnalyzer,
        bool hasCancellation)
    {
        var result = await LiveDetectionInferenceWorkflow.ExecuteAsync(
            new LiveDetectionInferenceWorkflowRequest(
                Snapshot: [1, 2, 3],
                TimestampSeconds: 4.25,
                IsClosing: isClosing,
                IsPlaybackDisposed: isPlaybackDisposed,
                ModelName: "models/qwen2.5-vl:7b",
                CancellationToken: hasCancellation ? CancellationToken.None : null),
            new LiveDetectionInferenceWorkflowActions(
                AnalyzeFrameAsync: hasAnalyzer
                    ? (_, _, _) => throw new InvalidOperationException("Analysis should not run.")
                    : null,
                SetLiveDetectionBadge: (_, _, _) => throw new InvalidOperationException("Badge should not run.")));

        Assert.Equal(LiveDetectionInferenceWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.HasResult);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ExecuteAsync_sets_inference_badge_before_analyzing_frame()
    {
        var calls = new List<string>();
        var snapshot = new byte[] { 1, 2, 3 };
        using var cts = new CancellationTokenSource();
        var detection = new LiveDetection(
            TimestampSeconds: 4.25,
            Findings: [],
            MeterReading: null,
            Error: null);

        var result = await LiveDetectionInferenceWorkflow.ExecuteAsync(
            new LiveDetectionInferenceWorkflowRequest(
                Snapshot: snapshot,
                TimestampSeconds: 4.25,
                IsClosing: false,
                IsPlaybackDisposed: false,
                ModelName: "models/qwen2.5-vl:7b",
                CancellationToken: cts.Token),
            new LiveDetectionInferenceWorkflowActions(
                AnalyzeFrameAsync: (frame, timestamp, cancellation) =>
                {
                    calls.Add($"analyze:{frame.Length}:{timestamp:F2}");
                    Assert.Same(snapshot, frame);
                    Assert.Equal(4.25, timestamp);
                    Assert.Equal(cts.Token, cancellation);
                    return Task.FromResult(detection);
                },
                SetLiveDetectionBadge: (status, color, stage) =>
                {
                    calls.Add($"badge:{status}|{stage}");
                    Assert.Equal(PlayerStatusColors.Warning, color);
                }));

        Assert.Equal(
            ["badge:KI aktiv|qwen2.5-vl:7b | Inferenz", "analyze:3:4.25"],
            calls);
        Assert.Equal(LiveDetectionInferenceWorkflowOutcome.Completed, result.Outcome);
        Assert.True(result.HasResult);
        Assert.Same(detection, result.Result);
    }
}
