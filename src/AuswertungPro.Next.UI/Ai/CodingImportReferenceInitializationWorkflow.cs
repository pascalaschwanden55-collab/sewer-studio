using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingImportReferenceInitializationWorkflowOutcome
{
    MissingRequiredState,
    Initialized
}

public sealed record CodingImportReferenceInitializationWorkflowRequest(
    bool HasCodingViewModel,
    bool HasEventCollection);

public sealed record CodingImportReferenceInitializationWorkflowActions(
    Func<CodingMatchRouting?> ResetProtocolMatchState,
    Action<CodingMatchRouting?> UpdateProtocolMatchSummary,
    Func<int> MoveExistingEventsToImportReference,
    Action SetImportItemsSource,
    Action<int> SetImportCount,
    Action ClearActiveSessionEvents,
    Action SetCodingItemsSource,
    Action<int> SetCodingCount,
    Func<string> BuildBaselineSignature,
    Action<string> SetBaselineSignature,
    Action ResetStretchTracker);

public sealed record CodingImportReferenceInitializationWorkflowResult(
    CodingImportReferenceInitializationWorkflowOutcome Outcome,
    int ImportEventCount)
{
    public bool Initialized => Outcome == CodingImportReferenceInitializationWorkflowOutcome.Initialized;
}

public static class CodingImportReferenceInitializationWorkflow
{
    public static CodingImportReferenceInitializationWorkflowResult Execute(
        CodingImportReferenceInitializationWorkflowRequest request,
        CodingImportReferenceInitializationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel || !request.HasEventCollection)
            return Result(CodingImportReferenceInitializationWorkflowOutcome.MissingRequiredState, importEventCount: 0);

        var matchRouting = actions.ResetProtocolMatchState();
        actions.UpdateProtocolMatchSummary(matchRouting);

        var importEventCount = actions.MoveExistingEventsToImportReference();
        actions.SetImportItemsSource();
        actions.SetImportCount(importEventCount);

        actions.ClearActiveSessionEvents();

        actions.SetCodingItemsSource();
        actions.SetCodingCount(0);

        var baselineSignature = actions.BuildBaselineSignature();
        actions.SetBaselineSignature(baselineSignature);
        actions.ResetStretchTracker();

        return Result(CodingImportReferenceInitializationWorkflowOutcome.Initialized, importEventCount);
    }

    private static CodingImportReferenceInitializationWorkflowResult Result(
        CodingImportReferenceInitializationWorkflowOutcome outcome,
        int importEventCount)
        => new(outcome, importEventCount);
}
