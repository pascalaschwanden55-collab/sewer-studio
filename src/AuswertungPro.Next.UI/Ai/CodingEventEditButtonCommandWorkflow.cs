using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingEventEditButtonCommandOutcome
{
    NoSelection,
    EditRequested
}

public sealed record CodingEventEditButtonCommandRequest(object? SelectedItem);

public sealed record CodingEventEditButtonCommandActions(
    Action<CodingEvent> EditSelectedEvent);

public sealed record CodingEventEditButtonCommandResult(
    CodingEventEditButtonCommandOutcome Outcome)
{
    public bool Handled => Outcome == CodingEventEditButtonCommandOutcome.EditRequested;
}

public static class CodingEventEditButtonCommandWorkflow
{
    public static CodingEventEditButtonCommandResult Execute(
        CodingEventEditButtonCommandRequest request,
        CodingEventEditButtonCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SelectedItem is not CodingEvent selectedEvent)
            return Result(CodingEventEditButtonCommandOutcome.NoSelection);

        actions.EditSelectedEvent(selectedEvent);
        return Result(CodingEventEditButtonCommandOutcome.EditRequested);
    }

    private static CodingEventEditButtonCommandResult Result(
        CodingEventEditButtonCommandOutcome outcome)
        => new(outcome);
}
