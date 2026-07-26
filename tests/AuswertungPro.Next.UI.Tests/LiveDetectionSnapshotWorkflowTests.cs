using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionSnapshotWorkflowTests
{
    [Fact]
    public void Handle_returns_available_without_actions_when_snapshot_exists()
    {
        var snapshot = new byte[] { 1, 2, 3 };

        var result = LiveDetectionSnapshotWorkflow.Handle(
            new LiveDetectionSnapshotWorkflowRequest(
                Snapshot: snapshot,
                IsClosing: false,
                IsPlaybackDisposed: false,
                ModelName: "models/qwen2.5-vl:7b"),
            NoActions());

        Assert.Equal(LiveDetectionSnapshotWorkflowOutcome.Available, result.Outcome);
        Assert.True(result.HasSnapshot);
        Assert.Same(snapshot, result.Snapshot);
    }

    [Fact]
    public void Handle_missing_snapshot_ends_detection_and_sets_ready_badge_when_active()
    {
        var calls = new List<string>();

        var result = LiveDetectionSnapshotWorkflow.Handle(
            new LiveDetectionSnapshotWorkflowRequest(
                Snapshot: null,
                IsClosing: false,
                IsPlaybackDisposed: false,
                ModelName: "models/qwen2.5-vl:7b"),
            new LiveDetectionSnapshotWorkflowActions(
                EndDetection: () => calls.Add("end"),
                SetLiveDetectionBadge: (status, color, stage) =>
                {
                    calls.Add($"badge:{status}|{stage}");
                    Assert.Equal(PlayerStatusColors.Success, color);
                }));

        Assert.Equal(
            ["end", "badge:KI aktiv|qwen2.5-vl:7b | Bereit"],
            calls);
        Assert.Equal(LiveDetectionSnapshotWorkflowOutcome.Missing, result.Outcome);
        Assert.False(result.HasSnapshot);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void Handle_missing_snapshot_does_not_set_ready_badge_while_closing()
    {
        var calls = new List<string>();

        var result = LiveDetectionSnapshotWorkflow.Handle(
            new LiveDetectionSnapshotWorkflowRequest(
                Snapshot: null,
                IsClosing: true,
                IsPlaybackDisposed: false,
                ModelName: "models/qwen2.5-vl:7b"),
            new LiveDetectionSnapshotWorkflowActions(
                EndDetection: () => calls.Add("end"),
                SetLiveDetectionBadge: (_, _, _) => throw new InvalidOperationException("Badge should not be updated.")));

        Assert.Equal(["end"], calls);
        Assert.Equal(LiveDetectionSnapshotWorkflowOutcome.Missing, result.Outcome);
        Assert.False(result.HasSnapshot);
    }

    private static LiveDetectionSnapshotWorkflowActions NoActions()
        => new(
            EndDetection: () => throw new InvalidOperationException("EndDetection should not run."),
            SetLiveDetectionBadge: (_, _, _) => throw new InvalidOperationException("Badge should not run."));
}
