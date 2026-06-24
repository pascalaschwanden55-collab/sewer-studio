using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionErrorWorkflowTests
{
    [Fact]
    public void Execute_ignores_error_when_window_is_closing()
    {
        var result = LiveDetectionErrorWorkflow.Execute(
            new LiveDetectionErrorWorkflowRequest(
                Error: new InvalidOperationException("boom"),
                IsClosing: true,
                IsPlaybackDisposed: false,
                ModelName: "models/qwen2.5-vl:7b"),
            NoActions());

        Assert.Equal(LiveDetectionErrorWorkflowOutcome.Ignored, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Execute_shows_truncated_error_and_error_badge()
    {
        var calls = new List<string>();
        var longMessage = new string('x', 205);

        var result = LiveDetectionErrorWorkflow.Execute(
            new LiveDetectionErrorWorkflowRequest(
                Error: new InvalidOperationException(longMessage),
                IsClosing: false,
                IsPlaybackDisposed: false,
                ModelName: "models/qwen2.5-vl:7b"),
            new LiveDetectionErrorWorkflowActions(
                ShowDetectionError: message =>
                {
                    calls.Add($"error:{message.Length}:{message.EndsWith("...", StringComparison.Ordinal)}");
                    Assert.Equal(new string('x', 200) + "...", message);
                },
                SetLiveDetectionBadge: (status, color, stage) =>
                {
                    calls.Add($"badge:{status}|{stage}");
                    Assert.Equal(PlayerStatusColors.Error, color);
                }));

        Assert.Equal(
            ["error:203:True", "badge:KI Fehler|qwen2.5-vl:7b"],
            calls);
        Assert.Equal(LiveDetectionErrorWorkflowOutcome.Shown, result.Outcome);
        Assert.True(result.Handled);
    }

    private static LiveDetectionErrorWorkflowActions NoActions()
        => new(
            ShowDetectionError: _ => throw new InvalidOperationException("Error UI should not run."),
            SetLiveDetectionBadge: (_, _, _) => throw new InvalidOperationException("Badge UI should not run."));
}
