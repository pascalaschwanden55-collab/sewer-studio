using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingEventSeekCommandWorkflowOutcome
{
    NoSelection,
    NotSeekable,
    Seeked
}

public sealed record CodingEventSeekCommandRequest(
    CodingEvent? SelectedEvent);

public sealed record CodingEventSeekCommandActions(
    Action<long> SeekMilliseconds);

public sealed record CodingEventSeekCommandWorkflowResult(
    CodingEventSeekCommandWorkflowOutcome Outcome)
{
    public bool Completed => Outcome == CodingEventSeekCommandWorkflowOutcome.Seeked;
}

public static class CodingEventSeekCommandWorkflow
{
    public static CodingEventSeekCommandWorkflowResult Execute(
        CodingEventSeekCommandRequest request,
        CodingEventSeekCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SelectedEvent is not { } selectedEvent)
            return Result(CodingEventSeekCommandWorkflowOutcome.NoSelection);

        if (!CodingEventSeekPolicy.TryGetSeekMilliseconds(selectedEvent, out var milliseconds))
            return Result(CodingEventSeekCommandWorkflowOutcome.NotSeekable);

        actions.SeekMilliseconds(milliseconds);
        return Result(CodingEventSeekCommandWorkflowOutcome.Seeked);
    }

    private static CodingEventSeekCommandWorkflowResult Result(
        CodingEventSeekCommandWorkflowOutcome outcome)
        => new(outcome);
}
