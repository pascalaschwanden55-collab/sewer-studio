using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerKeyboardInputWorkflowTests
{
    [Fact]
    public void Execute_does_not_mark_handled_when_action_is_not_executed()
    {
        var calls = new List<string>();

        var result = PlayerKeyboardInputWorkflow.Execute(
            new PlayerKeyboardInputWorkflowRequest(PlayerKeyboardAction.TogglePlayPause),
            new PlayerKeyboardInputWorkflowActions(
                ExecuteAction: action =>
                {
                    calls.Add($"execute:{action}");
                    return false;
                },
                MarkHandled: () => calls.Add("handled")));

        Assert.Equal(PlayerKeyboardInputWorkflowOutcome.Unhandled, result.Outcome);
        Assert.False(result.Handled);
        Assert.Equal(["execute:TogglePlayPause"], calls);
    }

    [Fact]
    public void Execute_marks_handled_when_action_is_executed()
    {
        var calls = new List<string>();

        var result = PlayerKeyboardInputWorkflow.Execute(
            new PlayerKeyboardInputWorkflowRequest(PlayerKeyboardAction.TogglePlayPause),
            new PlayerKeyboardInputWorkflowActions(
                ExecuteAction: action =>
                {
                    calls.Add($"execute:{action}");
                    return true;
                },
                MarkHandled: () => calls.Add("handled")));

        Assert.Equal(PlayerKeyboardInputWorkflowOutcome.Handled, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["execute:TogglePlayPause", "handled"], calls);
    }
}
