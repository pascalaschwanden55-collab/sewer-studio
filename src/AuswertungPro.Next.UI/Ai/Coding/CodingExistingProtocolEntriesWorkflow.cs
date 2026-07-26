using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingExistingProtocolEntriesWorkflowOutcome
{
    NoCodingContext,
    NoExistingEvents,
    NoEventCollection,
    Appended
}

public sealed record CodingExistingProtocolEntriesWorkflowRequest(
    bool HasCodingViewModel,
    HaltungRecord? HaltungRecord,
    ObservableCollection<CodingEvent>? EventCollection);

public sealed record CodingExistingProtocolEntriesWorkflowResult(
    CodingExistingProtocolEntriesWorkflowOutcome Outcome,
    int AddedCount)
{
    public bool Appended => Outcome == CodingExistingProtocolEntriesWorkflowOutcome.Appended;
}

public static class CodingExistingProtocolEntriesWorkflow
{
    public static CodingExistingProtocolEntriesWorkflowResult Execute(
        CodingExistingProtocolEntriesWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.HasCodingViewModel || request.HaltungRecord is null)
            return Result(CodingExistingProtocolEntriesWorkflowOutcome.NoCodingContext);

        var events = CodingProtocolEventMapper.BuildExistingEvents(request.HaltungRecord.Protocol);
        if (events.Count == 0)
            return Result(CodingExistingProtocolEntriesWorkflowOutcome.NoExistingEvents);

        if (request.EventCollection is null)
            return Result(CodingExistingProtocolEntriesWorkflowOutcome.NoEventCollection);

        var added = CodingProtocolEventCollectionAppender.Append(request.EventCollection, events);
        return new CodingExistingProtocolEntriesWorkflowResult(
            CodingExistingProtocolEntriesWorkflowOutcome.Appended,
            added);
    }

    private static CodingExistingProtocolEntriesWorkflowResult Result(
        CodingExistingProtocolEntriesWorkflowOutcome outcome)
        => new(outcome, AddedCount: 0);
}
