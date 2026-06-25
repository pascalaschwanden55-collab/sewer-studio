using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerUiUpdateWorkflowTests
{
    [Fact]
    public void Execute_skips_updates_while_dragging()
    {
        var result = PlayerUiUpdateWorkflow.Execute(
            new PlayerUiUpdateWorkflowRequest(
                IsDragging: true,
                IsCodingMode: true,
                CurrentTimeMs: 1000,
                DurationMs: 2000),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(PlayerUiUpdateWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Updated);
    }

    [Fact]
    public void Execute_updates_playback_state_and_rate_label()
    {
        var calls = new List<string>();

        var result = PlayerUiUpdateWorkflow.Execute(
            new PlayerUiUpdateWorkflowRequest(
                IsDragging: false,
                IsCodingMode: false,
                CurrentTimeMs: 1000,
                DurationMs: 5000),
            Actions(calls.Add));

        Assert.Equal(["playback:1000:5000", "rate"], calls);
        Assert.Equal(PlayerUiUpdateWorkflowOutcome.Updated, result.Outcome);
        Assert.True(result.Updated);
    }

    [Fact]
    public void Execute_updates_current_code_after_rate_label_in_coding_mode()
    {
        var calls = new List<string>();

        PlayerUiUpdateWorkflow.Execute(
            new PlayerUiUpdateWorkflowRequest(
                IsDragging: false,
                IsCodingMode: true,
                CurrentTimeMs: 3000,
                DurationMs: 9000),
            Actions(calls.Add));

        Assert.Equal(["playback:3000:9000", "rate", "coding"], calls);
    }

    private static PlayerUiUpdateWorkflowActions Actions(Action<string> call)
        => new(
            ApplyPlaybackState: (current, duration) => call($"playback:{current}:{duration}"),
            UpdateRateLabel: () => call("rate"),
            UpdateCodingCurrentCode: () => call("coding"));
}
