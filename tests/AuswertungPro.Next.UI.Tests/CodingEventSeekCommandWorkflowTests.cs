using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventSeekCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_selected_event()
    {
        var result = CodingEventSeekCommandWorkflow.Execute(
            new CodingEventSeekCommandRequest(SelectedEvent: null),
            new CodingEventSeekCommandActions(
                SeekMilliseconds: _ => throw new InvalidOperationException("Seek should not run.")));

        Assert.Equal(CodingEventSeekCommandWorkflowOutcome.NoSelection, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_skips_when_event_has_no_seek_target()
    {
        var result = CodingEventSeekCommandWorkflow.Execute(
            new CodingEventSeekCommandRequest(Event(TimeSpan.Zero, hasProtocolTime: false)),
            new CodingEventSeekCommandActions(
                SeekMilliseconds: _ => throw new InvalidOperationException("Seek should not run.")));

        Assert.Equal(CodingEventSeekCommandWorkflowOutcome.NotSeekable, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_seeks_to_event_timestamp()
    {
        var calls = new List<string>();

        var result = CodingEventSeekCommandWorkflow.Execute(
            new CodingEventSeekCommandRequest(Event(TimeSpan.FromSeconds(7), hasProtocolTime: false)),
            new CodingEventSeekCommandActions(
                SeekMilliseconds: milliseconds => calls.Add($"seek:{milliseconds}")));

        Assert.Equal(["seek:7000"], calls);
        Assert.Equal(CodingEventSeekCommandWorkflowOutcome.Seeked, result.Outcome);
        Assert.True(result.Completed);
    }

    private static CodingEvent Event(TimeSpan videoTimestamp, bool hasProtocolTime)
        => new()
        {
            Entry = new ProtocolEntry { Zeit = hasProtocolTime ? TimeSpan.Zero : null },
            VideoTimestamp = videoTimestamp
        };
}
