using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingCreateSelectedCodeEventCommandOutcome
{
    NoViewModel,
    NoEventCreated,
    Created
}

public sealed record CodingCreateSelectedCodeEventCommandRequest(bool HasViewModel);

public sealed record CodingCreateSelectedCodeEventCommandActions(
    Func<TimeSpan> GetCurrentVideoTime,
    Action<TimeSpan> SetCurrentVideoTime,
    Func<TimeSpan, CodingEvent?> CreateEvent,
    Action<CodingEvent> ApplyPostCreation);

public sealed record CodingCreateSelectedCodeEventCommandResult(
    CodingCreateSelectedCodeEventCommandOutcome Outcome);

public static class CodingCreateSelectedCodeEventCommandWorkflow
{
    public static CodingCreateSelectedCodeEventCommandResult Execute(
        CodingCreateSelectedCodeEventCommandRequest request,
        CodingCreateSelectedCodeEventCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasViewModel)
            return Result(CodingCreateSelectedCodeEventCommandOutcome.NoViewModel);

        var videoTime = actions.GetCurrentVideoTime();
        actions.SetCurrentVideoTime(videoTime);

        var createdEvent = actions.CreateEvent(videoTime);
        if (createdEvent is null)
            return Result(CodingCreateSelectedCodeEventCommandOutcome.NoEventCreated);

        actions.ApplyPostCreation(createdEvent);
        return Result(CodingCreateSelectedCodeEventCommandOutcome.Created);
    }

    private static CodingCreateSelectedCodeEventCommandResult Result(
        CodingCreateSelectedCodeEventCommandOutcome outcome)
        => new(outcome);
}
