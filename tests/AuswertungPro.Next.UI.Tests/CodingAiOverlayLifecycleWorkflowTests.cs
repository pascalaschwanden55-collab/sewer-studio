using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiOverlayLifecycleWorkflowTests
{
    [Fact]
    public void ScheduleAutoHide_offers_host_actions_overload()
    {
        var overload = typeof(CodingAiOverlayLifecycleWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingAiOverlayLifecycleWorkflow.ScheduleAutoHide) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(
                    [
                        typeof(CodingAiOverlayAutoHideRequest),
                        typeof(CodingAiOverlayAutoHideHostActions),
                    ]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void FadeOutAfterAction_offers_host_actions_overload()
    {
        var overload = typeof(CodingAiOverlayLifecycleWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingAiOverlayLifecycleWorkflow.FadeOutAfterAction) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual([typeof(CodingAiOverlayFadeOutHostActions)]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void ScheduleAutoHide_creates_timer_when_missing_and_restarts_it()
    {
        var calls = new List<string>();
        Action? clearAction = null;

        var result = CodingAiOverlayLifecycleWorkflow.ScheduleAutoHide(
            new CodingAiOverlayAutoHideRequest(HasTimer: false),
            new CodingAiOverlayAutoHideActions(
                CreateTimer: (delay, clear) =>
                {
                    calls.Add($"create:{delay.TotalSeconds:0}");
                    clearAction = clear;
                },
                StopTimer: () => calls.Add("stop"),
                StartTimer: () => calls.Add("start"),
                ClearVisuals: () => calls.Add("clear")));

        clearAction!();

        Assert.Equal(["create:3", "stop", "start", "clear"], calls);
        Assert.Equal(CodingAiOverlayLifecycleWorkflowOutcome.TimerCreated, result.Outcome);
        Assert.True(result.CreatedTimer);
    }

    [Fact]
    public void ScheduleAutoHide_restarts_existing_timer_without_creating_it()
    {
        var calls = new List<string>();

        var result = CodingAiOverlayLifecycleWorkflow.ScheduleAutoHide(
            new CodingAiOverlayAutoHideRequest(HasTimer: true),
            new CodingAiOverlayAutoHideActions(
                CreateTimer: (_, _) => throw new InvalidOperationException("Timer should not be created."),
                StopTimer: () => calls.Add("stop"),
                StartTimer: () => calls.Add("start"),
                ClearVisuals: () => throw new InvalidOperationException("Clear should run only from timer.")));

        Assert.Equal(["stop", "start"], calls);
        Assert.Equal(CodingAiOverlayLifecycleWorkflowOutcome.TimerRestarted, result.Outcome);
        Assert.False(result.CreatedTimer);
    }

    [Fact]
    public void FadeOutAfterAction_renders_then_schedules_clear_after_delay()
    {
        var calls = new List<string>();
        Action? clearAction = null;

        var result = CodingAiOverlayLifecycleWorkflow.FadeOutAfterAction(
            new CodingAiOverlayFadeOutActions(
                RenderAiOverlays: () => calls.Add("render"),
                ScheduleClear: (delay, clear) =>
                {
                    calls.Add($"schedule:{delay.TotalMilliseconds:0}");
                    clearAction = clear;
                },
                ClearAiOverlays: () => calls.Add("clear")));

        clearAction!();

        Assert.Equal(["render", "schedule:800", "clear"], calls);
        Assert.Equal(CodingAiOverlayLifecycleWorkflowOutcome.FadeOutScheduled, result.Outcome);
    }
}
