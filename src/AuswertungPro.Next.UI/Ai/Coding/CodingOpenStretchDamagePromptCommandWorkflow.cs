using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingOpenStretchDamagePromptCommandOutcome
{
    NoSession,
    NoOpenEvents,
    Continued,
    Cancelled,
    CloseRequestedNoChanges,
    Closed
}

public sealed record CodingOpenStretchDamagePromptCommandRequest(
    bool HasCodingViewModel,
    IEnumerable<CodingEvent> Events,
    double CurrentMeter);

public sealed record CodingOpenStretchDamagePromptCommandActions(
    Func<IEnumerable<CodingEvent>, IReadOnlyList<CodingEvent>> FindOpen,
    Func<IReadOnlyList<CodingEvent>, double, CodingOpenStretchDamageDialogDecision> ConfirmClose,
    Func<IReadOnlyList<CodingEvent>, double, bool> ApplyClose,
    Action RefreshEvents);

public sealed record CodingOpenStretchDamagePromptCommandResult(
    CodingOpenStretchDamagePromptCommandOutcome Outcome,
    bool ShouldContinue);

public static class CodingOpenStretchDamagePromptCommandWorkflow
{
    public static CodingOpenStretchDamagePromptCommandResult Execute(
        CodingOpenStretchDamagePromptCommandRequest request,
        CodingOpenStretchDamagePromptCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Continue(CodingOpenStretchDamagePromptCommandOutcome.NoSession);

        var openEvents = actions.FindOpen(request.Events);
        if (openEvents.Count == 0)
            return Continue(CodingOpenStretchDamagePromptCommandOutcome.NoOpenEvents);

        var decision = actions.ConfirmClose(openEvents, request.CurrentMeter);
        if (decision == CodingOpenStretchDamageDialogDecision.Cancel)
            return new(CodingOpenStretchDamagePromptCommandOutcome.Cancelled, ShouldContinue: false);

        if (decision != CodingOpenStretchDamageDialogDecision.Close)
            return Continue(CodingOpenStretchDamagePromptCommandOutcome.Continued);

        if (!actions.ApplyClose(openEvents, request.CurrentMeter))
            return Continue(CodingOpenStretchDamagePromptCommandOutcome.CloseRequestedNoChanges);

        actions.RefreshEvents();
        return Continue(CodingOpenStretchDamagePromptCommandOutcome.Closed);
    }

    private static CodingOpenStretchDamagePromptCommandResult Continue(
        CodingOpenStretchDamagePromptCommandOutcome outcome)
        => new(outcome, ShouldContinue: true);
}
