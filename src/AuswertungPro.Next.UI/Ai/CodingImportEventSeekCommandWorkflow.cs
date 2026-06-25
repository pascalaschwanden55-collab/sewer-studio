using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingImportEventSeekCommandOutcome
{
    NoSelection,
    NoSeekTarget,
    SeekedByTimestamp,
    SeekedByMeter
}

public sealed record CodingImportEventSeekCommandRequest(
    object? SelectedItem,
    bool HasCodingSessionService);

public sealed record CodingImportEventSeekCommandActions(
    Action<long> SeekMilliseconds,
    Action<double> MoveToMeter,
    Action MarkNavigationPending,
    Action SyncVideoToCodingMeter);

public sealed record CodingImportEventSeekCommandResult(
    CodingImportEventSeekCommandOutcome Outcome)
{
    public bool Completed =>
        Outcome is CodingImportEventSeekCommandOutcome.SeekedByTimestamp
            or CodingImportEventSeekCommandOutcome.SeekedByMeter;
}

public static class CodingImportEventSeekCommandWorkflow
{
    public static CodingImportEventSeekCommandResult Execute(
        CodingImportEventSeekCommandRequest request,
        CodingImportEventSeekCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SelectedItem is not CodingEvent importEvent)
            return Result(CodingImportEventSeekCommandOutcome.NoSelection);

        if (CodingEventSeekPolicy.TryGetSeekMilliseconds(importEvent, out var milliseconds))
        {
            actions.SeekMilliseconds(milliseconds);
            return Result(CodingImportEventSeekCommandOutcome.SeekedByTimestamp);
        }

        if (!request.HasCodingSessionService || importEvent.MeterAtCapture <= 0)
            return Result(CodingImportEventSeekCommandOutcome.NoSeekTarget);

        actions.MoveToMeter(importEvent.MeterAtCapture);
        actions.MarkNavigationPending();
        actions.SyncVideoToCodingMeter();
        return Result(CodingImportEventSeekCommandOutcome.SeekedByMeter);
    }

    private static CodingImportEventSeekCommandResult Result(
        CodingImportEventSeekCommandOutcome outcome)
        => new(outcome);
}
