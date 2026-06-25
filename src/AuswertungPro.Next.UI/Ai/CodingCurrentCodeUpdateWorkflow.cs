using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingCurrentCodeUpdateOutcome
{
    Hidden,
    Applied
}

public sealed record CodingCurrentCodeUpdateRequest(
    bool HasViewModel);

public sealed record CodingCurrentCodeUpdateActions(
    Func<IEnumerable<CodingEvent>> GetEvents,
    Func<double> ResolveCurrentMeter,
    Action<CodingCurrentCodeBadgeState> ApplyState);

public sealed record CodingCurrentCodeUpdateResult(
    CodingCurrentCodeUpdateOutcome Outcome);

public static class CodingCurrentCodeUpdateWorkflow
{
    public static CodingCurrentCodeUpdateResult Execute(
        CodingCurrentCodeUpdateRequest request,
        CodingCurrentCodeUpdateActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasViewModel)
        {
            actions.ApplyState(CodingCurrentCodeBadgeState.Hidden);
            return Result(CodingCurrentCodeUpdateOutcome.Hidden);
        }

        var state = CodingCurrentCodeBadgePolicy.Build(
            actions.GetEvents(),
            actions.ResolveCurrentMeter());
        actions.ApplyState(state);
        return Result(CodingCurrentCodeUpdateOutcome.Applied);
    }

    private static CodingCurrentCodeUpdateResult Result(CodingCurrentCodeUpdateOutcome outcome)
        => new(outcome);
}
