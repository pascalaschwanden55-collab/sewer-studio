using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventCloseStretchCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_selected_event()
    {
        var result = CodingEventCloseStretchCommandWorkflow.Execute(
            new CodingEventCloseStretchCommandRequest(
                SelectedEvent: null,
                HasCodingViewModel: true),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingEventCloseStretchCommandWorkflowOutcome.NoSelection, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_skips_without_coding_view_model()
    {
        var calls = new List<string>();

        var result = CodingEventCloseStretchCommandWorkflow.Execute(
            new CodingEventCloseStretchCommandRequest(
                Event("BAJ"),
                HasCodingViewModel: false),
            Actions(calls.Add));

        Assert.Empty(calls);
        Assert.Equal(CodingEventCloseStretchCommandWorkflowOutcome.NoCodingViewModel, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_stops_after_unapplied_close()
    {
        var calls = new List<string>();

        var result = CodingEventCloseStretchCommandWorkflow.Execute(
            new CodingEventCloseStretchCommandRequest(
                Event("BAJ"),
                HasCodingViewModel: true),
            Actions(
                calls.Add,
                closeStretch: selected =>
                {
                    calls.Add($"close:{selected.Entry.Code}");
                    return new CodingEventCloseStretchActionResult(
                        Applied: false,
                        RequiresLaterMeterPrompt: false,
                        ShouldRefreshEvents: false,
                        StatusText: "");
                }));

        Assert.Equal(["close:BAJ"], calls);
        Assert.Equal(CodingEventCloseStretchCommandWorkflowOutcome.NotApplied, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_shows_later_meter_prompt_without_refresh()
    {
        var calls = new List<string>();

        var result = CodingEventCloseStretchCommandWorkflow.Execute(
            new CodingEventCloseStretchCommandRequest(
                Event("BAJ"),
                HasCodingViewModel: true),
            Actions(
                calls.Add,
                closeStretch: _ => new CodingEventCloseStretchActionResult(
                    Applied: true,
                    RequiresLaterMeterPrompt: true,
                    ShouldRefreshEvents: false,
                    StatusText: "")));

        Assert.Equal(["later-meter"], calls);
        Assert.Equal(CodingEventCloseStretchCommandWorkflowOutcome.RequiresLaterMeterPrompt, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_refreshes_and_reports_closed_status()
    {
        var calls = new List<string>();

        var result = CodingEventCloseStretchCommandWorkflow.Execute(
            new CodingEventCloseStretchCommandRequest(
                Event("BAJ"),
                HasCodingViewModel: true),
            Actions(
                calls.Add,
                closeStretch: _ => new CodingEventCloseStretchActionResult(
                    Applied: true,
                    RequiresLaterMeterPrompt: false,
                    ShouldRefreshEvents: true,
                    StatusText: "Streckenschaden geschlossen")));

        Assert.Equal(["refresh", "status:Streckenschaden geschlossen"], calls);
        Assert.Equal(CodingEventCloseStretchCommandWorkflowOutcome.Closed, result.Outcome);
        Assert.True(result.Completed);
    }

    private static CodingEventCloseStretchCommandActions Actions(
        Action<string> calls,
        Func<CodingEvent, CodingEventCloseStretchActionResult>? closeStretch = null)
        => new(
            CloseStretch: closeStretch ?? (_ => new CodingEventCloseStretchActionResult(
                Applied: true,
                RequiresLaterMeterPrompt: false,
                ShouldRefreshEvents: true,
                StatusText: "closed")),
            ShowRequiresLaterMeterPrompt: () => calls("later-meter"),
            RefreshEvents: () => calls("refresh"),
            ShowSuccessStatus: status => calls($"status:{status}"));

    private static CodingEvent Event(string code)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new ProtocolEntry { Code = code }
        };
}
