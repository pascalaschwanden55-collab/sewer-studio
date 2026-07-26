using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionConfirmationDisplayWorkflowTests
{
    [Fact]
    public void Show_ignores_empty_findings_without_actions()
    {
        var result = LiveDetectionConfirmationDisplayWorkflow.Show(
            new LiveDetectionConfirmationShowRequest(
                Findings: [],
                IsPlaybackDisposed: false,
                IsPlayerPlaying: true,
                TimestampSeconds: 12.5),
            NoShowActions());

        Assert.Equal(LiveDetectionConfirmationDisplayWorkflowOutcome.Ignored, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Show_pauses_seeks_and_displays_confirmation_in_order()
    {
        var calls = new List<string>();
        var findings = new[] { Finding("Riss"), Finding("Wurzel") };

        var result = LiveDetectionConfirmationDisplayWorkflow.Show(
            new LiveDetectionConfirmationShowRequest(
                Findings: findings,
                IsPlaybackDisposed: false,
                IsPlayerPlaying: true,
                TimestampSeconds: 12.5),
            new LiveDetectionConfirmationShowActions(
                SetPause: pause => calls.Add($"pause:{pause}"),
                SeekMilliseconds: ms => calls.Add($"seek:{ms}"),
                ShowConfirmation: shown => calls.Add($"show:{shown.Count}")));

        Assert.Equal(["pause:True", "seek:12500", "show:2"], calls);
        Assert.Equal(LiveDetectionConfirmationDisplayWorkflowOutcome.Shown, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Show_skips_pause_when_playback_is_disposed()
    {
        var calls = new List<string>();

        LiveDetectionConfirmationDisplayWorkflow.Show(
            new LiveDetectionConfirmationShowRequest(
                Findings: [Finding("Riss")],
                IsPlaybackDisposed: true,
                IsPlayerPlaying: true,
                TimestampSeconds: null),
            new LiveDetectionConfirmationShowActions(
                SetPause: pause => calls.Add($"pause:{pause}"),
                SeekMilliseconds: ms => calls.Add($"seek:{ms}"),
                ShowConfirmation: shown => calls.Add($"show:{shown.Count}")));

        Assert.Equal(["show:1"], calls);
    }

    [Fact]
    public void Resume_clears_hides_and_restarts_playback_when_stopped()
    {
        var calls = new List<string>();

        var result = LiveDetectionConfirmationDisplayWorkflow.Resume(
            new LiveDetectionConfirmationResumeRequest(IsPlayerPlaying: false),
            new LiveDetectionConfirmationResumeActions(
                ClearBuffer: () => calls.Add("clear"),
                HideConfirmation: () => calls.Add("hide"),
                Play: () => calls.Add("play")));

        Assert.Equal(["clear", "hide", "play"], calls);
        Assert.Equal(LiveDetectionConfirmationDisplayWorkflowOutcome.Resumed, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Resume_does_not_restart_playback_when_already_playing()
    {
        var calls = new List<string>();

        LiveDetectionConfirmationDisplayWorkflow.Resume(
            new LiveDetectionConfirmationResumeRequest(IsPlayerPlaying: true),
            new LiveDetectionConfirmationResumeActions(
                ClearBuffer: () => calls.Add("clear"),
                HideConfirmation: () => calls.Add("hide"),
                Play: () => calls.Add("play")));

        Assert.Equal(["clear", "hide"], calls);
    }

    private static LiveDetectionConfirmationShowActions NoShowActions()
        => new(
            SetPause: _ => throw new InvalidOperationException("Pause should not run."),
            SeekMilliseconds: _ => throw new InvalidOperationException("Seek should not run."),
            ShowConfirmation: _ => throw new InvalidOperationException("Show should not run."));

    private static LiveFrameFinding Finding(string label)
        => new(label, Severity: 3, PositionClock: null, ExtentPercent: null);
}
