using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingEventCloseStretchCommandWorkflowOutcome
{
    NoSelection,
    NoCodingViewModel,
    NotApplied,
    RequiresLaterMeterPrompt,
    Closed
}

public sealed record CodingEventCloseStretchCommandRequest(
    CodingEvent? SelectedEvent,
    bool HasCodingViewModel);

public sealed record CodingEventCloseStretchCommandActions(
    Func<CodingEvent, CodingEventCloseStretchActionResult> CloseStretch,
    Action ShowRequiresLaterMeterPrompt,
    Action RefreshEvents,
    Action<string> ShowSuccessStatus);

public sealed record CodingEventCloseStretchCommandWorkflowResult(
    CodingEventCloseStretchCommandWorkflowOutcome Outcome)
{
    public bool Completed => Outcome == CodingEventCloseStretchCommandWorkflowOutcome.Closed;
}

public static class CodingEventCloseStretchCommandWorkflow
{
    public static CodingEventCloseStretchCommandWorkflowResult Execute(
        CodingEventCloseStretchCommandRequest request,
        CodingEventCloseStretchCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SelectedEvent is not { } selectedEvent)
            return Result(CodingEventCloseStretchCommandWorkflowOutcome.NoSelection);

        if (!request.HasCodingViewModel)
            return Result(CodingEventCloseStretchCommandWorkflowOutcome.NoCodingViewModel);

        var closeAction = actions.CloseStretch(selectedEvent);
        if (!closeAction.Applied)
            return Result(CodingEventCloseStretchCommandWorkflowOutcome.NotApplied);

        if (closeAction.RequiresLaterMeterPrompt)
        {
            actions.ShowRequiresLaterMeterPrompt();
            return Result(CodingEventCloseStretchCommandWorkflowOutcome.RequiresLaterMeterPrompt);
        }

        if (closeAction.ShouldRefreshEvents)
            actions.RefreshEvents();

        actions.ShowSuccessStatus(closeAction.StatusText);
        return Result(CodingEventCloseStretchCommandWorkflowOutcome.Closed);
    }

    private static CodingEventCloseStretchCommandWorkflowResult Result(
        CodingEventCloseStretchCommandWorkflowOutcome outcome)
        => new(outcome);
}
