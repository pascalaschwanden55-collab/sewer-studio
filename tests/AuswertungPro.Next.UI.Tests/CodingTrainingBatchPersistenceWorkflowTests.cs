using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTrainingBatchPersistenceWorkflowTests
{
    [Fact]
    public void Execute_skips_without_coding_view_model()
    {
        var calls = new List<string>();

        var result = CodingTrainingBatchPersistenceWorkflow.Execute(
            new CodingTrainingBatchPersistenceWorkflowRequest(
                HasCodingViewModel: false,
                Events: Events("BBA")),
            Actions(calls));

        Assert.Equal(CodingTrainingBatchPersistenceWorkflowOutcome.NoCodingContext, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_skips_without_event_collection()
    {
        var calls = new List<string>();

        var result = CodingTrainingBatchPersistenceWorkflow.Execute(
            new CodingTrainingBatchPersistenceWorkflowRequest(
                HasCodingViewModel: true,
                Events: null),
            Actions(calls));

        Assert.Equal(CodingTrainingBatchPersistenceWorkflowOutcome.NoEvents, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_skips_empty_event_collection()
    {
        var calls = new List<string>();

        var result = CodingTrainingBatchPersistenceWorkflow.Execute(
            new CodingTrainingBatchPersistenceWorkflowRequest(
                HasCodingViewModel: true,
                Events: new ObservableCollection<CodingEvent>()),
            Actions(calls));

        Assert.Equal(CodingTrainingBatchPersistenceWorkflowOutcome.NoEvents, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_starts_persistence_for_existing_events()
    {
        var calls = new List<string>();
        var events = Events("BBA", "BCA");

        var result = CodingTrainingBatchPersistenceWorkflow.Execute(
            new CodingTrainingBatchPersistenceWorkflowRequest(
                HasCodingViewModel: true,
                Events: events),
            Actions(calls));

        Assert.Equal(CodingTrainingBatchPersistenceWorkflowOutcome.Started, result.Outcome);
        Assert.Equal(2, result.EventCount);
        Assert.Equal(["persist:2"], calls);
    }

    private static CodingTrainingBatchPersistenceWorkflowActions Actions(List<string> calls)
        => new(PersistEvents: events => calls.Add($"persist:{events.Count}"));

    private static ObservableCollection<CodingEvent> Events(params string[] codes)
        => new(codes.Select(code => new CodingEvent
        {
            Entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = code }
        }));
}
