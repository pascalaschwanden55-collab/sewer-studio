using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingProtocolMatchCommandOutcome
{
    NoCodingViewModel,
    Completed
}

public sealed record CodingProtocolMatchCommandRequest(
    bool HasCodingViewModel);

public sealed record CodingProtocolMatchCommandActions(
    Func<CodingMatchRouting> RunMatch,
    Action<CodingMatchRouting> StoreMatch,
    Action<CodingMatchRouting> UpdateSummary,
    Action RefreshEvents,
    Action ScheduleHighlights);

public sealed record CodingProtocolMatchCommandResult(
    CodingProtocolMatchCommandOutcome Outcome,
    CodingMatchRouting? Routing)
{
    public bool Completed => Outcome == CodingProtocolMatchCommandOutcome.Completed;
}

public static class CodingProtocolMatchCommandWorkflow
{
    public static CodingProtocolMatchCommandResult Execute(
        CodingProtocolMatchCommandRequest request,
        CodingProtocolMatchCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingProtocolMatchCommandOutcome.NoCodingViewModel, routing: null);

        var routing = actions.RunMatch();
        actions.StoreMatch(routing);
        actions.UpdateSummary(routing);
        actions.RefreshEvents();
        actions.ScheduleHighlights();
        return Result(CodingProtocolMatchCommandOutcome.Completed, routing);
    }

    private static CodingProtocolMatchCommandResult Result(
        CodingProtocolMatchCommandOutcome outcome,
        CodingMatchRouting? routing)
        => new(outcome, routing);
}
