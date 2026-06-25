using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportConfirmCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_when_selected_item_is_not_coding_event()
    {
        var result = await CodingImportConfirmCommandWorkflow.ExecuteAsync(
            new CodingImportConfirmCommandRequest(SelectedItem: "not an event"),
            new CodingImportConfirmCommandActions(
                ConfirmImportAsTrainingAsync: _ => throw new InvalidOperationException("Confirm should not run.")));

        Assert.Equal(CodingImportConfirmCommandOutcome.NoSelection, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_reports_rejected_import_confirmation()
    {
        var ev = Event("BAB");
        CodingEvent? confirmed = null;

        var result = await CodingImportConfirmCommandWorkflow.ExecuteAsync(
            new CodingImportConfirmCommandRequest(ev),
            new CodingImportConfirmCommandActions(
                ConfirmImportAsTrainingAsync: selected =>
                {
                    confirmed = selected;
                    return Task.FromResult(false);
                }));

        Assert.Equal(CodingImportConfirmCommandOutcome.Rejected, result.Outcome);
        Assert.False(result.Completed);
        Assert.Same(ev, confirmed);
    }

    [Fact]
    public async Task ExecuteAsync_reports_confirmed_import_confirmation()
    {
        var ev = Event("BAB");
        CodingEvent? confirmed = null;

        var result = await CodingImportConfirmCommandWorkflow.ExecuteAsync(
            new CodingImportConfirmCommandRequest(ev),
            new CodingImportConfirmCommandActions(
                ConfirmImportAsTrainingAsync: selected =>
                {
                    confirmed = selected;
                    return Task.FromResult(true);
                }));

        Assert.Equal(CodingImportConfirmCommandOutcome.Confirmed, result.Outcome);
        Assert.True(result.Completed);
        Assert.Same(ev, confirmed);
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
