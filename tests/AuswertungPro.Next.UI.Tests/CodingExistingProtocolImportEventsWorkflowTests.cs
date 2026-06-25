using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingExistingProtocolImportEventsWorkflowTests
{
    [Fact]
    public void Execute_skips_without_import_collection()
    {
        var calls = new List<string>();

        var result = CodingExistingProtocolImportEventsWorkflow.Execute(
            new CodingExistingProtocolImportEventsWorkflowRequest(
                Protocol: ProtocolWithEntries(Entry("BBA", 1.2)),
                ImportEvents: null),
            Actions(calls));

        Assert.Equal(CodingExistingProtocolImportEventsWorkflowOutcome.NoImportCollection, result.Outcome);
        Assert.Equal(0, result.AddedCount);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_skips_without_protocol()
    {
        var calls = new List<string>();
        var importEvents = new ObservableCollection<CodingEvent>();

        var result = CodingExistingProtocolImportEventsWorkflow.Execute(
            new CodingExistingProtocolImportEventsWorkflowRequest(
                Protocol: null,
                ImportEvents: importEvents),
            Actions(calls));

        Assert.Equal(CodingExistingProtocolImportEventsWorkflowOutcome.NoProtocolEntries, result.Outcome);
        Assert.Empty(importEvents);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_updates_count_when_protocol_revision_has_no_importable_entries()
    {
        var calls = new List<string>();
        var importEvents = new ObservableCollection<CodingEvent>();

        var result = CodingExistingProtocolImportEventsWorkflow.Execute(
            new CodingExistingProtocolImportEventsWorkflowRequest(
                Protocol: new ProtocolDocument(),
                ImportEvents: importEvents),
            Actions(calls));

        Assert.Equal(CodingExistingProtocolImportEventsWorkflowOutcome.Loaded, result.Outcome);
        Assert.Equal(0, result.AddedCount);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(importEvents);
        Assert.Equal(["count:0"], calls);
    }

    [Fact]
    public void Execute_appends_missing_import_events_and_updates_count()
    {
        var calls = new List<string>();
        var importEvents = new ObservableCollection<CodingEvent>();
        var entry = Entry("BBA", 1.2);

        var result = CodingExistingProtocolImportEventsWorkflow.Execute(
            new CodingExistingProtocolImportEventsWorkflowRequest(
                Protocol: ProtocolWithEntries(entry),
                ImportEvents: importEvents),
            Actions(calls));

        Assert.Equal(CodingExistingProtocolImportEventsWorkflowOutcome.Loaded, result.Outcome);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.TotalCount);
        Assert.Same(entry, Assert.Single(importEvents).Entry);
        Assert.Equal(["count:1"], calls);
    }

    [Fact]
    public void Execute_updates_count_when_protocol_entries_exist_but_are_already_imported()
    {
        var calls = new List<string>();
        var entry = Entry("BBA", 1.2);
        var importEvents = new ObservableCollection<CodingEvent>
        {
            new() { Entry = entry, MeterAtCapture = 1.2 }
        };

        var result = CodingExistingProtocolImportEventsWorkflow.Execute(
            new CodingExistingProtocolImportEventsWorkflowRequest(
                Protocol: ProtocolWithEntries(entry),
                ImportEvents: importEvents),
            Actions(calls));

        Assert.Equal(CodingExistingProtocolImportEventsWorkflowOutcome.Loaded, result.Outcome);
        Assert.Equal(0, result.AddedCount);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(importEvents);
        Assert.Equal(["count:1"], calls);
    }

    private static CodingExistingProtocolImportEventsWorkflowActions Actions(List<string> calls)
        => new(SetImportCount: count => calls.Add($"count:{count}"));

    private static ProtocolDocument ProtocolWithEntries(params ProtocolEntry[] entries)
    {
        var revision = new ProtocolRevision();
        foreach (var entry in entries)
            revision.Entries.Add(entry);

        return new ProtocolDocument { Current = revision };
    }

    private static ProtocolEntry Entry(string code, double meter)
        => new()
        {
            EntryId = Guid.NewGuid(),
            Code = code,
            MeterStart = meter,
            Zeit = TimeSpan.FromSeconds(meter)
        };
}
