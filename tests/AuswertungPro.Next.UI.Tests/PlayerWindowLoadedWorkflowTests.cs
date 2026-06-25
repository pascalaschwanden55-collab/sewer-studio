using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLoadedWorkflowTests
{
    [Fact]
    public void Execute_runs_loaded_startup_without_initial_overlay()
    {
        var calls = new List<string>();

        var result = PlayerWindowLoadedWorkflow.Execute(
            new PlayerWindowLoadedWorkflowRequest(
                InitialOverlayText: "   ",
                InitialOverlayDuration: TimeSpan.FromSeconds(6)),
            Actions(calls));

        Assert.Equal(
            [
                "play",
                "viewport",
                "schedule-viewport",
                "build-markers",
                "focusable",
                "schedule-focus"
            ],
            calls);
        Assert.Equal(PlayerWindowLoadedWorkflowOutcome.Loaded, result.Outcome);
        Assert.False(result.ShowedInitialOverlay);
    }

    [Fact]
    public void Execute_shows_initial_overlay_before_building_markers()
    {
        var calls = new List<string>();

        var result = PlayerWindowLoadedWorkflow.Execute(
            new PlayerWindowLoadedWorkflowRequest(
                InitialOverlayText: "Start",
                InitialOverlayDuration: TimeSpan.FromSeconds(6)),
            Actions(calls));

        Assert.Equal(
            [
                "play",
                "viewport",
                "schedule-viewport",
                "overlay:Start:6",
                "build-markers",
                "focusable",
                "schedule-focus"
            ],
            calls);
        Assert.Equal(PlayerWindowLoadedWorkflowOutcome.Loaded, result.Outcome);
        Assert.True(result.ShowedInitialOverlay);
    }

    private static PlayerWindowLoadedWorkflowActions Actions(List<string> calls)
        => new(
            Play: () => calls.Add("play"),
            UpdateCodingOverlayViewport: () => calls.Add("viewport"),
            ScheduleLoadedViewportUpdate: () => calls.Add("schedule-viewport"),
            ShowOverlay: (text, duration) => calls.Add($"overlay:{text}:{duration.TotalSeconds:0}"),
            BuildDamageMarkerTimeline: () => calls.Add("build-markers"),
            EnableFocusable: () => calls.Add("focusable"),
            ScheduleFocusWindow: () => calls.Add("schedule-focus"));
}
