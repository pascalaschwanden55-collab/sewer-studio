using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingUnappliedChangesCloseWorkflowOutcome
{
    NoCodingContext,
    NoChanges,
    Prompted
}

public sealed record CodingUnappliedChangesCloseWorkflowRequest(
    bool IsCodingMode,
    bool HasCodingViewModel,
    IEnumerable<CodingEvent> Events,
    string BaselineSignature);

public sealed record CodingUnappliedChangesCloseWorkflowActions(
    Func<IEnumerable<CodingEvent>, string> BuildSignature,
    Func<bool> ConfirmWithSuspendedOverlay);

public sealed record CodingUnappliedChangesCloseWorkflowResult(
    CodingUnappliedChangesCloseWorkflowOutcome Outcome,
    bool ShouldClose);

public static class CodingUnappliedChangesCloseWorkflow
{
    public static CodingUnappliedChangesCloseWorkflowResult Execute(
        CodingUnappliedChangesCloseWorkflowRequest request,
        CodingUnappliedChangesCloseWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsCodingMode || !request.HasCodingViewModel)
            return Result(CodingUnappliedChangesCloseWorkflowOutcome.NoCodingContext, shouldClose: true);

        var currentSignature = actions.BuildSignature(request.Events);
        if (string.Equals(currentSignature, request.BaselineSignature, StringComparison.Ordinal))
            return Result(CodingUnappliedChangesCloseWorkflowOutcome.NoChanges, shouldClose: true);

        return Result(
            CodingUnappliedChangesCloseWorkflowOutcome.Prompted,
            actions.ConfirmWithSuspendedOverlay());
    }

    private static CodingUnappliedChangesCloseWorkflowResult Result(
        CodingUnappliedChangesCloseWorkflowOutcome outcome,
        bool shouldClose)
        => new(outcome, shouldClose);
}
