using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionStartWorkflowTests
{
    [Fact]
    public void Execute_returns_false_without_actions_when_required_state_is_missing()
    {
        var calls = new List<string>();

        var result = CodingSessionStartWorkflow.Execute(
            new CodingSessionStartWorkflowRequest(HasRequiredState: false, EndMeter: 12.3),
            Actions(calls));

        Assert.False(result);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_shows_error_and_exits_when_start_command_throws()
    {
        var calls = new List<string>();

        var result = CodingSessionStartWorkflow.Execute(
            new CodingSessionStartWorkflowRequest(HasRequiredState: true, EndMeter: 12.3),
            Actions(
                calls,
                executeStartSession: () =>
                {
                    calls.Add("execute");
                    throw new InvalidOperationException("Laenge fehlt");
                }));

        Assert.False(result);
        Assert.Equal(["execute", "error:Laenge fehlt", "exit"], calls);
    }

    [Fact]
    public void Execute_exits_when_start_command_does_not_create_active_session()
    {
        var calls = new List<string>();

        var result = CodingSessionStartWorkflow.Execute(
            new CodingSessionStartWorkflowRequest(HasRequiredState: true, EndMeter: 12.3),
            Actions(
                calls,
                hasActiveSession: () =>
                {
                    calls.Add("has-active");
                    return false;
                }));

        Assert.False(result);
        Assert.Equal(["execute", "has-active", "exit"], calls);
    }

    [Fact]
    public void Execute_pauses_session_and_initializes_header_when_active_session_exists()
    {
        var calls = new List<string>();

        var result = CodingSessionStartWorkflow.Execute(
            new CodingSessionStartWorkflowRequest(HasRequiredState: true, EndMeter: 12.3),
            Actions(calls));

        Assert.True(result);
        Assert.Equal(["execute", "has-active", "pause", "range:12.3", "meter:0.0"], calls);
    }

    private static CodingSessionStartWorkflowActions Actions(
        List<string> calls,
        Action? executeStartSession = null,
        Func<bool>? hasActiveSession = null)
        => new(
            ExecuteStartSession: executeStartSession ?? (() => calls.Add("execute")),
            HasActiveSession: hasActiveSession ?? (() =>
            {
                calls.Add("has-active");
                return true;
            }),
            ShowSessionStartFailed: message => calls.Add($"error:{message}"),
            ExitCodingMode: () => calls.Add("exit"),
            PauseSession: () => calls.Add("pause"),
            SetRangeText: endMeter => calls.Add($"range:{endMeter:F1}"),
            SetMeterText: meter => calls.Add($"meter:{meter:F1}"));
}
