using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingExistingProtocolImportEventsWorkflowOutcome
{
    NoProtocolEntries,
    NoImportCollection,
    Loaded
}

public sealed record CodingExistingProtocolImportEventsWorkflowRequest(
    ProtocolDocument? Protocol,
    ObservableCollection<CodingEvent>? ImportEvents);

public sealed record CodingExistingProtocolImportEventsWorkflowActions(
    Action<int> SetImportCount);

public sealed record CodingExistingProtocolImportEventsWorkflowResult(
    CodingExistingProtocolImportEventsWorkflowOutcome Outcome,
    int AddedCount,
    int TotalCount);

public static class CodingExistingProtocolImportEventsWorkflow
{
    public static CodingExistingProtocolImportEventsWorkflowResult Execute(
        CodingExistingProtocolImportEventsWorkflowRequest request,
        CodingExistingProtocolImportEventsWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.Protocol?.Current?.Entries is null)
            return Result(CodingExistingProtocolImportEventsWorkflowOutcome.NoProtocolEntries);

        if (request.ImportEvents is null)
            return Result(CodingExistingProtocolImportEventsWorkflowOutcome.NoImportCollection);

        var added = CodingProtocolEventCollectionAppender.Append(
            request.ImportEvents,
            CodingProtocolEventMapper.BuildMissingImportEvents(
                request.Protocol,
                request.ImportEvents));

        var totalCount = request.ImportEvents.Count;
        actions.SetImportCount(totalCount);

        return new CodingExistingProtocolImportEventsWorkflowResult(
            CodingExistingProtocolImportEventsWorkflowOutcome.Loaded,
            added,
            totalCount);
    }

    private static CodingExistingProtocolImportEventsWorkflowResult Result(
        CodingExistingProtocolImportEventsWorkflowOutcome outcome)
        => new(outcome, AddedCount: 0, TotalCount: 0);
}
