using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotWorkflowTests
{
    [Fact]
    public void TryTakeSnapshot_skips_without_active_player_window()
    {
        var result = PlayerSnapshotWorkflow.TryTakeSnapshot(
            new PlayerSnapshotRequest(
                HasPlayerWindow: false,
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsPlaying: true,
                CurrentTime: TimeSpan.FromSeconds(1)),
            new PlayerSnapshotActions(
                Capture: () => throw new InvalidOperationException("Capture should not run.")));

        Assert.Equal(PlayerSnapshotWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Captured);
        Assert.Equal(string.Empty, result.SnapshotPath);
    }

    [Fact]
    public void TryTakeSnapshot_skips_when_not_playing_without_current_time()
    {
        var result = PlayerSnapshotWorkflow.TryTakeSnapshot(
            new PlayerSnapshotRequest(
                HasPlayerWindow: true,
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsPlaying: false,
                CurrentTime: TimeSpan.Zero),
            new PlayerSnapshotActions(
                Capture: () => throw new InvalidOperationException("Capture should not run.")));

        Assert.Equal(PlayerSnapshotWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Captured);
    }

    [Fact]
    public void TryTakeSnapshot_returns_capture_path_when_capture_succeeds()
    {
        var result = PlayerSnapshotWorkflow.TryTakeSnapshot(
            new PlayerSnapshotRequest(
                HasPlayerWindow: true,
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsPlaying: false,
                CurrentTime: TimeSpan.FromSeconds(1)),
            new PlayerSnapshotActions(
                Capture: () => new PlayerSnapshotCaptureResult(true, "snapshot.png")));

        Assert.Equal(PlayerSnapshotWorkflowOutcome.Captured, result.Outcome);
        Assert.True(result.Captured);
        Assert.Equal("snapshot.png", result.SnapshotPath);
    }

    [Fact]
    public void TryTakeSnapshot_keeps_capture_path_when_capture_returns_false()
    {
        var result = PlayerSnapshotWorkflow.TryTakeSnapshot(
            new PlayerSnapshotRequest(
                HasPlayerWindow: true,
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsPlaying: false,
                CurrentTime: TimeSpan.FromSeconds(1)),
            new PlayerSnapshotActions(
                Capture: () => new PlayerSnapshotCaptureResult(false, "snapshot.png")));

        Assert.Equal(PlayerSnapshotWorkflowOutcome.Failed, result.Outcome);
        Assert.False(result.Captured);
        Assert.Equal("snapshot.png", result.SnapshotPath);
    }

    [Fact]
    public void TakeSnapshotSafe_skips_when_playback_is_unavailable()
    {
        var result = PlayerSnapshotWorkflow.TakeSnapshotSafe(
            new PlayerSnapshotSafeRequest(
                IsClosing: true,
                IsPlaybackDisposed: false),
            NoSafeActions());

        Assert.Equal(PlayerSnapshotWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Captured);
    }

    [Fact]
    public void TakeSnapshotSafe_pauses_disables_overlay_captures_and_resumes_in_order()
    {
        var calls = new List<string>();

        var result = PlayerSnapshotWorkflow.TakeSnapshotSafe(
            new PlayerSnapshotSafeRequest(
                IsClosing: false,
                IsPlaybackDisposed: false),
            SafeActions(calls, snapshotResult: true));

        Assert.Equal(["pause", "check", "disable", "snapshot", "resume:True"], calls);
        Assert.Equal(PlayerSnapshotWorkflowOutcome.Captured, result.Outcome);
        Assert.True(result.Captured);
    }

    [Fact]
    public void TakeSnapshotSafe_resumes_after_capture_exception()
    {
        var calls = new List<string>();

        var result = PlayerSnapshotWorkflow.TakeSnapshotSafe(
            new PlayerSnapshotSafeRequest(
                IsClosing: false,
                IsPlaybackDisposed: false),
            SafeActions(calls, throwOnSnapshot: true));

        Assert.Equal(["pause", "check", "disable", "snapshot", "resume:True"], calls);
        Assert.Equal(PlayerSnapshotWorkflowOutcome.Failed, result.Outcome);
        Assert.False(result.Captured);
    }

    private static PlayerSnapshotSafeActions SafeActions(
        List<string> calls,
        bool snapshotResult = false,
        bool throwOnSnapshot = false)
        => new(
            PauseIfPlaying: () =>
            {
                calls.Add("pause");
                return true;
            },
            IsPlaybackUnavailable: () =>
            {
                calls.Add("check");
                return false;
            },
            DisableMarqueeOverlay: () => calls.Add("disable"),
            TakeSnapshot: () =>
            {
                calls.Add("snapshot");
                if (throwOnSnapshot)
                    throw new InvalidOperationException("boom");
                return snapshotResult;
            },
            ResumeIfNeeded: wasPlaying => calls.Add($"resume:{wasPlaying}"));

    private static PlayerSnapshotSafeActions NoSafeActions()
        => new(
            PauseIfPlaying: () => throw new InvalidOperationException("Pause should not run."),
            IsPlaybackUnavailable: () => throw new InvalidOperationException("Check should not run."),
            DisableMarqueeOverlay: () => throw new InvalidOperationException("Disable should not run."),
            TakeSnapshot: () => throw new InvalidOperationException("Snapshot should not run."),
            ResumeIfNeeded: _ => throw new InvalidOperationException("Resume should not run."));
}
