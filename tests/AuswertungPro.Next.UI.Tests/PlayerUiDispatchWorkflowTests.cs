using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerUiDispatchWorkflowTests
{
    [Fact]
    public void Execute_applies_immediately_when_dispatcher_access_is_available()
    {
        var calls = new List<string>();

        var result = PlayerUiDispatchWorkflow.Execute(
            new PlayerUiDispatchWorkflowRequest(HasDispatcherAccess: true),
            Actions(calls));

        Assert.Equal(PlayerUiDispatchWorkflowOutcome.Applied, result.Outcome);
        Assert.Equal(["apply"], calls);
    }

    [Fact]
    public void Execute_dispatches_apply_when_dispatcher_access_is_missing()
    {
        var calls = new List<string>();
        Action? dispatched = null;

        var result = PlayerUiDispatchWorkflow.Execute(
            new PlayerUiDispatchWorkflowRequest(HasDispatcherAccess: false),
            Actions(
                calls,
                dispatchToUi: action =>
                {
                    calls.Add("dispatch");
                    dispatched = action;
                }));

        Assert.Equal(PlayerUiDispatchWorkflowOutcome.Dispatched, result.Outcome);
        Assert.Equal(["dispatch"], calls);

        dispatched!.Invoke();

        Assert.Equal(["dispatch", "apply"], calls);
    }

    private static PlayerUiDispatchWorkflowActions Actions(
        List<string> calls,
        Action<Action>? dispatchToUi = null)
        => new(
            Apply: () => calls.Add("apply"),
            DispatchToUi: dispatchToUi ?? (_ => calls.Add("dispatch")));
}
