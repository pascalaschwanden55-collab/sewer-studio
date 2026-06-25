using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationEditCommandWorkflowTests
{
    [Fact]
    public void Execute_closes_and_resumes_without_selecting_when_edit_returns_null()
    {
        var calls = new List<string>();

        var result = CodingConfirmationEditCommandWorkflow.Execute(
            new CodingConfirmationEditCommandActions(
                EditConfirmation: () =>
                {
                    calls.Add("edit");
                    return null;
                },
                CloseConfirmationPanel: () => calls.Add("close"),
                SelectEvent: _ => throw new InvalidOperationException("Select must not run."),
                ResumeAfterConfirmation: () => calls.Add("resume")));

        Assert.Equal(["edit", "close", "resume"], calls);
        Assert.Equal(CodingConfirmationEditCommandWorkflowOutcome.NoSelection, result.Outcome);
        Assert.False(result.Selected);
    }

    [Fact]
    public void Execute_selects_event_between_close_and_resume_when_edit_returns_event()
    {
        var calls = new List<string>();
        var ev = Event("BBA");

        var result = CodingConfirmationEditCommandWorkflow.Execute(
            new CodingConfirmationEditCommandActions(
                EditConfirmation: () =>
                {
                    calls.Add("edit");
                    return ev;
                },
                CloseConfirmationPanel: () => calls.Add("close"),
                SelectEvent: selected => calls.Add($"select:{selected.Entry.Code}"),
                ResumeAfterConfirmation: () => calls.Add("resume")));

        Assert.Equal(["edit", "close", "select:BBA", "resume"], calls);
        Assert.Equal(CodingConfirmationEditCommandWorkflowOutcome.Selected, result.Outcome);
        Assert.True(result.Selected);
    }

    private static CodingEvent Event(string code)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new ProtocolEntry { Code = code }
        };
}
