using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingTrainingBatchPersistenceWorkflowOutcome
{
    NoCodingContext,
    NoEvents,
    Started
}

public sealed record CodingTrainingBatchPersistenceWorkflowRequest(
    bool HasCodingViewModel,
    IReadOnlyList<CodingEvent>? Events);

public sealed record CodingTrainingBatchPersistenceWorkflowActions(
    Action<IReadOnlyList<CodingEvent>> PersistEvents);

public sealed record CodingTrainingBatchPersistenceWorkflowResult(
    CodingTrainingBatchPersistenceWorkflowOutcome Outcome,
    int EventCount);

public static class CodingTrainingBatchPersistenceWorkflow
{
    public static CodingTrainingBatchPersistenceWorkflowResult Execute(
        CodingTrainingBatchPersistenceWorkflowRequest request,
        CodingTrainingBatchPersistenceWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingTrainingBatchPersistenceWorkflowOutcome.NoCodingContext);

        if (request.Events is null || request.Events.Count == 0)
            return Result(CodingTrainingBatchPersistenceWorkflowOutcome.NoEvents);

        actions.PersistEvents(request.Events);
        return new CodingTrainingBatchPersistenceWorkflowResult(
            CodingTrainingBatchPersistenceWorkflowOutcome.Started,
            request.Events.Count);
    }

    private static CodingTrainingBatchPersistenceWorkflowResult Result(
        CodingTrainingBatchPersistenceWorkflowOutcome outcome)
        => new(outcome, EventCount: 0);
}
