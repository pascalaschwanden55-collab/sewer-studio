using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionTickStartWorkflowTests
{
    [Fact]
    public void Start_skips_without_actions_when_timer_gate_blocks_tick()
    {
        var result = LiveDetectionTickStartWorkflow.Start(
            new LiveDetectionTickStartWorkflowRequest(
                ShouldRunTick: false,
                ModelName: "models/qwen2.5-vl:7b"),
            NoActions());

        Assert.Equal(LiveDetectionTickStartWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Started);
    }

    [Fact]
    public void Start_begins_detection_before_snapshot_badge_when_gate_allows_tick()
    {
        var calls = new List<string>();

        var result = LiveDetectionTickStartWorkflow.Start(
            new LiveDetectionTickStartWorkflowRequest(
                ShouldRunTick: true,
                ModelName: "models/qwen2.5-vl:7b"),
            new LiveDetectionTickStartWorkflowActions(
                BeginDetection: () => calls.Add("begin"),
                SetLiveDetectionBadge: (status, color, stage) =>
                {
                    calls.Add($"badge:{status}|{stage}");
                    Assert.Equal(PlayerStatusColors.Warning, color);
                }));

        Assert.Equal(
            ["begin", "badge:KI aktiv|qwen2.5-vl:7b | Snapshot"],
            calls);
        Assert.Equal(LiveDetectionTickStartWorkflowOutcome.Started, result.Outcome);
        Assert.True(result.Started);
    }

    private static LiveDetectionTickStartWorkflowActions NoActions()
        => new(
            BeginDetection: () => throw new InvalidOperationException("BeginDetection should not run."),
            SetLiveDetectionBadge: (_, _, _) => throw new InvalidOperationException("Badge should not run."));
}
