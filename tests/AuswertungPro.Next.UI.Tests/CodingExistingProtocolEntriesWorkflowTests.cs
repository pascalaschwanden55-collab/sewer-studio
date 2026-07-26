using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingExistingProtocolEntriesWorkflowTests
{
    [Fact]
    public void Execute_skips_without_coding_context()
    {
        var result = CodingExistingProtocolEntriesWorkflow.Execute(
            new CodingExistingProtocolEntriesWorkflowRequest(
                HasCodingViewModel: false,
                HaltungRecord: RecordWithEntry("BBA"),
                EventCollection: new ObservableCollection<CodingEvent>()));

        Assert.Equal(CodingExistingProtocolEntriesWorkflowOutcome.NoCodingContext, result.Outcome);
        Assert.Equal(0, result.AddedCount);
        Assert.False(result.Appended);
    }

    [Fact]
    public void Execute_skips_without_existing_protocol_events()
    {
        var collection = new ObservableCollection<CodingEvent>();

        var result = CodingExistingProtocolEntriesWorkflow.Execute(
            new CodingExistingProtocolEntriesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: new HaltungRecord(),
                EventCollection: collection));

        Assert.Equal(CodingExistingProtocolEntriesWorkflowOutcome.NoExistingEvents, result.Outcome);
        Assert.Empty(collection);
    }

    [Fact]
    public void Execute_skips_without_event_collection_after_mapping()
    {
        var result = CodingExistingProtocolEntriesWorkflow.Execute(
            new CodingExistingProtocolEntriesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: RecordWithEntry("BBA"),
                EventCollection: null));

        Assert.Equal(CodingExistingProtocolEntriesWorkflowOutcome.NoEventCollection, result.Outcome);
        Assert.Equal(0, result.AddedCount);
    }

    [Fact]
    public void Execute_appends_existing_protocol_events()
    {
        var collection = new ObservableCollection<CodingEvent>();

        var result = CodingExistingProtocolEntriesWorkflow.Execute(
            new CodingExistingProtocolEntriesWorkflowRequest(
                HasCodingViewModel: true,
                HaltungRecord: RecordWithEntry("BBA"),
                EventCollection: collection));

        Assert.Equal(CodingExistingProtocolEntriesWorkflowOutcome.Appended, result.Outcome);
        Assert.True(result.Appended);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal("BBA", Assert.Single(collection).Entry.Code);
    }

    private static HaltungRecord RecordWithEntry(string code)
        => new()
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    {
                        new ProtocolEntry
                        {
                            EntryId = Guid.NewGuid(),
                            Code = code,
                            MeterStart = 1.2,
                            Zeit = TimeSpan.FromSeconds(3)
                        }
                    }
                }
            }
        };
}
