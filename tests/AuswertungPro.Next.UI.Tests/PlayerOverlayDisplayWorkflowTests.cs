using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerOverlayDisplayWorkflowTests
{
    [Fact]
    public void Show_offers_host_actions_overload()
    {
        var overload = typeof(PlayerOverlayDisplayWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(PlayerOverlayDisplayWorkflow.Show) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(
                    [
                        typeof(PlayerOverlayDisplayWorkflowRequest),
                        typeof(PlayerOverlayDisplayHostActions),
                    ]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void Show_skips_when_playback_is_disposed()
    {
        var result = PlayerOverlayDisplayWorkflow.Show(
            new PlayerOverlayDisplayWorkflowRequest(
                IsPlaybackDisposed: true,
                Text: "Start",
                Duration: TimeSpan.FromSeconds(3)),
            new PlayerOverlayDisplayWorkflowActions(
                ShowMarquee: _ => throw new InvalidOperationException("Show should not run."),
                ScheduleDisable: (_, _) => throw new InvalidOperationException("Schedule should not run."),
                DisableMarquee: () => throw new InvalidOperationException("Disable should not run.")));

        Assert.Equal(PlayerOverlayDisplayWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Show_displays_marquee_and_schedules_disable_in_order()
    {
        var calls = new List<string>();

        var result = PlayerOverlayDisplayWorkflow.Show(
            new PlayerOverlayDisplayWorkflowRequest(
                IsPlaybackDisposed: false,
                Text: "Bereit",
                Duration: TimeSpan.FromSeconds(4)),
            new PlayerOverlayDisplayWorkflowActions(
                ShowMarquee: marquee => calls.Add($"show:{marquee.Text}:{marquee.X}:{marquee.Y}"),
                ScheduleDisable: (duration, disable) =>
                {
                    calls.Add($"schedule:{duration.TotalSeconds:0}");
                    disable();
                },
                DisableMarquee: () => calls.Add("disable")));

        Assert.Equal(["show:Bereit:16:16", "schedule:4", "disable"], calls);
        Assert.Equal(PlayerOverlayDisplayWorkflowOutcome.Shown, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Show_ignores_overlay_errors()
    {
        var result = PlayerOverlayDisplayWorkflow.Show(
            new PlayerOverlayDisplayWorkflowRequest(
                IsPlaybackDisposed: false,
                Text: "Start",
                Duration: TimeSpan.FromSeconds(3)),
            new PlayerOverlayDisplayWorkflowActions(
                ShowMarquee: _ => throw new InvalidOperationException("VLC failed."),
                ScheduleDisable: (_, _) => throw new InvalidOperationException("Schedule should not run."),
                DisableMarquee: () => throw new InvalidOperationException("Disable should not run.")));

        Assert.Equal(PlayerOverlayDisplayWorkflowOutcome.IgnoredError, result.Outcome);
        Assert.False(result.Handled);
    }
}
