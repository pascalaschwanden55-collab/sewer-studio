using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventEditButtonCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_when_selected_item_is_not_coding_event()
    {
        var result = CodingEventEditButtonCommandWorkflow.Execute(
            new CodingEventEditButtonCommandRequest(SelectedItem: "not an event"),
            new CodingEventEditButtonCommandActions(
                EditSelectedEvent: _ => throw new InvalidOperationException("Edit should not run.")));

        Assert.Equal(CodingEventEditButtonCommandOutcome.NoSelection, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Execute_edits_selected_coding_event()
    {
        var ev = Event("BCA");
        CodingEvent? edited = null;

        var result = CodingEventEditButtonCommandWorkflow.Execute(
            new CodingEventEditButtonCommandRequest(ev),
            new CodingEventEditButtonCommandActions(
                EditSelectedEvent: selected => edited = selected));

        Assert.Equal(CodingEventEditButtonCommandOutcome.EditRequested, result.Outcome);
        Assert.True(result.Handled);
        Assert.Same(ev, edited);
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
