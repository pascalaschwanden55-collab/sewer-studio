using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPipelineHealthChangeWorkflowTests
{
    [Fact]
    public void Execute_ignores_event_when_window_is_closing()
    {
        var calls = new List<string>();

        var result = CodingPipelineHealthChangeWorkflow.Execute(
            new CodingPipelineHealthChangeWorkflowRequest(
                IsClosing: true,
                DispatcherHasShutdownStarted: false,
                HasDispatcherAccess: true),
            Actions(calls));

        Assert.Equal(CodingPipelineHealthChangeWorkflowOutcome.Ignored, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_dispatches_to_ui_thread_and_rechecks_runtime_state()
    {
        var calls = new List<string>();
        Action? dispatched = null;

        var result = CodingPipelineHealthChangeWorkflow.Execute(
            new CodingPipelineHealthChangeWorkflowRequest(
                IsClosing: false,
                DispatcherHasShutdownStarted: false,
                HasDispatcherAccess: false),
            Actions(
                calls,
                dispatch: action =>
                {
                    calls.Add("dispatch");
                    dispatched = action;
                }));

        Assert.Equal(CodingPipelineHealthChangeWorkflowOutcome.Dispatched, result.Outcome);
        Assert.Equal(["dispatch"], calls);

        dispatched!.Invoke();

        Assert.Equal(["dispatch", "should-apply", "apply"], calls);
    }

    [Fact]
    public void Execute_applies_immediately_when_on_ui_thread_and_monitor_is_active()
    {
        var calls = new List<string>();

        var result = CodingPipelineHealthChangeWorkflow.Execute(
            new CodingPipelineHealthChangeWorkflowRequest(
                IsClosing: false,
                DispatcherHasShutdownStarted: false,
                HasDispatcherAccess: true),
            Actions(calls));

        Assert.Equal(CodingPipelineHealthChangeWorkflowOutcome.Applied, result.Outcome);
        Assert.Equal(["should-apply", "apply"], calls);
    }

    [Fact]
    public void Execute_skips_apply_when_coding_mode_or_monitor_is_no_longer_active()
    {
        var calls = new List<string>();

        var result = CodingPipelineHealthChangeWorkflow.Execute(
            new CodingPipelineHealthChangeWorkflowRequest(
                IsClosing: false,
                DispatcherHasShutdownStarted: false,
                HasDispatcherAccess: true),
            Actions(calls, shouldApply: () =>
            {
                calls.Add("should-apply");
                return false;
            }));

        Assert.Equal(CodingPipelineHealthChangeWorkflowOutcome.Ignored, result.Outcome);
        Assert.Equal(["should-apply"], calls);
    }

    private static CodingPipelineHealthChangeWorkflowActions Actions(
        List<string> calls,
        Func<bool>? shouldApply = null,
        Action<Action>? dispatch = null)
        => new(
            ShouldApply: shouldApply ?? (() =>
            {
                calls.Add("should-apply");
                return true;
            }),
            DispatchToUi: dispatch ?? (_ => calls.Add("dispatch")),
            ApplyPipelineHealth: () => calls.Add("apply"));
}
